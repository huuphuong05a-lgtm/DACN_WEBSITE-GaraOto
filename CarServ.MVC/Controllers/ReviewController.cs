using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Controllers
{
    public class ReviewController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<ReviewController> _logger;

        public ReviewController(CarServContext context, ILogger<ReviewController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: Review/AddReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int? productId, int? serviceId, string customerName, int rating, string? comment)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(customerName))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập họ tên.";
                return RedirectToProductOrService(productId, serviceId);
            }

            if (rating < 1 || rating > 5)
            {
                TempData["ErrorMessage"] = "Đánh giá phải từ 1 đến 5 sao.";
                return RedirectToProductOrService(productId, serviceId);
            }

            // Kiểm tra productId hoặc serviceId phải có một
            if (!productId.HasValue && !serviceId.HasValue)
            {
                TempData["ErrorMessage"] = "Không xác định được sản phẩm hoặc dịch vụ.";
                return RedirectToAction("Index", "Home");
            }

            // Kiểm tra sản phẩm hoặc dịch vụ tồn tại
            if (productId.HasValue)
            {
                var product = await _context.Products.FindAsync(productId.Value);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "Sản phẩm không tồn tại.";
                    return RedirectToAction("Index", "Product");
                }
            }

            if (serviceId.HasValue)
            {
                var service = await _context.Services.FindAsync(serviceId.Value);
                if (service == null)
                {
                    TempData["ErrorMessage"] = "Dịch vụ không tồn tại.";
                    return RedirectToAction("Index", "Service");
                }
            }

            // Tạo review mới
            var review = new Review
            {
                ProductId = productId,
                ServiceId = serviceId,
                CustomerName = customerName.Trim(),
                Rating = rating,
                Comment = comment?.Trim(),
                CreatedDate = DateTime.Now,
                IsApproved = false // Mặc định chưa duyệt
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Review added: {ReviewId} for {Type} {Id}", 
                review.Id, 
                productId.HasValue ? "Product" : "Service", 
                productId ?? serviceId);

            TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá! Đánh giá của bạn đang chờ duyệt.";
            return RedirectToProductOrService(productId, serviceId);
        }

        // Helper method để redirect về trang chi tiết
        private IActionResult RedirectToProductOrService(int? productId, int? serviceId)
        {
            if (productId.HasValue)
            {
                var product = _context.Products.Find(productId.Value);
                if (product != null && !string.IsNullOrEmpty(product.Slug))
                {
                    return RedirectToAction("Details", "Product", new { slug = product.Slug });
                }
                return RedirectToAction("Details", "Product", new { id = productId.Value });
            }

            if (serviceId.HasValue)
            {
                var service = _context.Services.Find(serviceId.Value);
                if (service != null && !string.IsNullOrEmpty(service.Slug))
                {
                    return RedirectToAction("Details", "Service", new { slug = service.Slug });
                }
                return RedirectToAction("Details", "Service", new { id = serviceId.Value });
            }

            return RedirectToAction("Index", "Home");
        }

        // Helper methods để tính rating trung bình (có thể gọi từ controller khác)
        public static double GetAverageRatingForProduct(CarServContext context, int productId)
        {
            var reviews = context.Reviews
                .Where(r => r.ProductId == productId && r.IsApproved)
                .ToList();

            if (reviews.Count == 0)
                return 0.0;

            return Math.Round(reviews.Average(r => r.Rating), 1);
        }

        public static double GetAverageRatingForService(CarServContext context, int serviceId)
        {
            var reviews = context.Reviews
                .Where(r => r.ServiceId == serviceId && r.IsApproved)
                .ToList();

            if (reviews.Count == 0)
                return 0.0;

            return Math.Round(reviews.Average(r => r.Rating), 1);
        }

        public static int GetReviewCountForProduct(CarServContext context, int productId)
        {
            return context.Reviews
                .Count(r => r.ProductId == productId && r.IsApproved);
        }

        public static int GetReviewCountForService(CarServContext context, int serviceId)
        {
            return context.Reviews
                .Count(r => r.ServiceId == serviceId && r.IsApproved);
        }
    }
}


