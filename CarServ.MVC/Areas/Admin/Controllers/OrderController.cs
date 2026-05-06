using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class OrderController : Controller
    {
        private readonly CarServContext _context;

        public OrderController(CarServContext context)
        {
            _context = context;
        }

        // GET: Admin/Order
        public async Task<IActionResult> Index(string searchString, string statusFilter, string paymentStatusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentPaymentStatus"] = paymentStatusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["DateSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_desc" : "";

            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                orders = orders.Where(o => 
                    (o.OrderCode != null && o.OrderCode.Contains(searchString))
                    || (o.Customer != null && o.Customer.FullName.Contains(searchString))
                    || (o.Customer != null && o.Customer.Phone != null && o.Customer.Phone.Contains(searchString))
                    || (o.Customer != null && o.Customer.Email != null && o.Customer.Email.Contains(searchString))
                    || (o.TransactionCode != null && o.TransactionCode.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                orders = orders.Where(o => o.Status == statusFilter);
            }

            // Filter by payment status
            if (!string.IsNullOrEmpty(paymentStatusFilter))
            {
                orders = orders.Where(o => o.PaymentStatus == paymentStatusFilter);
            }

            // Sort
            switch (sortOrder)
            {
                case "date_desc":
                    orders = orders.OrderByDescending(o => o.OrderDate);
                    break;
                case "amount_desc":
                    orders = orders.OrderByDescending(o => o.FinalAmount);
                    break;
                case "amount":
                    orders = orders.OrderBy(o => o.FinalAmount);
                    break;
                default:
                    orders = orders.OrderByDescending(o => o.OrderDate);
                    break;
            }

            var orderList = await orders.ToListAsync();

            // Get status list for filter
            var statusList = await _context.Orders
                .Where(o => o.Status != null)
                .Select(o => o.Status)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            ViewBag.StatusList = statusList;

            // Get payment status list for filter
            var paymentStatusList = await _context.Orders
                .Where(o => o.PaymentStatus != null)
                .Select(o => o.PaymentStatus)
                .Distinct()
                .OrderBy(ps => ps)
                .ToListAsync();

            ViewBag.PaymentStatusList = paymentStatusList;

            return View(orderList);
        }

        // GET: Admin/Order/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            // Load payment history for this order
            var payments = await _context.Payments
                .Where(p => p.OrderId == id)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            ViewData["Payments"] = payments;
            ViewData["TotalPaid"] = payments.Where(p => p.PaymentStatus == "Đã thanh toán").Sum(p => p.Amount);

            return View(order);
        }

        // GET: Admin/Order/Create
        public async Task<IActionResult> Create()
        {
            // Get customers for dropdown
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            // Get products for order items
            var products = await _context.Products
                .Where(p => p.IsActive == true)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ViewBag.Products = products.Select(p => new SelectListItem 
            { 
                Value = p.ProductId.ToString(), 
                Text = $"{p.ProductName} - {p.Price?.ToString("N0")} đ" 
            }).ToList();

            // Status options
            var statusList = new[] { "Chờ xử lý", "Đang xử lý", "Đang giao hàng", "Đã giao hàng", "Đã hủy", "Hoàn trả" };
            ViewBag.StatusList = statusList;

            // Payment status options
            var paymentStatusList = new[] { "Chưa thanh toán", "Đã thanh toán", "Thanh toán một phần", "Hoàn tiền" };
            ViewBag.PaymentStatusList = paymentStatusList;

            // Payment method options
            var paymentMethodList = new[] { "Tiền mặt", "Chuyển khoản", "Thẻ tín dụng", "Ví điện tử", "COD" };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View();
        }

        // POST: Admin/Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerId,TotalAmount,ShippingFee,DiscountAmount,DiscountCode,FinalAmount,Status,PaymentMethod,PaymentStatus,ShippingAddress,ShippingCity,ShippingDistrict,ShippingWard,CustomerNotes,AdminNotes")] Order order)
        {
            if (ModelState.IsValid)
            {
                // Generate order code
                order.OrderCode = GenerateOrderCode();
                order.OrderDate = DateTime.Now;
                order.UpdatedDate = DateTime.Now;

                if (string.IsNullOrEmpty(order.Status))
                {
                    order.Status = "Chờ xử lý";
                }

                if (string.IsNullOrEmpty(order.PaymentStatus))
                {
                    order.PaymentStatus = "Chưa thanh toán";
                }

                _context.Add(order);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Reload dropdowns on error
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            var products = await _context.Products
                .Where(p => p.IsActive == true)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            ViewBag.Products = products.Select(p => new SelectListItem 
            { 
                Value = p.ProductId.ToString(), 
                Text = $"{p.ProductName} - {p.Price?.ToString("N0")} đ" 
            }).ToList();

            var statusList = new[] { "Chờ xử lý", "Đang xử lý", "Đang giao hàng", "Đã giao hàng", "Đã hủy", "Hoàn trả" };
            ViewBag.StatusList = statusList;

            var paymentStatusList = new[] { "Chưa thanh toán", "Đã thanh toán", "Thanh toán một phần", "Hoàn tiền" };
            ViewBag.PaymentStatusList = paymentStatusList;

            var paymentMethodList = new[] { "Tiền mặt", "Chuyển khoản", "Thẻ tín dụng", "Ví điện tử", "COD" };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View(order);
        }

        // GET: Admin/Order/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            // Get customers for dropdown
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            // Status options
            var statusList = new[] { "Chờ xử lý", "Đang xử lý", "Đang giao hàng", "Đã giao hàng", "Đã hủy", "Hoàn trả" };
            ViewBag.StatusList = statusList;

            // Payment status options
            var paymentStatusList = new[] { "Chưa thanh toán", "Đã thanh toán", "Thanh toán một phần", "Hoàn tiền" };
            ViewBag.PaymentStatusList = paymentStatusList;

            // Payment method options
            var paymentMethodList = new[] { "Tiền mặt", "Chuyển khoản", "Thẻ tín dụng", "Ví điện tử", "COD" };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View(order);
        }

        // POST: Admin/Order/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OrderId,OrderCode,CustomerId,TotalAmount,ShippingFee,DiscountAmount,DiscountCode,FinalAmount,Status,PaymentMethod,PaymentStatus,PaymentDate,TransactionCode,ShippingAddress,ShippingCity,ShippingDistrict,ShippingWard,CustomerNotes,AdminNotes,OrderDate")] Order order)
        {
            if (id != order.OrderId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingOrder = await _context.Orders.FindAsync(id);
                    if (existingOrder == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingOrder.CustomerId = order.CustomerId;
                    existingOrder.TotalAmount = order.TotalAmount;
                    existingOrder.ShippingFee = order.ShippingFee;
                    existingOrder.DiscountAmount = order.DiscountAmount;
                    existingOrder.DiscountCode = order.DiscountCode;
                    existingOrder.FinalAmount = order.FinalAmount;
                    existingOrder.Status = order.Status;
                    existingOrder.PaymentMethod = order.PaymentMethod;
                    existingOrder.PaymentStatus = order.PaymentStatus;
                    existingOrder.PaymentDate = order.PaymentDate;
                    existingOrder.TransactionCode = order.TransactionCode;
                    existingOrder.ShippingAddress = order.ShippingAddress;
                    existingOrder.ShippingCity = order.ShippingCity;
                    existingOrder.ShippingDistrict = order.ShippingDistrict;
                    existingOrder.ShippingWard = order.ShippingWard;
                    existingOrder.CustomerNotes = order.CustomerNotes;
                    existingOrder.AdminNotes = order.AdminNotes;
                    existingOrder.UpdatedDate = DateTime.Now;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.OrderId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            // Reload dropdowns on error
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            var statusList = new[] { "Chờ xử lý", "Đang xử lý", "Đang giao hàng", "Đã giao hàng", "Đã hủy", "Hoàn trả" };
            ViewBag.StatusList = statusList;

            var paymentStatusList = new[] { "Chưa thanh toán", "Đã thanh toán", "Thanh toán một phần", "Hoàn tiền" };
            ViewBag.PaymentStatusList = paymentStatusList;

            var paymentMethodList = new[] { "Tiền mặt", "Chuyển khoản", "Thẻ tín dụng", "Ví điện tử", "COD" };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View(order);
        }

        // GET: Admin/Order/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Admin/Order/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order != null)
            {
                // Delete order items first
                _context.OrderItems.RemoveRange(order.OrderItems);
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderId == id);
        }

        private string GenerateOrderCode()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var count = _context.Orders.Count(o => o.OrderDate.HasValue && o.OrderDate.Value.Date == DateTime.Now.Date) + 1;
            return $"ORD{date}{count:D4}";
        }
    }
}


