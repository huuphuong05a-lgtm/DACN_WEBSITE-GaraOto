using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class BlogPostAdminController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<BlogPostAdminController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BlogPostAdminController(CarServContext context, ILogger<BlogPostAdminController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/BlogPostAdmin
        public async Task<IActionResult> Index(string searchString, int? categoryId, string publishedFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = categoryId;
            ViewData["CurrentPublished"] = publishedFilter;
            ViewData["CurrentSort"] = sortOrder;

            var posts = _context.BlogPosts
                .Include(p => p.Category)
                .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                posts = posts.Where(p => p.Title.Contains(searchString) ||
                    (p.ShortDescription != null && p.ShortDescription.Contains(searchString)));
            }

            // Filter by category
            if (categoryId.HasValue)
            {
                posts = posts.Where(p => p.CategoryId == categoryId);
            }

            // Filter by published status
            if (!string.IsNullOrEmpty(publishedFilter))
            {
                bool isPublished = publishedFilter == "published";
                posts = posts.Where(p => p.IsPublished == isPublished);
            }

            // Sort
            switch (sortOrder)
            {
                case "title_desc":
                    posts = posts.OrderByDescending(p => p.Title);
                    break;
                case "Date":
                    posts = posts.OrderBy(p => p.PublishedDate ?? DateTime.MinValue).ThenBy(p => p.Id);
                    break;
                case "date_desc":
                    posts = posts.OrderByDescending(p => p.PublishedDate ?? DateTime.MinValue).ThenByDescending(p => p.Id);
                    break;
                default:
                    posts = posts.OrderByDescending(p => p.PublishedDate ?? DateTime.MinValue).ThenByDescending(p => p.Id);
                    break;
            }

            // Get categories for filter dropdown
            ViewData["Categories"] = await _context.BlogCategories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(await posts.ToListAsync());
        }

        // GET: Admin/BlogPostAdmin/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.BlogPosts
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            return View(post);
        }

        // GET: Admin/BlogPostAdmin/Create
        public async Task<IActionResult> Create()
        {
            ViewData["CategoryId"] = new SelectList(
                await _context.BlogCategories.Where(c => c.IsActive == true).OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name");
            return View();
        }

        // POST: Admin/BlogPostAdmin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,ShortDescription,Content,Thumbnail,CategoryId,PublishedDate,IsPublished,SeoTitle,SeoDescription")] BlogPost post)
        {
            if (ModelState.IsValid)
            {
                // Generate slug from Title
                post.Slug = GenerateSlug(post.Title);
                
                // Ensure unique slug
                var baseSlug = post.Slug;
                int counter = 1;
                while (await _context.BlogPosts.AnyAsync(p => p.Slug == post.Slug))
                {
                    post.Slug = $"{baseSlug}-{counter}";
                    counter++;
                }

                post.ViewCount = 0;
                if (!post.PublishedDate.HasValue && post.IsPublished)
                {
                    post.PublishedDate = DateTime.Now;
                }

                _context.Add(post);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm bài viết blog thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(
                await _context.BlogCategories.Where(c => c.IsActive == true).OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", post.CategoryId);
            return View(post);
        }

        // GET: Admin/BlogPostAdmin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            ViewData["CategoryId"] = new SelectList(
                await _context.BlogCategories.Where(c => c.IsActive == true).OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", post.CategoryId);
            return View(post);
        }

        // POST: Admin/BlogPostAdmin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Slug,ShortDescription,Content,Thumbnail,CategoryId,PublishedDate,IsPublished,ViewCount,SeoTitle,SeoDescription")] BlogPost post)
        {
            if (id != post.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update slug if title changed
                    var existingPost = await _context.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                    if (existingPost != null && existingPost.Title != post.Title)
                    {
                        var newSlug = GenerateSlug(post.Title);
                        var baseSlug = newSlug;
                        int counter = 1;
                        while (await _context.BlogPosts.AnyAsync(p => p.Slug == newSlug && p.Id != id))
                        {
                            newSlug = $"{baseSlug}-{counter}";
                            counter++;
                        }
                        post.Slug = newSlug;
                    }

                    // Set published date if publishing for the first time
                    if (post.IsPublished && !post.PublishedDate.HasValue)
                    {
                        post.PublishedDate = DateTime.Now;
                    }

                    _context.Update(post);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật bài viết blog thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlogPostExists(post.Id))
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

            ViewData["CategoryId"] = new SelectList(
                await _context.BlogCategories.Where(c => c.IsActive == true).OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", post.CategoryId);
            return View(post);
        }

        // GET: Admin/BlogPostAdmin/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.BlogPosts
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            return View(post);
        }

        // POST: Admin/BlogPostAdmin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post != null)
            {
                _context.BlogPosts.Remove(post);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa bài viết blog thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/BlogPostAdmin/BrowseImages
        public IActionResult BrowseImages()
        {
            try
            {
                var imagesFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "blog");
                var images = new List<object>();

                // Tạo thư mục nếu chưa tồn tại
                if (!Directory.Exists(imagesFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(imagesFolder);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating images folder");
                        return Json(new List<object>());
                    }
                }

                if (Directory.Exists(imagesFolder))
                {
                    try
                    {
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                        var files = Directory.GetFiles(imagesFolder)
                            .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .OrderByDescending(f => new FileInfo(f).CreationTime)
                            .ToList();

                        foreach (var file in files)
                        {
                            try
                            {
                                var fileName = Path.GetFileName(file);
                                var fileUrl = "/images/blog/" + fileName;
                                var fileInfo = new FileInfo(file);
                                var fileSize = fileInfo.Exists ? fileInfo.Length : 0;
                                
                                images.Add(new
                                {
                                    url = fileUrl,
                                    name = fileName,
                                    size = fileSize
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error processing file: {FileName}", file);
                                // Bỏ qua file lỗi, tiếp tục với file khác
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading files from images folder");
                        return Json(new List<object>());
                    }
                }

                return Json(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BrowseImages");
                return Json(new List<object>());
            }
        }

        // POST: Admin/BlogPostAdmin/UploadImage
        [HttpPost]
        [Route("Admin/BlogPostAdmin/UploadImage")]
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

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "blog");
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

                var fileUrl = "/images/blog/" + uniqueFileName;
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
                _logger.LogError(ex, "Error uploading image");
                return Json(new { success = false, message = "Lỗi khi upload ảnh: " + ex.Message });
            }
        }

        // POST: Admin/BlogPostAdmin/DeleteImage
        [HttpPost]
        [Route("Admin/BlogPostAdmin/DeleteImage")]
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
                _logger.LogError(ex, "Error deleting image: {ImageUrl}", imageUrl);
                return Json(new { success = false, message = "Lỗi khi xóa ảnh: " + ex.Message });
            }
        }

        private bool BlogPostExists(int id)
        {
            return _context.BlogPosts.Any(e => e.Id == id);
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

