using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class ProductController : Controller
    {
        private readonly CarServContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(CarServContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/Product
        public async Task<IActionResult> Index(string searchString, string categoryFilter, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = categoryFilter;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["PriceSortParm"] = sortOrder == "Price" ? "price_desc" : "Price";
            ViewData["SortOrderSortParm"] = sortOrder == "SortOrder" ? "sortorder_desc" : "SortOrder";

            var products = _context.Products.Include(p => p.Category).AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p => p.ProductName.Contains(searchString) 
                    || (p.ShortDescription != null && p.ShortDescription.Contains(searchString))
                    || (p.Sku != null && p.Sku.Contains(searchString))
                    || (p.Brand != null && p.Brand.Contains(searchString)));
            }

            // Filter by category
            if (!string.IsNullOrEmpty(categoryFilter) && int.TryParse(categoryFilter, out int categoryId))
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                bool isActive = statusFilter == "active";
                products = products.Where(p => p.IsActive == isActive);
            }

            // Sort
            switch (sortOrder)
            {
                case "name_desc":
                    products = products.OrderByDescending(p => p.ProductName);
                    break;
                case "Price":
                    products = products.OrderBy(p => p.Price);
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price);
                    break;
                default:
                    products = products.OrderByDescending(p => p.CreatedDate);
                    break;
            }

            var productList = await products.ToListAsync();
            
            // Get categories for filter dropdown
            ViewBag.Categories = await _context.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            return View(productList);
        }

        // GET: Admin/Product/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Admin/Product/Create
        public async Task<IActionResult> Create()
        {
            ViewData["CategoryId"] = new SelectList(await _context.Categories.Where(c => c.IsActive == true).OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName");
            return View();
        }

        // POST: Admin/Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductName,ShortDescription,Description,Price,SalePrice,CategoryId,StockQuantity,Sku,Weight,Dimensions,Brand,WarrantyMonths,IsActive,IsFeatured,IsNew,MetaTitle,MetaDescription,ImageUrl")] Product product)
        {
            if (ModelState.IsValid)
            {
                // Generate slug from ProductName
                product.Slug = GenerateSlug(product.ProductName);
                
                product.CreatedDate = DateTime.Now;
                product.UpdatedDate = DateTime.Now;
                product.StockQuantity ??= 0;
                product.ViewCount = 0;

                // Handle checkboxes
                var isActiveValues = Request.Form["IsActive"];
                product.IsActive = isActiveValues.Contains("true");
                
                var isFeaturedValues = Request.Form["IsFeatured"];
                product.IsFeatured = isFeaturedValues.Contains("true");
                
                var isNewValues = Request.Form["IsNew"];
                product.IsNew = isNewValues.Contains("true");

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(await _context.Categories.Where(c => c.IsActive == true).OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // GET: Admin/Product/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewData["CategoryId"] = new SelectList(await _context.Categories.Where(c => c.IsActive == true).OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // POST: Admin/Product/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProductId,ProductName,Slug,ShortDescription,Description,Price,SalePrice,CategoryId,StockQuantity,Sku,Weight,Dimensions,Brand,WarrantyMonths,IsActive,IsFeatured,IsNew,MetaTitle,MetaDescription,ImageUrl,CreatedDate")] Product product)
        {
            if (id != product.ProductId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Load existing product
                    var existingProduct = await _context.Products.FindAsync(id);
                    if (existingProduct == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingProduct.ProductName = product.ProductName;
                    existingProduct.Slug = GenerateSlug(product.ProductName);
                    existingProduct.ShortDescription = product.ShortDescription;
                    existingProduct.Description = product.Description;
                    existingProduct.Price = product.Price;
                    existingProduct.SalePrice = product.SalePrice;
                    existingProduct.CategoryId = product.CategoryId;
                    existingProduct.StockQuantity = product.StockQuantity;
                    existingProduct.Sku = product.Sku;
                    existingProduct.Weight = product.Weight;
                    existingProduct.Dimensions = product.Dimensions;
                    existingProduct.Brand = product.Brand;
                    existingProduct.WarrantyMonths = product.WarrantyMonths;
                    existingProduct.MetaTitle = product.MetaTitle;
                    existingProduct.MetaDescription = product.MetaDescription;
                    existingProduct.ImageUrl = product.ImageUrl;
                    existingProduct.UpdatedDate = DateTime.Now;

                    // Handle checkboxes
                    var isActiveValues = Request.Form["IsActive"];
                    existingProduct.IsActive = isActiveValues.Contains("true");
                    
                    var isFeaturedValues = Request.Form["IsFeatured"];
                    existingProduct.IsFeatured = isFeaturedValues.Contains("true");
                    
                    var isNewValues = Request.Form["IsNew"];
                    existingProduct.IsNew = isNewValues.Contains("true");

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId))
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

            ViewData["CategoryId"] = new SelectList(await _context.Categories.Where(c => c.IsActive == true).OrderBy(c => c.CategoryName).ToListAsync(), "CategoryId", "CategoryName", product.CategoryId);
            return View(product);
        }

        // GET: Admin/Product/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Admin/Product/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Product/BrowseImages
        public IActionResult BrowseImages()
        {
            var imagesFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
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
                    var fileUrl = "/images/products/" + fileName;
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

        // POST: Admin/Product/UploadImage
        [HttpPost]
        [Route("Admin/Product/UploadImage")]
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

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
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

                var fileUrl = "/images/products/" + uniqueFileName;
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

        // POST: Admin/Product/DeleteImage
        [HttpPost]
        [Route("Admin/Product/DeleteImage")]
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

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
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
    }
}


