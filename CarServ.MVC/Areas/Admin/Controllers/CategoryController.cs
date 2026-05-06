using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Hosting;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class CategoryController : Controller
    {
        private readonly CarServContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CategoryController(CarServContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/Category
        public async Task<IActionResult> Index(string searchString, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["SortOrderSortParm"] = sortOrder == "SortOrder" ? "sortorder_desc" : "SortOrder";

            var categories = _context.Categories.Include(c => c.Parent).AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                categories = categories.Where(c => c.CategoryName.Contains(searchString) 
                    || (c.Description != null && c.Description.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                bool isActive = statusFilter == "active";
                categories = categories.Where(c => c.IsActive == isActive);
            }

            // Sort
            switch (sortOrder)
            {
                case "name_desc":
                    categories = categories.OrderByDescending(c => c.CategoryName);
                    break;
                case "SortOrder":
                    categories = categories.OrderBy(c => c.SortOrder);
                    break;
                case "sortorder_desc":
                    categories = categories.OrderByDescending(c => c.SortOrder);
                    break;
                default:
                    categories = categories.OrderBy(c => c.SortOrder);
                    break;
            }

            var categoryList = await categories.ToListAsync();
            return View(categoryList);
        }

        // GET: Admin/Category/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Parent)
                .Include(c => c.InverseParent)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.CategoryId == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Admin/Category/Create
        public IActionResult Create()
        {
            ViewData["ParentId"] = new SelectList(_context.Categories.Where(c => c.ParentId == null), "CategoryId", "CategoryName");
            return View();
        }

        // POST: Admin/Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CategoryName,Description,ParentId,SortOrder,IconClass,MetaTitle,MetaDescription,ImageUrl")] Category category)
        {
            if (ModelState.IsValid)
            {
                category.CreatedDate = DateTime.Now;
                category.UpdatedDate = DateTime.Now;
                category.SortOrder ??= 0;

                // Handle checkbox
                var isActiveValues = Request.Form["IsActive"];
                category.IsActive = isActiveValues.Contains("true");

                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["ParentId"] = new SelectList(_context.Categories.Where(c => c.ParentId == null), "CategoryId", "CategoryName", category.ParentId);
            return View(category);
        }

        // GET: Admin/Category/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            // Không cho phép chọn chính nó làm parent
            ViewData["ParentId"] = new SelectList(_context.Categories.Where(c => c.ParentId == null && c.CategoryId != id), "CategoryId", "CategoryName", category.ParentId);
            return View(category);
        }

        // POST: Admin/Category/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CategoryId,CategoryName,Description,ParentId,SortOrder,IconClass,MetaTitle,MetaDescription,ImageUrl,CreatedDate")] Category category)
        {
            if (id != category.CategoryId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Load existing category
                    var existingCategory = await _context.Categories.FindAsync(id);
                    if (existingCategory == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingCategory.CategoryName = category.CategoryName;
                    existingCategory.Description = category.Description;
                    existingCategory.ParentId = category.ParentId;
                    existingCategory.SortOrder = category.SortOrder;
                    existingCategory.IconClass = category.IconClass;
                    existingCategory.MetaTitle = category.MetaTitle;
                    existingCategory.MetaDescription = category.MetaDescription;
                    existingCategory.ImageUrl = category.ImageUrl;
                    existingCategory.UpdatedDate = DateTime.Now;

                    // Handle checkbox
                    var isActiveValues = Request.Form["IsActive"];
                    existingCategory.IsActive = isActiveValues.Contains("true");

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.CategoryId))
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

            ViewData["ParentId"] = new SelectList(_context.Categories.Where(c => c.ParentId == null && c.CategoryId != id), "CategoryId", "CategoryName", category.ParentId);
            return View(category);
        }

        // GET: Admin/Category/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.Parent)
                .Include(c => c.InverseParent)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.CategoryId == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Admin/Category/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories
                .Include(c => c.InverseParent)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(m => m.CategoryId == id);

            if (category != null)
            {
                // Kiểm tra xem có sản phẩm nào đang sử dụng danh mục này không
                if (category.Products.Any())
                {
                    TempData["ErrorMessage"] = "Không thể xóa danh mục này vì có sản phẩm đang sử dụng. Vui lòng xóa hoặc chuyển các sản phẩm sang danh mục khác trước.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }

                // Nếu có danh mục con, xóa cả danh mục con trước
                if (category.InverseParent.Any())
                {
                    _context.Categories.RemoveRange(category.InverseParent);
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Category/BrowseImages
        public IActionResult BrowseImages()
        {
            var imagesFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "categories");
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
                    var fileUrl = "/images/categories/" + fileName;
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

        // POST: Admin/Category/UploadImage
        [HttpPost]
        [Route("Admin/Category/UploadImage")]
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

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "categories");
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

                var fileUrl = "/images/categories/" + uniqueFileName;
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

        // POST: Admin/Category/DeleteImage
        [HttpPost]
        [Route("Admin/Category/DeleteImage")]
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

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.CategoryId == id);
        }
    }
}

