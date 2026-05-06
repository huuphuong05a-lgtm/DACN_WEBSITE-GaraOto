using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class ServiceController : Controller
    {
        private readonly CarServContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ServiceController(CarServContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/Service
        public async Task<IActionResult> Index(string searchString, string categoryFilter, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = categoryFilter;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["PriceSortParm"] = sortOrder == "Price" ? "price_desc" : "Price";
            ViewData["SortOrderSortParm"] = sortOrder == "SortOrder" ? "sortorder_desc" : "SortOrder";

            var services = _context.Services.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                services = services.Where(s => s.ServiceName.Contains(searchString) 
                    || (s.ShortDescription != null && s.ShortDescription.Contains(searchString))
                    || (s.ServiceCategory != null && s.ServiceCategory.Contains(searchString)));
            }

            // Filter by category
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                services = services.Where(s => s.ServiceCategory == categoryFilter);
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                bool isActive = statusFilter == "active";
                services = services.Where(s => s.IsActive == isActive);
            }

            // Sort
            switch (sortOrder)
            {
                case "name_desc":
                    services = services.OrderByDescending(s => s.ServiceName);
                    break;
                case "Price":
                    services = services.OrderBy(s => s.Price);
                    break;
                case "price_desc":
                    services = services.OrderByDescending(s => s.Price);
                    break;
                case "SortOrder":
                    services = services.OrderBy(s => s.SortOrder);
                    break;
                case "sortorder_desc":
                    services = services.OrderByDescending(s => s.SortOrder);
                    break;
                default:
                    services = services.OrderBy(s => s.SortOrder);
                    break;
            }

            var serviceList = await services.ToListAsync();
            
            // Get distinct categories for filter dropdown
            ViewBag.Categories = await _context.Services
                .Where(s => s.ServiceCategory != null)
                .Select(s => s.ServiceCategory)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return View(serviceList);
        }

        // GET: Admin/Service/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services
                .FirstOrDefaultAsync(m => m.ServiceId == id);

            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // GET: Admin/Service/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Service/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ServiceName,ShortDescription,Description,Price,EstimatedDuration,ServiceCategory,IsActive,IsFeatured,SortOrder,MetaTitle,MetaDescription,ImageUrl")] Service service)
        {
            if (ModelState.IsValid)
            {
                // Generate slug from ServiceName
                service.Slug = GenerateSlug(service.ServiceName);
                
                service.CreatedDate = DateTime.Now;
                service.UpdatedDate = DateTime.Now;
                service.SortOrder ??= 0;

                // Handle checkbox
                var isActiveValues = Request.Form["IsActive"];
                service.IsActive = isActiveValues.Contains("true");
                
                var isFeaturedValues = Request.Form["IsFeatured"];
                service.IsFeatured = isFeaturedValues.Contains("true");

                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(service);
        }

        // GET: Admin/Service/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // POST: Admin/Service/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ServiceId,ServiceName,Slug,ShortDescription,Description,Price,EstimatedDuration,ServiceCategory,IsActive,IsFeatured,SortOrder,MetaTitle,MetaDescription,ImageUrl,CreatedDate")] Service service)
        {
            if (id != service.ServiceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Load existing service
                    var existingService = await _context.Services.FindAsync(id);
                    if (existingService == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingService.ServiceName = service.ServiceName;
                    existingService.Slug = GenerateSlug(service.ServiceName);
                    existingService.ShortDescription = service.ShortDescription;
                    existingService.Description = service.Description;
                    existingService.Price = service.Price;
                    existingService.EstimatedDuration = service.EstimatedDuration;
                    existingService.ServiceCategory = service.ServiceCategory;
                    existingService.SortOrder = service.SortOrder;
                    existingService.MetaTitle = service.MetaTitle;
                    existingService.MetaDescription = service.MetaDescription;
                    existingService.UpdatedDate = DateTime.Now;

                    // Handle image: use value from form (from ImageUrl field)
                    existingService.ImageUrl = service.ImageUrl;

                    // Handle checkboxes
                    var isActiveValues = Request.Form["IsActive"];
                    existingService.IsActive = isActiveValues.Contains("true");
                    
                    var isFeaturedValues = Request.Form["IsFeatured"];
                    existingService.IsFeatured = isFeaturedValues.Contains("true");

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceExists(service.ServiceId))
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

            return View(service);
        }

        // GET: Admin/Service/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var service = await _context.Services
                .FirstOrDefaultAsync(m => m.ServiceId == id);

            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        // POST: Admin/Service/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                // Delete image if exists
                if (!string.IsNullOrEmpty(service.ImageUrl))
                {
                    DeleteImageFile(service.ImageUrl);
                }

                _context.Services.Remove(service);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ServiceExists(int id)
        {
            return _context.Services.Any(e => e.ServiceId == id);
        }

        private string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Convert to lowercase
            text = text.ToLowerInvariant();

            // Remove Vietnamese accents
            text = RemoveVietnameseAccents(text);

            // Replace spaces with hyphens
            text = Regex.Replace(text, @"\s+", "-");

            // Remove invalid characters
            text = Regex.Replace(text, @"[^a-z0-9\-]", "");

            // Remove consecutive hyphens
            text = Regex.Replace(text, @"\-+", "-");

            // Trim hyphens from start and end
            text = text.Trim('-');

            return text;
        }

        private string RemoveVietnameseAccents(string text)
        {
            string[] VietnameseSigns = new string[]
            {
                "aAeEoOuUiIdDyY",
                "áàạảãâấầậẩẫăắằặẳẵ",
                "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
                "éèẹẻẽêếềệểễ",
                "ÉÈẸẺẼÊẾỀỆỂỄ",
                "óòọỏõôốồộổỗơớờợởỡ",
                "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
                "úùụủũưứừựửữ",
                "ÚÙỤỦŨƯỨỪỰỬỮ",
                "íìịỉĩ",
                "ÍÌỊỈĨ",
                "đ",
                "Đ",
                "ýỳỵỷỹ",
                "ÝỲỴỶỸ"
            };

            for (int i = 1; i < VietnameseSigns.Length; i++)
            {
                for (int j = 0; j < VietnameseSigns[i].Length; j++)
                    text = text.Replace(VietnameseSigns[i][j], VietnameseSigns[0][i - 1]);
            }

            return text;
        }

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return "/images/services/" + uniqueFileName;
        }

        private void DeleteImageFile(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return;

            var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }
        }

        // GET: Admin/Service/BrowseImages
        public IActionResult BrowseImages()
        {
            var imagesFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
            var images = new List<object>();

            if (Directory.Exists(imagesFolder))
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var files = Directory.GetFiles(imagesFolder)
                    .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .ToList();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var fileUrl = "/images/services/" + fileName;
                    var fileSize = new FileInfo(file).Length;
                    
                    images.Add(new
                    {
                        url = fileUrl,
                        name = fileName,
                        size = fileSize
                    });
                }
            }

            return Json(images);
        }

        // POST: Admin/Service/UploadImage
        [HttpPost]
        [Route("Admin/Service/UploadImage")]
        [IgnoreAntiforgeryToken]
        [RequestSizeLimit(10_485_760)] // 10MB
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file ảnh" });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return Json(new { success = false, message = "Chỉ chấp nhận file ảnh: JPG, JPEG, PNG, GIF, WEBP" });
            }

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "services");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Sanitize filename
            var sanitizedFileName = Path.GetFileName(file.FileName);
            sanitizedFileName = string.Join("_", sanitizedFileName.Split(Path.GetInvalidFileNameChars()));
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + sanitizedFileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                var fileUrl = "/images/services/" + uniqueFileName;
                var fileSize = new FileInfo(filePath).Length;

                return Json(new
                {
                    success = true,
                    image = new
                    {
                        url = fileUrl,
                        name = uniqueFileName,
                        size = fileSize
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi upload ảnh: " + ex.Message });
            }
        }

        // POST: Admin/Service/DeleteImage
        [HttpPost]
        [Route("Admin/Service/DeleteImage")]
        [IgnoreAntiforgeryToken]
        public IActionResult DeleteImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return Json(new { success = false, message = "URL ảnh không hợp lệ" });
            }

            try
            {
                var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, imageUrl.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                    return Json(new { success = true, message = "Xóa ảnh thành công" });
                }
                else
                {
                    return Json(new { success = false, message = "Không tìm thấy file ảnh" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa ảnh: " + ex.Message });
            }
        }
    }
}

