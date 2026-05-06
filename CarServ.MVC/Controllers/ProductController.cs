using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Controllers
{
    public class ProductController : Controller
    {
        private readonly CarServContext _context;

        public ProductController(CarServContext context)
        {
            _context = context;
        }

        // GET: Product
        public async Task<IActionResult> Index(string searchString, int? categoryId)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CategoryId"] = categoryId;

            var products = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p => p.ProductName.Contains(searchString) 
                    || (p.Description != null && p.Description.Contains(searchString))
                    || (p.Brand != null && p.Brand.Contains(searchString)));
            }

            // Filter by category
            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            // Get categories for filter dropdown
            ViewData["Categories"] = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.CategoryName)
                .ToListAsync();

            products = products.OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedDate);

            return View(await products.ToListAsync());
        }

        // GET: Product/Details/5
        public async Task<IActionResult> Details(int? id, string slug)
        {
            if (id == null && string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            Product? product = null;

            if (id.HasValue)
            {
                product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(m => m.ProductId == id && m.IsActive);
            }
            else if (!string.IsNullOrEmpty(slug))
            {
                product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(m => m.Slug == slug && m.IsActive);
            }

            if (product == null)
            {
                return NotFound();
            }

            // Increment view count
            product.ViewCount = (product.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            // Get related products
            ViewData["RelatedProducts"] = await _context.Products
                .Where(p => p.IsActive && p.ProductId != product.ProductId 
                    && (p.CategoryId == product.CategoryId || p.IsFeatured))
                .OrderByDescending(p => p.IsFeatured)
                .Take(4)
                .ToListAsync();

            // Get reviews (chỉ hiển thị đã duyệt)
            var reviews = await _context.Reviews
                .Where(r => r.ProductId == product.ProductId && r.IsApproved)
                .OrderByDescending(r => r.CreatedDate)
                .Take(10)
                .ToListAsync();

            ViewData["Reviews"] = reviews;
            ViewData["AverageRating"] = ReviewController.GetAverageRatingForProduct(_context, product.ProductId);
            ViewData["ReviewCount"] = ReviewController.GetReviewCountForProduct(_context, product.ProductId);

            return View(product);
        }
    }
}


