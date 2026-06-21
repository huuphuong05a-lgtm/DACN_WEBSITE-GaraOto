using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace CarServ.MVC.Controllers
{
    public class ReviewController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<ReviewController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ReviewController(CarServContext context, ILogger<ReviewController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
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
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Service(int? appointmentId, int? serviceHistoryId)
        {
            var customerId = GetCurrentCustomerId();
            var history = await ResolveServiceHistoryAsync(customerId, appointmentId, serviceHistoryId);

            if (history == null)
            {
                return NotFound();
            }

            if (!CanReviewServiceHistory(history))
            {
                return Forbid();
            }

            if (await _context.Reviews.AnyAsync(r => r.CustomerId == customerId && r.ServiceHistoryId == history.ServiceHistoryId))
            {
                TempData["ErrorMessage"] = "Ban da danh gia dich vu nay.";
                return RedirectToVehicleDetails(history.VehicleId);
            }

            return View("CreateServiceReview", BuildServiceReviewViewModel(history));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Service(ServiceReviewInputModel model)
        {
            var customerId = GetCurrentCustomerId();
            var history = await ResolveServiceHistoryAsync(customerId, model.AppointmentId, model.ServiceHistoryId);

            if (history == null)
            {
                return NotFound();
            }

            if (!CanReviewServiceHistory(history))
            {
                return Forbid();
            }

            if (await _context.Reviews.AnyAsync(r => r.CustomerId == customerId && r.ServiceHistoryId == history.ServiceHistoryId))
            {
                TempData["ErrorMessage"] = "Ban da danh gia dich vu nay.";
                return RedirectToVehicleDetails(history.VehicleId);
            }

            if (model.Rating < 1 || model.Rating > 5)
            {
                ModelState.AddModelError(nameof(model.Rating), "Vui long chon so sao tu 1 den 5.");
            }

            if (string.IsNullOrWhiteSpace(model.Comment))
            {
                ModelState.AddModelError(nameof(model.Comment), "Vui long nhap noi dung danh gia.");
            }
            else if (model.Comment.Length > 1000)
            {
                ModelState.AddModelError(nameof(model.Comment), "Noi dung danh gia khong duoc vuot qua 1000 ky tu.");
            }

            string? imageUrl = null;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                imageUrl = await SaveReviewImageAsync(model.ImageFile);
                if (imageUrl == null)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), "Anh khong hop le. Chi ho tro JPG, PNG, WEBP va toi da 2MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                var viewModel = BuildServiceReviewViewModel(history);
                viewModel.Rating = model.Rating;
                viewModel.Comment = model.Comment;
                return View("CreateServiceReview", viewModel);
            }

            var customer = await _context.Customers.FindAsync(customerId);
            var review = new Review
            {
                CustomerId = customerId,
                ServiceId = history.ServiceId,
                ServiceHistoryId = history.ServiceHistoryId,
                CustomerName = customer?.FullName ?? User.Identity?.Name ?? "Khach hang",
                Rating = model.Rating,
                Comment = model.Comment!.Trim(),
                ImageUrl = imageUrl,
                CreatedDate = DateTime.Now,
                IsApproved = true
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Service review added: {ReviewId} for ServiceHistory {ServiceHistoryId} by Customer {CustomerId}",
                review.Id,
                history.ServiceHistoryId,
                customerId);

            TempData["SuccessMessage"] = "Cam on ban da danh gia dich vu.";
            return RedirectToVehicleDetails(history.VehicleId);
        }

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

        private int GetCurrentCustomerId()
        {
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim != null && int.TryParse(customerIdClaim.Value, out var customerId))
            {
                return customerId;
            }

            throw new UnauthorizedAccessException("Khong tim thay thong tin khach hang.");
        }

        private async Task<ServiceHistory?> ResolveServiceHistoryAsync(int customerId, int? appointmentId, int? serviceHistoryId)
        {
            var query = _context.ServiceHistories
                .Include(h => h.Appointment)
                .Include(h => h.Service)
                .Include(h => h.Technician)
                .Include(h => h.Vehicle)
                    .ThenInclude(v => v.Brand)
                .Include(h => h.Vehicle)
                    .ThenInclude(v => v.Model)
                .Where(h => h.Vehicle.CustomerId == customerId);

            if (serviceHistoryId.HasValue)
            {
                return await query.FirstOrDefaultAsync(h => h.ServiceHistoryId == serviceHistoryId.Value);
            }

            if (appointmentId.HasValue)
            {
                return await query.FirstOrDefaultAsync(h => h.AppointmentId == appointmentId.Value);
            }

            return null;
        }

        private static bool CanReviewServiceHistory(ServiceHistory history)
        {
            return history.Appointment == null
                || history.Appointment.Status == AppConstants.AppointmentStatus.Completed;
        }

        private ServiceReviewViewModel BuildServiceReviewViewModel(ServiceHistory history)
        {
            var vehicleName = string.Join(" ", new[]
            {
                history.Vehicle.Brand?.BrandName,
                history.Vehicle.Model?.ModelName,
                history.Vehicle.Year?.ToString()
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return new ServiceReviewViewModel
            {
                AppointmentId = history.AppointmentId,
                ServiceHistoryId = history.ServiceHistoryId,
                VehicleId = history.VehicleId,
                ServiceName = history.Service?.ServiceName ?? history.ServiceName ?? "Dich vu gara",
                VehicleLabel = string.IsNullOrWhiteSpace(vehicleName)
                    ? history.Vehicle.LicensePlate
                    : $"{history.Vehicle.LicensePlate} - {vehicleName}",
                ServiceDate = history.ServiceDate,
                TechnicianName = history.Technician?.FullName
            };
        }

        private IActionResult RedirectToVehicleDetails(int vehicleId)
        {
            return RedirectToAction("VehicleDetails", "Customer", new { id = vehicleId });
        }

        private async Task<string?> SaveReviewImageAsync(IFormFile imageFile)
        {
            const long maxFileSize = 2 * 1024 * 1024;
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension) || imageFile.Length > maxFileSize)
            {
                return null;
            }

            var uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "reviews");
            Directory.CreateDirectory(uploadFolder);

            var fileName = $"review-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return $"/uploads/reviews/{fileName}";
        }
    }

    public class ServiceReviewInputModel
    {
        public int? AppointmentId { get; set; }

        public int ServiceHistoryId { get; set; }

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public IFormFile? ImageFile { get; set; }
    }

    public class ServiceReviewViewModel : ServiceReviewInputModel
    {
        public int VehicleId { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public string VehicleLabel { get; set; } = string.Empty;

        public DateTime ServiceDate { get; set; }

        public string? TechnicianName { get; set; }
    }
}


