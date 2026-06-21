using CarServ.MVC.Models;
using CarServ.MVC.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminStaffOrTechnician)]
    public class HomeController : Controller
    {
        private readonly CarServContext _context;

        public HomeController(CarServContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var chartStart = today.AddDays(-6);

            // Week calculations (Monday to Sunday)
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var weekStart = today.AddDays(-1 * diff).Date;
            var nextWeek = weekStart.AddDays(7);

            // ── 1. Paid Orders (with detail for transactions) ──
            var paidOrderDetails = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Where(o => o.PaymentStatus == AppConstants.PaymentStatus.Paid && o.OrderDate.HasValue)
                .Select(o => new
                {
                    Date = o.PaymentDate ?? o.OrderDate!.Value,
                    Amount = o.FinalAmount ?? o.TotalAmount ?? 0,
                    Code = o.OrderCode ?? $"ORD-{o.OrderId}",
                    CustomerName = o.Customer != null ? o.Customer.FullName : "Khách vãng lai",
                    PaymentMethod = o.PaymentMethod ?? "N/A",
                    Items = o.OrderItems.Select(i => i.ProductName ?? "Sản phẩm").ToList()
                })
                .ToListAsync();

            // ── 2. Paid Invoices (with detail) ──
            var paidInvoiceDetails = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.ServiceHistory)
                .Where(i => i.PaymentStatus == AppConstants.PaymentStatus.Paid)
                .Select(i => new
                {
                    Date = i.PaymentDate ?? i.InvoiceDate,
                    Amount = i.TotalAmount,
                    Code = i.InvoiceCode,
                    CustomerName = i.Customer.FullName,
                    PaymentMethod = i.PaymentMethod ?? "N/A",
                    ServiceName = i.ServiceHistory != null ? (i.ServiceHistory.ServiceName ?? "Dịch vụ") : "Dịch vụ"
                })
                .ToListAsync();

            // ── Build unified transaction list ──
            var allTransactions = new List<RevenueTransactionDto>();

            allTransactions.AddRange(paidOrderDetails.Select(o => new RevenueTransactionDto
            {
                TransactionCode = o.Code,
                CustomerName = o.CustomerName,
                ItemDescription = string.Join(", ", o.Items),
                PaymentMethod = o.PaymentMethod,
                Amount = o.Amount,
                PaymentDate = o.Date,
                Source = "Đơn hàng"
            }));

            allTransactions.AddRange(paidInvoiceDetails.Select(i => new RevenueTransactionDto
            {
                TransactionCode = i.Code,
                CustomerName = i.CustomerName,
                ItemDescription = i.ServiceName,
                PaymentMethod = i.PaymentMethod,
                Amount = i.Amount,
                PaymentDate = i.Date,
                Source = "Hóa đơn"
            }));

            // ── Revenue aggregation (same logic as before) ──
            var revenues = allTransactions
                .Select(t => new { Date = t.PaymentDate.Date, t.Amount })
                .ToList();

            // ── Revenue by source ──
            var productRevenue = paidOrderDetails.Sum(o => o.Amount);
            var serviceRevenue = paidInvoiceDetails.Sum(i => i.Amount);

            // ── Top 5 Products ──
            var topProducts = await _context.OrderItems
                .Where(oi => oi.Order != null && oi.Order.PaymentStatus == AppConstants.PaymentStatus.Paid)
                .GroupBy(oi => oi.ProductName ?? "Sản phẩm")
                .Select(g => new TopItemDto
                {
                    Name = g.Key,
                    Count = g.Sum(x => x.Quantity ?? 1),
                    Revenue = g.Sum(x => x.TotalPrice ?? 0)
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            // ── Top 5 Services ──
            // From invoices
            var invoiceServiceStats = await _context.Invoices
                .Where(i => i.PaymentStatus == AppConstants.PaymentStatus.Paid && i.ServiceHistory != null)
                .GroupBy(i => i.ServiceHistory!.ServiceName ?? "Dịch vụ")
                .Select(g => new { Name = g.Key, Count = g.Count(), Revenue = g.Sum(x => x.TotalAmount) })
                .ToListAsync();

            var topServices = invoiceServiceStats
                .GroupBy(x => x.Name)
                .Select(g => new TopItemDto
                {
                    Name = g.Key,
                    Count = g.Sum(x => x.Count),
                    Revenue = g.Sum(x => x.Revenue)
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();

            // ── Build model ──
            var model = new AdminDashboardViewModel
            {
                TodayRevenue = revenues.Where(x => x.Date == today).Sum(x => x.Amount),
                WeekRevenue = revenues.Where(x => x.Date >= weekStart && x.Date < nextWeek).Sum(x => x.Amount),
                MonthRevenue = revenues.Where(x => x.Date >= monthStart && x.Date < nextMonth).Sum(x => x.Amount),
                TotalRevenue = revenues.Sum(x => x.Amount),
                PaidOrderCount = paidOrderDetails.Count,
                PaidInvoiceCount = paidInvoiceDetails.Count,
                PendingAppointmentCount = await _context.Appointments.CountAsync(a => a.Status == AppConstants.AppointmentStatus.Pending),
                CompletedAppointmentCount = await _context.Appointments.CountAsync(a => a.Status == AppConstants.AppointmentStatus.Completed),
                LowStockProductCount = await _context.Products.CountAsync(p => (p.StockQuantity ?? 0) <= 5),

                // New data
                TodayTransactions = allTransactions.Where(t => t.PaymentDate.Date == today).OrderByDescending(t => t.PaymentDate).ToList(),
                WeekTransactions = allTransactions.Where(t => t.PaymentDate.Date >= weekStart && t.PaymentDate.Date < nextWeek).OrderByDescending(t => t.PaymentDate).ToList(),
                MonthTransactions = allTransactions.Where(t => t.PaymentDate.Date >= monthStart && t.PaymentDate.Date < nextMonth).OrderByDescending(t => t.PaymentDate).ToList(),
                AllTransactions = allTransactions.OrderByDescending(t => t.PaymentDate).ToList(),
                ServiceRevenue = serviceRevenue,
                ProductRevenue = productRevenue,
                TopProducts = topProducts,
                TopServices = topServices
            };

            // ── 7-day chart with daily details ──
            for (var date = chartStart; date <= today; date = date.AddDays(1))
            {
                var dayTransactions = allTransactions.Where(t => t.PaymentDate.Date == date).OrderByDescending(t => t.PaymentDate).ToList();
                var dayTotal = dayTransactions.Sum(t => t.Amount);

                model.RevenueLabels.Add(date.ToString("dd/MM"));
                model.RevenueValues.Add(dayTotal);
                model.DailyRevenueDetails.Add(new DailyRevenueDetailDto
                {
                    DateLabel = date.ToString("dd/MM/yyyy"),
                    Date = date,
                    TotalRevenue = dayTotal,
                    Transactions = dayTransactions
                });
            }



            return View(model);
        }

        // ── API: Revenue by custom time period (AJAX) ──
        [HttpGet]
        public async Task<IActionResult> RevenueByPeriod(string period, DateTime? startDate, DateTime? endDate)
        {
            var today = DateTime.Today;
            DateTime filterStart, filterEnd;
            bool groupByMonth = false;

            switch (period)
            {
                case "today":
                    filterStart = today;
                    filterEnd = today;
                    break;
                case "7days":
                    filterStart = today.AddDays(-6);
                    filterEnd = today;
                    break;
                case "30days":
                    filterStart = today.AddDays(-29);
                    filterEnd = today;
                    break;
                case "3months":
                    filterStart = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
                    filterEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
                    groupByMonth = true;
                    break;
                case "6months":
                    filterStart = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
                    filterEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
                    groupByMonth = true;
                    break;
                case "1year":
                    filterStart = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
                    filterEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
                    groupByMonth = true;
                    break;
                case "custom":
                    filterStart = startDate?.Date ?? today.AddDays(-29);
                    filterEnd = endDate?.Date ?? today;
                    break;
                default:
                    filterStart = today.AddDays(-6);
                    filterEnd = today;
                    break;
            }

            // Orders in range
            var ordersQuery = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .Where(o => o.PaymentStatus == AppConstants.PaymentStatus.Paid && o.OrderDate.HasValue)
                .Where(o => (o.PaymentDate ?? o.OrderDate!.Value).Date >= filterStart.Date && (o.PaymentDate ?? o.OrderDate!.Value).Date <= filterEnd.Date)
                .Select(o => new
                {
                    o.OrderCode,
                    o.OrderId,
                    o.Customer,
                    o.OrderItems,
                    o.PaymentMethod,
                    FinalAmount = o.FinalAmount ?? o.TotalAmount ?? 0,
                    PaymentDate = o.PaymentDate ?? o.OrderDate!.Value
                })
                .ToListAsync();

            var orders = ordersQuery.Select(o => new RevenueTransactionDto
            {
                TransactionCode = o.OrderCode ?? ("ORD-" + o.OrderId),
                CustomerName = o.Customer != null ? o.Customer.FullName : "Khách vãng lai",
                ItemDescription = string.Join(", ", o.OrderItems.Select(i => i.ProductName ?? "Sản phẩm")),
                PaymentMethod = o.PaymentMethod ?? "N/A",
                Amount = o.FinalAmount,
                PaymentDate = o.PaymentDate,
                Source = "Đơn hàng"
            }).ToList();

            // Invoices in range
            var invoicesQuery = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.ServiceHistory)
                .Where(i => i.PaymentStatus == AppConstants.PaymentStatus.Paid)
                .Where(i => (i.PaymentDate ?? i.InvoiceDate).Date >= filterStart.Date && (i.PaymentDate ?? i.InvoiceDate).Date <= filterEnd.Date)
                .Select(i => new
                {
                    i.InvoiceCode,
                    i.Customer,
                    i.ServiceHistory,
                    i.PaymentMethod,
                    TotalAmount = i.TotalAmount,
                    PaymentDate = i.PaymentDate ?? i.InvoiceDate
                })
                .ToListAsync();

            var invoices = invoicesQuery.Select(i => new RevenueTransactionDto
            {
                TransactionCode = i.InvoiceCode,
                CustomerName = i.Customer.FullName,
                ItemDescription = i.ServiceHistory != null ? (i.ServiceHistory.ServiceName ?? "Dịch vụ") : "Dịch vụ",
                PaymentMethod = i.PaymentMethod ?? "N/A",
                Amount = i.TotalAmount,
                PaymentDate = i.PaymentDate,
                Source = "Hóa đơn"
            }).ToList();

            var allTx = orders.Concat(invoices).OrderByDescending(t => t.PaymentDate).ToList();

            var labels = new List<string>();
            var values = new List<decimal>();
            var pointDetails = new List<object>();

            if (groupByMonth)
            {
                for (var m = new DateTime(filterStart.Year, filterStart.Month, 1); m <= new DateTime(filterEnd.Year, filterEnd.Month, 1); m = m.AddMonths(1))
                {
                    var monthTx = allTx.Where(t => t.PaymentDate.Year == m.Year && t.PaymentDate.Month == m.Month).ToList();
                    var monthTotal = monthTx.Sum(t => t.Amount);

                    labels.Add(m.ToString("MM/yyyy"));
                    values.Add(monthTotal);

                    pointDetails.Add(new
                    {
                        dateLabel = m.ToString("MM/yyyy"),
                        totalRevenue = monthTotal,
                        transactionCount = monthTx.Count,
                        transactions = monthTx.Select(t => new
                        {
                            t.TransactionCode,
                            t.CustomerName,
                            t.ItemDescription,
                            t.PaymentMethod,
                            t.Amount,
                            paymentDate = t.PaymentDate.ToString("dd/MM/yyyy HH:mm"),
                            t.Source
                        }).ToList()
                    });
                }
            }
            else
            {
                for (var d = filterStart.Date; d <= filterEnd.Date; d = d.AddDays(1))
                {
                    var dayTx = allTx.Where(t => t.PaymentDate.Date == d).ToList();
                    var dayTotal = dayTx.Sum(t => t.Amount);

                    labels.Add(d.ToString("dd/MM"));
                    values.Add(dayTotal);

                    pointDetails.Add(new
                    {
                        dateLabel = d.ToString("dd/MM/yyyy"),
                        totalRevenue = dayTotal,
                        transactionCount = dayTx.Count,
                        transactions = dayTx.Select(t => new
                        {
                            t.TransactionCode,
                            t.CustomerName,
                            t.ItemDescription,
                            t.PaymentMethod,
                            t.Amount,
                            paymentDate = t.PaymentDate.ToString("dd/MM/yyyy HH:mm"),
                            t.Source
                        }).ToList()
                    });
                }
            }

            return Json(new
            {
                totalRevenue = allTx.Sum(t => t.Amount),
                productRevenue = orders.Sum(o => o.Amount),
                serviceRevenue = invoices.Sum(i => i.Amount),
                labels,
                values,
                pointDetails,
                transactions = allTx.Select(t => new
                {
                    t.TransactionCode,
                    t.CustomerName,
                    t.ItemDescription,
                    t.PaymentMethod,
                    t.Amount,
                    paymentDate = t.PaymentDate.ToString("dd/MM/yyyy HH:mm"),
                    t.Source
                }).ToList()
            });
        }
    }
}
