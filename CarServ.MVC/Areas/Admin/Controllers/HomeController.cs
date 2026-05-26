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
            var monthlyStart = monthStart.AddMonths(-5);

            var paidOrders = await _context.Orders
                .Where(o => o.PaymentStatus == AppConstants.PaymentStatus.Paid && o.OrderDate.HasValue)
                .Select(o => new
                {
                    Date = o.PaymentDate ?? o.OrderDate!.Value,
                    Amount = o.FinalAmount ?? o.TotalAmount ?? 0
                })
                .ToListAsync();

            var paidInvoices = await _context.Invoices
                .Where(i => i.PaymentStatus == AppConstants.PaymentStatus.Paid)
                .Select(i => new
                {
                    Date = i.PaymentDate ?? i.InvoiceDate,
                    Amount = i.TotalAmount
                })
                .ToListAsync();

            var completedAppointments = await _context.Appointments
                .Include(a => a.ServiceHistories)
                    .ThenInclude(h => h.Invoices)
                .Where(a => a.Status == AppConstants.AppointmentStatus.Completed && a.TotalPrice.HasValue)
                .ToListAsync();

            var completedAppointmentRevenues = completedAppointments
                .Where(a => !a.ServiceHistories
                    .SelectMany(h => h.Invoices)
                    .Any(i => i.PaymentStatus == AppConstants.PaymentStatus.Paid))
                .Select(a => new
                {
                    Date = (a.UpdatedDate ?? a.AppointmentDate).Date,
                    Amount = a.TotalPrice ?? 0
                })
                .ToList();

            var revenues = paidOrders
                .Select(x => new { Date = x.Date.Date, x.Amount })
                .Concat(paidInvoices.Select(x => new { Date = x.Date.Date, x.Amount }))
                .Concat(completedAppointmentRevenues.Select(x => new { x.Date, x.Amount }))
                .ToList();

            var model = new AdminDashboardViewModel
            {
                TodayRevenue = revenues.Where(x => x.Date == today).Sum(x => x.Amount),
                MonthRevenue = revenues.Where(x => x.Date >= monthStart && x.Date < nextMonth).Sum(x => x.Amount),
                TotalRevenue = revenues.Sum(x => x.Amount),
                PaidOrderCount = paidOrders.Count,
                PaidInvoiceCount = paidInvoices.Count,
                PendingAppointmentCount = await _context.Appointments.CountAsync(a => a.Status == AppConstants.AppointmentStatus.Pending),
                CompletedAppointmentCount = await _context.Appointments.CountAsync(a => a.Status == AppConstants.AppointmentStatus.Completed),
                LowStockProductCount = await _context.Products.CountAsync(p => (p.StockQuantity ?? 0) <= 5)
            };

            for (var date = chartStart; date <= today; date = date.AddDays(1))
            {
                model.RevenueLabels.Add(date.ToString("dd/MM"));
                model.RevenueValues.Add(revenues.Where(x => x.Date == date).Sum(x => x.Amount));
            }

            for (var month = monthlyStart; month <= monthStart; month = month.AddMonths(1))
            {
                var end = month.AddMonths(1);
                model.MonthlyRevenueLabels.Add(month.ToString("MM/yyyy"));
                model.MonthlyRevenueValues.Add(revenues.Where(x => x.Date >= month && x.Date < end).Sum(x => x.Amount));
            }

            return View(model);
        }
    }
}
