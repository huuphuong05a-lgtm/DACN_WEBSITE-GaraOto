using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Controllers
{
    public class CartController : Controller
    {
        private readonly CarServContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartController(CarServContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Helper method to get current session ID
        private string GetSessionId()
        {
            if (string.IsNullOrEmpty(_httpContextAccessor.HttpContext?.Session.GetString("CartSessionId")))
            {
                _httpContextAccessor.HttpContext?.Session.SetString("CartSessionId", Guid.NewGuid().ToString());
            }
            return _httpContextAccessor.HttpContext?.Session.GetString("CartSessionId") ?? Guid.NewGuid().ToString();
        }

        // Helper method to get current customer ID (if logged in)
        private int? GetCurrentCustomerId()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var customerIdClaim = User.FindFirst("CustomerId");
                if (customerIdClaim != null && int.TryParse(customerIdClaim.Value, out int customerId))
                {
                    return customerId;
                }
            }
            return null; // Guest user
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var customerId = GetCurrentCustomerId();
            var sessionId = GetSessionId();

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => (customerId.HasValue && c.CustomerId == customerId) 
                    || (!customerId.HasValue && c.SessionId == sessionId))
                .ToListAsync();

            // Calculate totals
            decimal totalAmount = 0;
            int totalItems = 0;

            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    var price = item.Product.SalePrice ?? item.Product.Price ?? 0;
                    totalAmount += price * (item.Quantity ?? 1);
                    totalItems += item.Quantity ?? 1;
                }
            }

            ViewData["TotalAmount"] = totalAmount;
            ViewData["TotalItems"] = totalItems;

            return View(cartItems);
        }

        // POST: Cart/AddToCart
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã ngừng bán." });
            }

            // Check stock
            if (product.StockQuantity.HasValue && product.StockQuantity < quantity)
            {
                return Json(new { success = false, message = $"Sản phẩm chỉ còn {product.StockQuantity} sản phẩm trong kho." });
            }

            var customerId = GetCurrentCustomerId();
            var sessionId = GetSessionId();

            // Check if product already in cart
            var existingCart = await _context.Carts
                .FirstOrDefaultAsync(c => c.ProductId == productId 
                    && ((customerId.HasValue && c.CustomerId == customerId) 
                        || (!customerId.HasValue && c.SessionId == sessionId)));

            if (existingCart != null)
            {
                // Update quantity
                var newQuantity = (existingCart.Quantity ?? 0) + quantity;
                if (product.StockQuantity.HasValue && newQuantity > product.StockQuantity)
                {
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho. Tồn kho: {product.StockQuantity}" });
                }
                existingCart.Quantity = newQuantity;
            }
            else
            {
                // Add new item
                var cartItem = new Cart
                {
                    ProductId = productId,
                    CustomerId = customerId,
                    SessionId = customerId.HasValue ? null : sessionId,
                    Quantity = quantity,
                    AddedDate = DateTime.Now
                };
                _context.Carts.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            // Get cart count
            var cartCount = await _context.Carts
                .Where(c => (customerId.HasValue && c.CustomerId == customerId) 
                    || (!customerId.HasValue && c.SessionId == sessionId))
                .SumAsync(c => c.Quantity ?? 0);

            return Json(new { success = true, message = "Đã thêm vào giỏ hàng!", cartCount = cartCount });
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartId, int quantity)
        {
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Số lượng phải lớn hơn 0." });
            }

            var cartItem = await _context.Carts
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.CartId == cartId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng." });
            }

            // Check stock
            if (cartItem.Product != null && cartItem.Product.StockQuantity.HasValue && quantity > cartItem.Product.StockQuantity)
            {
                return Json(new { success = false, message = $"Số lượng vượt quá tồn kho. Tồn kho: {cartItem.Product.StockQuantity}" });
            }

            cartItem.Quantity = quantity;
            await _context.SaveChangesAsync();

            // Calculate new total
            var price = cartItem.Product?.SalePrice ?? cartItem.Product?.Price ?? 0;
            var itemTotal = price * quantity;

            var customerId = GetCurrentCustomerId();
            var sessionId = GetSessionId();

            var totalAmount = await _context.Carts
                .Include(c => c.Product)
                .Where(c => (customerId.HasValue && c.CustomerId == customerId) 
                    || (!customerId.HasValue && c.SessionId == sessionId))
                .SumAsync(c => (c.Product != null ? (c.Product.SalePrice ?? c.Product.Price ?? 0) : 0) * (c.Quantity ?? 0));

            return Json(new { success = true, itemTotal = itemTotal, totalAmount = totalAmount });
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int cartId)
        {
            var cartItem = await _context.Carts.FindAsync(cartId);
            if (cartItem == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng." });
            }

            _context.Carts.Remove(cartItem);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa sản phẩm khỏi giỏ hàng.";

            return Json(new { success = true, message = "Đã xóa sản phẩm khỏi giỏ hàng." });
        }

        // GET: Cart/Checkout
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var customerId = GetCurrentCustomerId();
            var sessionId = GetSessionId();

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => (customerId.HasValue && c.CustomerId == customerId) 
                    || (!customerId.HasValue && c.SessionId == sessionId))
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction(nameof(Index));
            }

            // Calculate totals
            decimal subtotal = 0;
            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    var price = item.Product.SalePrice ?? item.Product.Price ?? 0;
                    subtotal += price * (item.Quantity ?? 1);
                }
            }

            ViewData["Subtotal"] = subtotal;
            ViewData["ShippingFee"] = 0m; // Có thể tính phí ship sau
            ViewData["TotalAmount"] = subtotal;
            ViewData["CartItems"] = cartItems;

            return View(new CheckoutViewModel());
        }

        // POST: Cart/ProcessCheckout
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var customerId = GetCurrentCustomerId();
            var sessionId = GetSessionId();

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => (customerId.HasValue && c.CustomerId == customerId) 
                    || (!customerId.HasValue && c.SessionId == sessionId))
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction(nameof(Index));
            }

            // Calculate totals
            decimal subtotal = 0;
            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    var price = item.Product.SalePrice ?? item.Product.Price ?? 0;
                    subtotal += price * (item.Quantity ?? 1);
                }
            }

            // Validate ModelState - if invalid, re-render checkout with cart data
            if (!ModelState.IsValid)
            {
                ViewData["Subtotal"] = subtotal;
                ViewData["ShippingFee"] = 0m;
                ViewData["TotalAmount"] = subtotal;
                ViewData["CartItems"] = cartItems;
                return View("Checkout", model);
            }

            // Validate stock
            foreach (var item in cartItems)
            {
                if (item.Product != null && item.Product.StockQuantity.HasValue 
                    && item.Quantity > item.Product.StockQuantity)
                {
                    TempData["ErrorMessage"] = $"Sản phẩm '{item.Product.ProductName}' không đủ tồn kho.";
                    return RedirectToAction(nameof(Checkout));
                }
            }

            // Since [Authorize] is required, customerId should always have a value
            if (!customerId.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đặt hàng.";
                return RedirectToAction("Login", "Account");
            }

            // Determine payment method (case-insensitive check)
            var paymentMethod = model.PaymentMethod?.Trim() ?? AppConstants.PaymentMethod.COD;
            var isOnlinePayment = paymentMethod.Equals(AppConstants.PaymentMethod.VNPay, StringComparison.OrdinalIgnoreCase);

            // Create order
            var order = new Order
            {
                CustomerId = customerId,
                OrderDate = DateTime.Now,
                Status = AppConstants.OrderStatus.Pending,
                PaymentStatus = isOnlinePayment ? AppConstants.PaymentStatus.OnlinePending : AppConstants.PaymentStatus.Unpaid,
                ShippingAddress = model.ShippingAddress,
                PaymentMethod = paymentMethod,
                CustomerNotes = model.Notes,
                ShippingFee = 0,
                DiscountAmount = 0,
                FinalAmount = subtotal,
                TotalAmount = subtotal,
                UpdatedDate = DateTime.Now
            };
            
            // Generate order code
            var count = await _context.Orders.CountAsync() + 1;
            order.OrderCode = "ORD" + DateTime.Now.ToString("yyyyMMdd") + count.ToString("D4");

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Create order items and update stock
            foreach (var cartItem in cartItems)
            {
                if (cartItem.Product != null)
                {
                    var price = cartItem.Product.SalePrice ?? cartItem.Product.Price ?? 0;
                    
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = cartItem.ProductId,
                        ProductName = cartItem.Product.ProductName,
                        ProductImage = cartItem.Product.ImageUrl,
                        Quantity = cartItem.Quantity ?? 1,
                        UnitPrice = price,
                        TotalPrice = price * (cartItem.Quantity ?? 1)
                    };
                    _context.OrderItems.Add(orderItem);

                    // Update stock
                    if (cartItem.Product.StockQuantity.HasValue)
                    {
                        cartItem.Product.StockQuantity -= cartItem.Quantity ?? 1;
                    }
                }
            }

            // Clear cart
            _context.Carts.RemoveRange(cartItems);
            await _context.SaveChangesAsync();

            // If online payment (VNPay), redirect to payment gateway
            if (isOnlinePayment)
            {
                return RedirectToAction("CreatePaymentUrl", "Payment", new { orderId = order.OrderId, paymentMethod = paymentMethod });
            }

            // Create payment record for offline payment methods (COD)
            var paymentCount = await _context.Payments.CountAsync() + 1;
            var payment = new Payment
            {
                PaymentCode = "PAY" + DateTime.Now.ToString("yyyyMMdd") + paymentCount.ToString("D4"),
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                Amount = subtotal,
                PaymentMethod = paymentMethod,
                PaymentStatus = AppConstants.PaymentStatus.Pending,
                PaymentDate = DateTime.Now,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đặt hàng thành công! Mã đơn hàng: {order.OrderCode}";
            return RedirectToAction("Index", "Home");
        }

        // GET: Cart/GetCartCount (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var customerId = GetCurrentCustomerId();
            var sessionId = GetSessionId();

            var count = await _context.Carts
                .Where(c => (customerId.HasValue && c.CustomerId == customerId) 
                    || (!customerId.HasValue && c.SessionId == sessionId))
                .SumAsync(c => c.Quantity ?? 0);

            return Json(new { count = count });
        }
    }
}

