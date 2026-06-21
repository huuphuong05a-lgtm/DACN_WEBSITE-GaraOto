using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
    public class PaymentController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(CarServContext context, ILogger<PaymentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin/Payment
        public async Task<IActionResult> Index(string searchString, string statusFilter, string methodFilter, int? orderId, int? invoiceId, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentMethod"] = methodFilter;
            ViewData["CurrentOrderId"] = orderId;
            ViewData["CurrentInvoiceId"] = invoiceId;
            ViewData["CurrentSort"] = sortOrder;

            var payments = _context.Payments
                .Include(p => p.Customer)
                .Include(p => p.Order)
                .Include(p => p.Invoice)
                .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                payments = payments.Where(p => 
                    p.PaymentCode.Contains(searchString) ||
                    (p.TransactionCode != null && p.TransactionCode.Contains(searchString)) ||
                    (p.Customer != null && p.Customer.FullName.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                payments = payments.Where(p => p.PaymentStatus == statusFilter);
            }

            // Filter by method
            if (!string.IsNullOrEmpty(methodFilter))
            {
                payments = payments.Where(p => p.PaymentMethod == methodFilter);
            }

            // Filter by order
            if (orderId.HasValue)
            {
                payments = payments.Where(p => p.OrderId == orderId);
            }

            // Filter by invoice
            if (invoiceId.HasValue)
            {
                payments = payments.Where(p => p.InvoiceId == invoiceId);
            }

            // Sort
            switch (sortOrder)
            {
                case "date_desc":
                    payments = payments.OrderByDescending(p => p.PaymentDate);
                    break;
                case "amount":
                    payments = payments.OrderBy(p => p.Amount);
                    break;
                case "amount_desc":
                    payments = payments.OrderByDescending(p => p.Amount);
                    break;
                default:
                    payments = payments.OrderByDescending(p => p.PaymentDate);
                    break;
            }

            var paymentList = await payments.ToListAsync();

            // Get filter options
            var statusList = await _context.Payments
                .Where(p => p.PaymentStatus != null)
                .Select(p => p.PaymentStatus)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            var methodList = await _context.Payments
                .Where(p => p.PaymentMethod != null)
                .Select(p => p.PaymentMethod)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            ViewBag.StatusList = statusList;
            ViewBag.MethodList = methodList;

            return View(paymentList);
        }

        // GET: Admin/Payment/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Customer)
                .Include(p => p.Order)
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(m => m.PaymentId == id);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // GET: Admin/Payment/Create
        public async Task<IActionResult> Create(int? orderId, int? invoiceId)
        {
            ViewBag.OrderId = orderId;
            ViewBag.InvoiceId = invoiceId;

            // Get customers
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem
            {
                Value = c.CustomerId.ToString(),
                Text = $"{c.FullName} - {c.Phone}"
            }).ToList();

            // Get orders
            var orders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Orders = orders.Select(o => new SelectListItem
            {
                Value = o.OrderId.ToString(),
                Text = $"{o.OrderCode} - {o.FinalAmount?.ToString("N0")} Ä‘"
            }).ToList();

            // Get invoices
            var invoices = await _context.Invoices
                .OrderByDescending(i => i.InvoiceDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Invoices = invoices.Select(i => new SelectListItem
            {
                Value = i.InvoiceId.ToString(),
                Text = $"{i.InvoiceCode} - {i.TotalAmount.ToString("N0")} Ä‘"
            }).ToList();

            // Payment status options
            var paymentStatusList = new[] { AppConstants.PaymentStatus.Pending, AppConstants.PaymentStatus.Paid, AppConstants.PaymentStatus.Failed, AppConstants.PaymentStatus.Canceled, AppConstants.PaymentStatus.Refunded };
            ViewBag.PaymentStatusList = paymentStatusList;

            // Payment method options
            var paymentMethodList = new[] { AppConstants.PaymentMethod.Cash, AppConstants.PaymentMethod.BankTransfer, AppConstants.PaymentMethod.CreditCard, AppConstants.PaymentMethod.EWallet, AppConstants.PaymentMethod.VNPay, AppConstants.PaymentMethod.COD };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordInvoicePayment(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.ServiceHistory)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
            {
                return NotFound();
            }

            var appointmentId = invoice.ServiceHistory?.AppointmentId;

            if (invoice.PaymentStatus == AppConstants.PaymentStatus.Paid)
            {
                TempData["SuccessMessage"] = "Hóa đơn này đã được ghi nhận thanh toán.";
                return RedirectAfterInvoicePayment(appointmentId);
            }

            var now = DateTime.Now;
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.InvoiceId == invoice.InvoiceId);

            if (payment == null)
            {
                var count = await _context.Payments.CountAsync() + 1;
                payment = new Payment
                {
                    PaymentCode = "PAY" + now.ToString("yyyyMMdd") + count.ToString("D4"),
                    InvoiceId = invoice.InvoiceId,
                    CustomerId = invoice.CustomerId,
                    Amount = invoice.TotalAmount,
                    PaymentMethod = AppConstants.PaymentMethod.Cash,
                    PaymentStatus = AppConstants.PaymentStatus.Paid,
                    PaymentDate = now,
                    CompletedDate = now,
                    Notes = "Ghi nhận thanh toán tiền mặt từ chi tiết lịch hẹn",
                    CreatedBy = User.Identity?.Name,
                    CreatedDate = now,
                    UpdatedDate = now
                };
                _context.Payments.Add(payment);
            }
            else
            {
                payment.CustomerId = invoice.CustomerId;
                payment.Amount = invoice.TotalAmount;
                payment.PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMethod)
                    ? AppConstants.PaymentMethod.Cash
                    : payment.PaymentMethod;
                payment.PaymentStatus = AppConstants.PaymentStatus.Paid;
                payment.PaymentDate = payment.PaymentDate == default ? now : payment.PaymentDate;
                payment.CompletedDate ??= now;
                payment.UpdatedDate = now;
            }

            invoice.PaymentStatus = AppConstants.PaymentStatus.Paid;
            invoice.PaymentDate = now;
            invoice.PaymentMethod = string.IsNullOrWhiteSpace(invoice.PaymentMethod)
                ? AppConstants.PaymentMethod.Cash
                : invoice.PaymentMethod;
            invoice.Status = AppConstants.PaymentStatus.Paid;
            invoice.UpdatedDate = now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã ghi nhận thanh toán thành công.";
            return RedirectAfterInvoicePayment(appointmentId);
        }

        // POST: Admin/Payment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("OrderId,InvoiceId,CustomerId,Amount,PaymentMethod,PaymentStatus,TransactionCode,GatewayTransactionId,GatewayName,GatewayResponse,PaymentDate,CompletedDate,Notes")] Payment payment)
        {
            if (ModelState.IsValid)
            {
                // Generate payment code
                var count = await _context.Payments.CountAsync() + 1;
                payment.PaymentCode = "PAY" + DateTime.Now.ToString("yyyyMMdd") + count.ToString("D4");

                payment.CreatedDate = DateTime.Now;
                payment.UpdatedDate = DateTime.Now;

                if (string.IsNullOrEmpty(payment.PaymentStatus))
                {
                    payment.PaymentStatus = AppConstants.PaymentStatus.Pending;
                }

                _context.Add(payment);
                await _context.SaveChangesAsync();

                // Update order payment status if linked
                if (payment.OrderId.HasValue)
                {
                    var order = await _context.Orders.FindAsync(payment.OrderId.Value);
                    if (order != null)
                    {
                        // Calculate total paid amount
                        var totalPaid = await _context.Payments
                            .Where(p => p.OrderId == payment.OrderId && p.PaymentStatus == AppConstants.PaymentStatus.Paid)
                            .SumAsync(p => p.Amount);

                        if (totalPaid >= (order.FinalAmount ?? 0))
                        {
                            order.PaymentStatus = AppConstants.PaymentStatus.Paid;
                        }
                        else if (totalPaid > 0)
                        {
                            order.PaymentStatus = AppConstants.PaymentStatus.Partial;
                        }

                        order.PaymentDate = payment.PaymentDate;
                        order.TransactionCode = payment.TransactionCode;
                        await _context.SaveChangesAsync();
                    }
                }

                // Update invoice payment status if linked
                if (payment.InvoiceId.HasValue)
                {
                    var invoice = await _context.Invoices.FindAsync(payment.InvoiceId.Value);
                    if (invoice != null)
                    {
                        var totalPaid = await _context.Payments
                            .Where(p => p.InvoiceId == payment.InvoiceId && p.PaymentStatus == AppConstants.PaymentStatus.Paid)
                            .SumAsync(p => p.Amount);

                        if (totalPaid >= invoice.TotalAmount)
                        {
                            invoice.PaymentStatus = AppConstants.PaymentStatus.Paid;
                        }
                        else if (totalPaid > 0)
                        {
                            invoice.PaymentStatus = AppConstants.PaymentStatus.Partial;
                        }

                        invoice.PaymentDate = payment.PaymentDate;
                        invoice.TransactionCode = payment.TransactionCode;
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["SuccessMessage"] = $"ÄÃ£ táº¡o thanh toÃ¡n thÃ nh cÃ´ng! MÃ£ thanh toÃ¡n: {payment.PaymentCode}";
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

            var orders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Orders = orders.Select(o => new SelectListItem
            {
                Value = o.OrderId.ToString(),
                Text = $"{o.OrderCode} - {o.FinalAmount?.ToString("N0")} Ä‘"
            }).ToList();

            var invoices = await _context.Invoices
                .OrderByDescending(i => i.InvoiceDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Invoices = invoices.Select(i => new SelectListItem
            {
                Value = i.InvoiceId.ToString(),
                Text = $"{i.InvoiceCode} - {i.TotalAmount.ToString("N0")} Ä‘"
            }).ToList();

            var paymentStatusList = new[] { AppConstants.PaymentStatus.Pending, AppConstants.PaymentStatus.Paid, AppConstants.PaymentStatus.Failed, AppConstants.PaymentStatus.Canceled, AppConstants.PaymentStatus.Refunded };
            ViewBag.PaymentStatusList = paymentStatusList;

            var paymentMethodList = new[] { AppConstants.PaymentMethod.Cash, AppConstants.PaymentMethod.BankTransfer, AppConstants.PaymentMethod.CreditCard, AppConstants.PaymentMethod.EWallet, AppConstants.PaymentMethod.VNPay, AppConstants.PaymentMethod.COD };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View(payment);
        }

        // GET: Admin/Payment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                return NotFound();
            }

            // Get customers
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem
            {
                Value = c.CustomerId.ToString(),
                Text = $"{c.FullName} - {c.Phone}"
            }).ToList();

            // Get orders
            var orders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Orders = orders.Select(o => new SelectListItem
            {
                Value = o.OrderId.ToString(),
                Text = $"{o.OrderCode} - {o.FinalAmount?.ToString("N0")} Ä‘"
            }).ToList();

            // Get invoices
            var invoices = await _context.Invoices
                .OrderByDescending(i => i.InvoiceDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Invoices = invoices.Select(i => new SelectListItem
            {
                Value = i.InvoiceId.ToString(),
                Text = $"{i.InvoiceCode} - {i.TotalAmount.ToString("N0")} Ä‘"
            }).ToList();

            var paymentStatusList = new[] { AppConstants.PaymentStatus.Pending, AppConstants.PaymentStatus.Paid, AppConstants.PaymentStatus.Failed, AppConstants.PaymentStatus.Canceled, AppConstants.PaymentStatus.Refunded };
            ViewBag.PaymentStatusList = paymentStatusList;

            var paymentMethodList = new[] { AppConstants.PaymentMethod.Cash, AppConstants.PaymentMethod.BankTransfer, AppConstants.PaymentMethod.CreditCard, AppConstants.PaymentMethod.EWallet, AppConstants.PaymentMethod.VNPay, AppConstants.PaymentMethod.COD };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View(payment);
        }

        // POST: Admin/Payment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PaymentId,PaymentCode,OrderId,InvoiceId,CustomerId,Amount,PaymentMethod,PaymentStatus,TransactionCode,GatewayTransactionId,GatewayName,GatewayResponse,PaymentDate,CompletedDate,Notes,CreatedDate")] Payment payment)
        {
            if (id != payment.PaymentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    payment.UpdatedDate = DateTime.Now;

                    if (payment.PaymentStatus == AppConstants.PaymentStatus.Paid && payment.CompletedDate == null)
                    {
                        payment.CompletedDate = DateTime.Now;
                    }

                    _context.Update(payment);
                    await _context.SaveChangesAsync();

                    // Update order/invoice payment status
                    if (payment.OrderId.HasValue)
                    {
                        var order = await _context.Orders.FindAsync(payment.OrderId.Value);
                        if (order != null)
                        {
                            var totalPaid = await _context.Payments
                                .Where(p => p.OrderId == payment.OrderId && p.PaymentStatus == AppConstants.PaymentStatus.Paid)
                                .SumAsync(p => p.Amount);

                            if (totalPaid >= (order.FinalAmount ?? 0))
                            {
                                order.PaymentStatus = AppConstants.PaymentStatus.Paid;
                            }
                            else if (totalPaid > 0)
                            {
                                order.PaymentStatus = AppConstants.PaymentStatus.Partial;
                            }
                            else
                            {
                                order.PaymentStatus = AppConstants.PaymentStatus.Unpaid;
                            }

                            await _context.SaveChangesAsync();
                        }
                    }

                    if (payment.InvoiceId.HasValue)
                    {
                        var invoice = await _context.Invoices.FindAsync(payment.InvoiceId.Value);
                        if (invoice != null)
                        {
                            var totalPaid = await _context.Payments
                                .Where(p => p.InvoiceId == payment.InvoiceId && p.PaymentStatus == AppConstants.PaymentStatus.Paid)
                                .SumAsync(p => p.Amount);

                            if (totalPaid >= invoice.TotalAmount)
                            {
                                invoice.PaymentStatus = AppConstants.PaymentStatus.Paid;
                            }
                            else if (totalPaid > 0)
                            {
                                invoice.PaymentStatus = AppConstants.PaymentStatus.Partial;
                            }
                            else
                            {
                                invoice.PaymentStatus = AppConstants.PaymentStatus.Unpaid;
                            }

                            await _context.SaveChangesAsync();
                        }
                    }

                    TempData["SuccessMessage"] = "ÄÃ£ cáº­p nháº­t thanh toÃ¡n thÃ nh cÃ´ng!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentExists(payment.PaymentId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
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

            var orders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Orders = orders.Select(o => new SelectListItem
            {
                Value = o.OrderId.ToString(),
                Text = $"{o.OrderCode} - {o.FinalAmount?.ToString("N0")} Ä‘"
            }).ToList();

            var invoices = await _context.Invoices
                .OrderByDescending(i => i.InvoiceDate)
                .Take(100)
                .ToListAsync();

            ViewBag.Invoices = invoices.Select(i => new SelectListItem
            {
                Value = i.InvoiceId.ToString(),
                Text = $"{i.InvoiceCode} - {i.TotalAmount.ToString("N0")} Ä‘"
            }).ToList();

            var paymentStatusList = new[] { AppConstants.PaymentStatus.Pending, AppConstants.PaymentStatus.Paid, AppConstants.PaymentStatus.Failed, AppConstants.PaymentStatus.Canceled, AppConstants.PaymentStatus.Refunded };
            ViewBag.PaymentStatusList = paymentStatusList;

            var paymentMethodList = new[] { AppConstants.PaymentMethod.Cash, AppConstants.PaymentMethod.BankTransfer, AppConstants.PaymentMethod.CreditCard, AppConstants.PaymentMethod.EWallet, AppConstants.PaymentMethod.VNPay, AppConstants.PaymentMethod.COD };
            ViewBag.PaymentMethodList = paymentMethodList;

            return View(payment);
        }

        // GET: Admin/Payment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments
                .Include(p => p.Customer)
                .Include(p => p.Order)
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(m => m.PaymentId == id);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // POST: Admin/Payment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment != null)
            {
                var orderId = payment.OrderId;
                var invoiceId = payment.InvoiceId;

                _context.Payments.Remove(payment);
                await _context.SaveChangesAsync();

                // Update order/invoice payment status after deletion
                if (orderId.HasValue)
                {
                    var order = await _context.Orders.FindAsync(orderId.Value);
                    if (order != null)
                    {
                        var totalPaid = await _context.Payments
                            .Where(p => p.OrderId == orderId && p.PaymentStatus == AppConstants.PaymentStatus.Paid)
                            .SumAsync(p => p.Amount);

                        if (totalPaid >= (order.FinalAmount ?? 0))
                        {
                            order.PaymentStatus = AppConstants.PaymentStatus.Paid;
                        }
                        else if (totalPaid > 0)
                        {
                            order.PaymentStatus = AppConstants.PaymentStatus.Partial;
                        }
                        else
                        {
                            order.PaymentStatus = AppConstants.PaymentStatus.Unpaid;
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                if (invoiceId.HasValue)
                {
                    var invoice = await _context.Invoices.FindAsync(invoiceId.Value);
                    if (invoice != null)
                    {
                        var totalPaid = await _context.Payments
                            .Where(p => p.InvoiceId == invoiceId && p.PaymentStatus == AppConstants.PaymentStatus.Paid)
                            .SumAsync(p => p.Amount);

                        if (totalPaid >= invoice.TotalAmount)
                        {
                            invoice.PaymentStatus = AppConstants.PaymentStatus.Paid;
                        }
                        else if (totalPaid > 0)
                        {
                            invoice.PaymentStatus = AppConstants.PaymentStatus.Partial;
                        }
                        else
                        {
                            invoice.PaymentStatus = AppConstants.PaymentStatus.Unpaid;
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                TempData["SuccessMessage"] = "ÄÃ£ xÃ³a thanh toÃ¡n thÃ nh cÃ´ng!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PaymentExists(int id)
        {
            return _context.Payments.Any(e => e.PaymentId == id);
        }

        private IActionResult RedirectAfterInvoicePayment(int? appointmentId)
        {
            if (appointmentId.HasValue)
            {
                return RedirectToAction("Details", "Appointment", new { area = "Admin", id = appointmentId.Value });
            }

            return RedirectToAction(nameof(Index));
        }
    }
}


