using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using CarServ.MVC.Services;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Controllers
{
    public class PaymentController : Controller
    {
        private readonly CarServContext _context;
        private readonly IPaymentGatewayService _paymentGatewayService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            CarServContext context,
            IPaymentGatewayService paymentGatewayService,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _paymentGatewayService = paymentGatewayService;
            _logger = logger;
        }

        // GET: Payment/CreatePaymentUrl
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CreatePaymentUrl(int orderId, string paymentMethod)
        {
            try
            {
                _logger.LogInformation("Creating payment URL for order {OrderId} with method {PaymentMethod}", orderId, paymentMethod);

                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found", orderId);
                    return RedirectToAction("PaymentResult", new { success = false, message = "Đơn hàng không tồn tại." });
                }

                // Verify that the logged-in user owns this order
                var customerIdClaim = User.FindFirst("CustomerId");
                if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId) || order.CustomerId != customerId)
                {
                    _logger.LogWarning("Unauthorized payment attempt for order {OrderId} by customer {CustomerId}", orderId, customerIdClaim?.Value);
                    return RedirectToAction("PaymentResult", new { success = false, message = "Bạn không có quyền thanh toán đơn hàng này." });
                }

                if (order.FinalAmount == null || order.FinalAmount <= 0)
                {
                    _logger.LogWarning("Invalid amount for order {OrderId}: {Amount}", orderId, order.FinalAmount);
                    return RedirectToAction("PaymentResult", new { success = false, message = "Số tiền thanh toán không hợp lệ." });
                }

                var amount = order.FinalAmount.Value;
                var orderInfo = $"Thanh toan don hang {order.OrderCode}";
                
                // Build return URL - ensure it's absolute
                var scheme = Request.Scheme;
                var host = Request.Host.Value;
                var returnUrl = $"{scheme}://{host}/payment/paymentcallback";
                
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                
                // If behind proxy, try to get real IP
                if (Request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0]?.Trim() ?? ipAddress;
                }

                _logger.LogInformation("Order info: Amount={Amount}, ReturnUrl={ReturnUrl}, IpAddress={IpAddress}", amount, returnUrl, ipAddress);

                string paymentUrl;

                switch (paymentMethod?.ToLower())
                {
                    case "vnpay":
                        _logger.LogInformation("Creating VNPay payment URL");
                        paymentUrl = _paymentGatewayService.CreateVNPayPaymentUrl(
                            orderId, amount, orderInfo, returnUrl, ipAddress);
                        _logger.LogInformation("VNPay URL created: {PaymentUrl}", paymentUrl);
                        break;
                    case "momo":
                        _logger.LogInformation("Creating Momo payment URL");
                        paymentUrl = _paymentGatewayService.CreateMomoPaymentUrl(
                            orderId, amount, orderInfo, returnUrl, ipAddress);
                        _logger.LogInformation("Momo URL created: {PaymentUrl}", paymentUrl);
                        break;
                    default:
                        _logger.LogWarning("Unsupported payment method: {PaymentMethod}", paymentMethod);
                        return RedirectToAction("PaymentResult", new { success = false, message = "Phương thức thanh toán không được hỗ trợ." });
                }

                // Check if payment record already exists (avoid duplicate)
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.OrderId == orderId && p.GatewayName == paymentMethod && p.PaymentStatus == "Chờ thanh toán");

                Payment payment;
                if (existingPayment != null)
                {
                    _logger.LogInformation("Using existing payment record {PaymentId}", existingPayment.PaymentId);
                    payment = existingPayment;
                }
                else
                {
                    // Create payment record
                    var paymentCount = await _context.Payments.CountAsync() + 1;
                    payment = new Payment
                    {
                        PaymentCode = "PAY" + DateTime.Now.ToString("yyyyMMdd") + paymentCount.ToString("D4"),
                        OrderId = orderId,
                        CustomerId = order.CustomerId,
                        Amount = amount,
                        PaymentMethod = paymentMethod,
                        PaymentStatus = "Chờ thanh toán",
                        PaymentDate = DateTime.Now,
                        GatewayName = paymentMethod,
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Created new payment record {PaymentId} with code {PaymentCode}", payment.PaymentId, payment.PaymentCode);
                }

                // Redirect to payment gateway
                _logger.LogInformation("Redirecting to payment gateway: {PaymentUrl}", paymentUrl);
                return Redirect(paymentUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment URL for order {OrderId}", orderId);
                return RedirectToAction("PaymentResult", new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        // GET: Payment/PaymentCallback
        [HttpGet]
        public async Task<IActionResult> PaymentCallback()
        {
            try
            {
                // Check if this is VNPay callback
                if (Request.Query.ContainsKey("vnp_TxnRef"))
                {
                    return await HandleVNPayCallback();
                }

                // Check if this is Momo callback
                if (Request.Query.ContainsKey("partnerCode"))
                {
                    return await HandleMomoCallback();
                }

                return RedirectToAction("PaymentResult", new { success = false, message = "Thông tin thanh toán không hợp lệ." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment callback");
                return RedirectToAction("PaymentResult", new { success = false, message = "Có lỗi xảy ra khi xử lý thanh toán." });
            }
        }

        // POST: Payment/PaymentCallback (for IPN)
        [HttpPost]
        public async Task<IActionResult> PaymentCallbackPost()
        {
            try
            {
                // Handle IPN (Instant Payment Notification) from payment gateways
                // This is typically called by the gateway server-to-server

                if (Request.Form.ContainsKey("vnp_TxnRef"))
                {
                    return await HandleVNPayIPN();
                }

                if (Request.Form.ContainsKey("partnerCode"))
                {
                    return await HandleMomoIPN();
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment IPN");
                return StatusCode(500);
            }
        }

        private async Task<IActionResult> HandleVNPayCallback()
        {
            var vnpParams = new Dictionary<string, string>();

            foreach (var key in Request.Query.Keys)
            {
                if (key.StartsWith("vnp_"))
                {
                    vnpParams.Add(key, Request.Query[key].ToString());
                }
            }

            // Validate required parameters
            if (!Request.Query.ContainsKey("vnp_SecureHash") || 
                !Request.Query.ContainsKey("vnp_TxnRef") || 
                !Request.Query.ContainsKey("vnp_ResponseCode") || 
                !Request.Query.ContainsKey("vnp_TransactionStatus"))
            {
                _logger.LogWarning("Missing required VNPay callback parameters");
                return RedirectToAction("PaymentResult", new { success = false, message = "Thông tin thanh toán không đầy đủ." });
            }

            var vnpSecureHash = Request.Query["vnp_SecureHash"].ToString();
            var vnpTxnRef = Request.Query["vnp_TxnRef"].ToString();
            var vnpResponseCode = Request.Query["vnp_ResponseCode"].ToString();
            var vnpTransactionStatus = Request.Query["vnp_TransactionStatus"].ToString();

            if (!int.TryParse(vnpTxnRef, out int orderId))
            {
                _logger.LogWarning("Invalid order ID in VNPay callback: {TxnRef}", vnpTxnRef);
                return RedirectToAction("PaymentResult", new { success = false, message = "Mã đơn hàng không hợp lệ." });
            }

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return RedirectToAction("PaymentResult", new { success = false, message = "Đơn hàng không tồn tại." });
            }

            // Verify signature
            var isValid = _paymentGatewayService.VerifyVNPayCallback(vnpParams, vnpSecureHash);

            if (!isValid)
            {
                _logger.LogWarning("Invalid VNPay signature for order {OrderId}", orderId);
                return RedirectToAction("PaymentResult", new { success = false, message = "Chữ ký thanh toán không hợp lệ." });
            }

            // Find payment record
            var payment = await _context.Payments
                .Where(p => p.OrderId == orderId && p.GatewayName == "VNPay")
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();

            if (payment == null)
            {
                return RedirectToAction("PaymentResult", new { success = false, message = "Không tìm thấy thông tin thanh toán." });
            }

            // Update payment status
            if (vnpResponseCode == "00" && vnpTransactionStatus == "00")
            {
                payment.PaymentStatus = "Đã thanh toán";
                payment.CompletedDate = DateTime.Now;
                var transactionNo = Request.Query.ContainsKey("vnp_TransactionNo") 
                    ? Request.Query["vnp_TransactionNo"].ToString() 
                    : null;
                payment.TransactionCode = transactionNo;
                payment.GatewayTransactionId = transactionNo;
                payment.GatewayResponse = string.Join("&", vnpParams.Select(p => $"{p.Key}={p.Value}"));

                // Update order
                order.PaymentStatus = "Đã thanh toán";
                order.PaymentDate = DateTime.Now;
                order.TransactionCode = payment.TransactionCode;

                await _context.SaveChangesAsync();

                return RedirectToAction("PaymentResult", new { success = true, orderCode = order.OrderCode });
            }
            else
            {
                payment.PaymentStatus = "Thất bại";
                payment.GatewayResponse = $"ResponseCode: {vnpResponseCode}, TransactionStatus: {vnpTransactionStatus}";
                await _context.SaveChangesAsync();

                return RedirectToAction("PaymentResult", new { success = false, message = "Thanh toán thất bại." });
            }
        }

        private async Task<IActionResult> HandleMomoCallback()
        {
            // Momo callback handling
            // This is a simplified version - actual implementation depends on Momo's API
            var orderIdStr = Request.Query["orderId"].ToString();
            
            if (!int.TryParse(orderIdStr, out int orderId))
            {
                return RedirectToAction("PaymentResult", new { success = false, message = "Mã đơn hàng không hợp lệ." });
            }

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return RedirectToAction("PaymentResult", new { success = false, message = "Đơn hàng không tồn tại." });
            }

            var resultCode = Request.Query["resultCode"].ToString();
            var payment = await _context.Payments
                .Where(p => p.OrderId == orderId && p.GatewayName == "Momo")
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();

            if (payment == null)
            {
                return RedirectToAction("PaymentResult", new { success = false, message = "Không tìm thấy thông tin thanh toán." });
            }

            if (resultCode == "0")
            {
                payment.PaymentStatus = "Đã thanh toán";
                payment.CompletedDate = DateTime.Now;
                var transId = Request.Query["transId"].ToString();
                payment.TransactionCode = !string.IsNullOrEmpty(transId) ? transId : null;
                payment.GatewayTransactionId = !string.IsNullOrEmpty(transId) ? transId : null;

                order.PaymentStatus = "Đã thanh toán";
                order.PaymentDate = DateTime.Now;
                order.TransactionCode = payment.TransactionCode;

                await _context.SaveChangesAsync();

                return RedirectToAction("PaymentResult", new { success = true, orderCode = order.OrderCode });
            }
            else
            {
                payment.PaymentStatus = "Thất bại";
                await _context.SaveChangesAsync();

                return RedirectToAction("PaymentResult", new { success = false, message = "Thanh toán thất bại." });
            }
        }

        private async Task<IActionResult> HandleVNPayIPN()
        {
            try
            {
                // Handle IPN from VNPay (server-to-server notification)
                // Similar to callback but return status code instead of redirect
                var vnpParams = new Dictionary<string, string>();

                foreach (var key in Request.Form.Keys)
                {
                    if (key.StartsWith("vnp_"))
                    {
                        vnpParams.Add(key, Request.Form[key].ToString());
                    }
                }

                // Validate required parameters
                if (!Request.Form.ContainsKey("vnp_SecureHash") || 
                    !Request.Form.ContainsKey("vnp_TxnRef") || 
                    !Request.Form.ContainsKey("vnp_ResponseCode"))
                {
                    _logger.LogWarning("Missing required VNPay IPN parameters");
                    return BadRequest();
                }

                var vnpSecureHash = Request.Form["vnp_SecureHash"].ToString();
                var isValid = _paymentGatewayService.VerifyVNPayCallback(vnpParams, vnpSecureHash);

                if (isValid && int.TryParse(Request.Form["vnp_TxnRef"].ToString(), out int orderId))
                {
                    var payment = await _context.Payments
                        .Where(p => p.OrderId == orderId && p.GatewayName == "VNPay")
                        .OrderByDescending(p => p.PaymentDate)
                        .FirstOrDefaultAsync();

                    if (payment != null && Request.Form["vnp_ResponseCode"].ToString() == "00")
                    {
                        payment.PaymentStatus = "Đã thanh toán";
                        payment.CompletedDate = DateTime.Now;
                        var transactionNo = Request.Form.ContainsKey("vnp_TransactionNo") 
                            ? Request.Form["vnp_TransactionNo"].ToString() 
                            : null;
                        payment.TransactionCode = transactionNo;
                        payment.GatewayTransactionId = transactionNo;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("VNPay IPN processed successfully for order {OrderId}", orderId);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing VNPay IPN");
                return StatusCode(500);
            }
        }

        private Task<IActionResult> HandleMomoIPN()
        {
            // Handle IPN from Momo
            // Similar implementation to callback
            return Task.FromResult<IActionResult>(Ok());
        }

        // GET: Payment/PaymentResult
        [HttpGet]
        public IActionResult PaymentResult(bool success, string? message = null, string? orderCode = null)
        {
            ViewBag.Success = success;
            ViewBag.Message = message;
            ViewBag.OrderCode = orderCode;
            return View();
        }

        // GET: Payment/TestPaymentUrl (for debugging)
        [HttpGet]
        public async Task<IActionResult> TestPaymentUrl(int orderId = 1, string paymentMethod = "VNPay")
        {
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return Content($"Order {orderId} not found");
                }

                var amount = order.FinalAmount ?? 100000;
                var orderInfo = $"Test payment for order {order.OrderCode}";
                var returnUrl = $"{Request.Scheme}://{Request.Host}/payment/paymentcallback";
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                string paymentUrl;
                if (paymentMethod == "VNPay")
                {
                    paymentUrl = _paymentGatewayService.CreateVNPayPaymentUrl(orderId, amount, orderInfo, returnUrl, ipAddress);
                }
                else
                {
                    paymentUrl = _paymentGatewayService.CreateMomoPaymentUrl(orderId, amount, orderInfo, returnUrl, ipAddress);
                }

                return Content($"<h2>Payment URL Test</h2>" +
                    $"<p><strong>Order ID:</strong> {orderId}</p>" +
                    $"<p><strong>Amount:</strong> {amount:N0} đ</p>" +
                    $"<p><strong>Return URL:</strong> {returnUrl}</p>" +
                    $"<p><strong>Payment URL:</strong></p>" +
                    $"<p><a href='{paymentUrl}' target='_blank'>{paymentUrl}</a></p>" +
                    $"<p><a href='{paymentUrl}' class='btn btn-primary' target='_blank'>Test Payment</a></p>",
                    "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<h2>Error</h2><p>{ex.Message}</p><pre>{ex.StackTrace}</pre>", "text/html");
            }
        }
    }
}

