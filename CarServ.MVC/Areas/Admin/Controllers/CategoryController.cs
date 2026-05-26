using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Helpers;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Hosting;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
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
            return Json(AdminImageStorage.BrowseImages(_webHostEnvironment, "images", "categories"));
        }

        // POST: Admin/Category/UploadImage
        [HttpPost]
        [Route("Admin/Category/UploadImage")]
        [IgnoreAntiforgeryToken]
        [RequestSizeLimit(10_485_760)] // 10MB
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            try
            {
                var result = await AdminImageStorage.SaveImageAsync(_webHostEnvironment, file, "images", "categories");
                return Json(new { success = result.Success, message = result.Message, image = result.Image });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Lỗi khi upload ảnh. Vui lòng thử lại." });
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
                if (AdminImageStorage.DeleteImage(_webHostEnvironment, imageUrl, "images", "categories"))
                {
                    return Json(new { success = true, message = "Xóa ảnh thành công" });
                }

                return Json(new { success = false, message = "Không tìm thấy file ảnh" });
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

