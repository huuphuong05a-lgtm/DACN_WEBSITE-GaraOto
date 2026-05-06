using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using System.Text.RegularExpressions;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class BlogCategoryAdminController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<BlogCategoryAdminController> _logger;

        public BlogCategoryAdminController(CarServContext context, ILogger<BlogCategoryAdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin/BlogCategoryAdmin
        public async Task<IActionResult> Index(string searchString, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;

            var categories = _context.BlogCategories.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                categories = categories.Where(c => c.Name.Contains(searchString) ||
                    (c.Description != null && c.Description.Contains(searchString)));
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
                    categories = categories.OrderByDescending(c => c.Name);
                    break;
                case "DisplayOrder":
                    categories = categories.OrderBy(c => c.DisplayOrder);
                    break;
                case "displayorder_desc":
                    categories = categories.OrderByDescending(c => c.DisplayOrder);
                    break;
                default:
                    categories = categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name);
                    break;
            }

            return View(await categories.ToListAsync());
        }

        // GET: Admin/BlogCategoryAdmin/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.BlogCategories
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Admin/BlogCategoryAdmin/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/BlogCategoryAdmin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,IsActive,DisplayOrder")] BlogCategory category)
        {
            if (ModelState.IsValid)
            {
                // Generate slug from Name
                category.Slug = GenerateSlug(category.Name);
                category.CreatedDate = DateTime.Now;

                // Check if slug already exists
                if (await _context.BlogCategories.AnyAsync(c => c.Slug == category.Slug))
                {
                    ModelState.AddModelError("Name", "Tên chuyên mục đã tồn tại. Vui lòng chọn tên khác.");
                    return View(category);
                }

                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm chuyên mục blog thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(category);
        }

        // GET: Admin/BlogCategoryAdmin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.BlogCategories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Admin/BlogCategoryAdmin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Slug,Description,IsActive,DisplayOrder,CreatedDate")] BlogCategory category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update slug if name changed
                    var existingCategory = await _context.BlogCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                    if (existingCategory != null && existingCategory.Name != category.Name)
                    {
                        category.Slug = GenerateSlug(category.Name);
                        
                        // Check if new slug already exists
                        if (await _context.BlogCategories.AnyAsync(c => c.Slug == category.Slug && c.Id != id))
                        {
                            ModelState.AddModelError("Name", "Tên chuyên mục đã tồn tại. Vui lòng chọn tên khác.");
                            return View(category);
                        }
                    }

                    _context.Update(category);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật chuyên mục blog thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlogCategoryExists(category.Id))
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

            return View(category);
        }

        // GET: Admin/BlogCategoryAdmin/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.BlogCategories
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            // Check if category has posts
            var hasPosts = await _context.BlogPosts.AnyAsync(p => p.CategoryId == id);
            ViewData["HasPosts"] = hasPosts;

            return View(category);
        }

        // POST: Admin/BlogCategoryAdmin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.BlogCategories.FindAsync(id);
            if (category != null)
            {
                // Check if category has posts
                var hasPosts = await _context.BlogPosts.AnyAsync(p => p.CategoryId == id);
                if (hasPosts)
                {
                    TempData["ErrorMessage"] = "Không thể xóa chuyên mục này vì đang có bài viết. Vui lòng xóa hoặc chuyển các bài viết trước.";
                    return RedirectToAction(nameof(Index));
                }

                _context.BlogCategories.Remove(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa chuyên mục blog thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool BlogCategoryExists(int id)
        {
            return _context.BlogCategories.Any(e => e.Id == id);
        }

        private string GenerateSlug(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.ToLowerInvariant();
            text = RemoveVietnameseAccents(text);
            text = Regex.Replace(text, @"\s+", "-");
            text = Regex.Replace(text, @"[^a-z0-9\-]", "");
            text = Regex.Replace(text, @"\-+", "-");
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
                {
                    text = text.Replace(VietnameseSigns[i][j], VietnameseSigns[0][i - 1]);
                }
            }

            return text;
        }
    }
}


