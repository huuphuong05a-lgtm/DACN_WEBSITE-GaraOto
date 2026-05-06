using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using CarServ.MVC.Models.ViewModels;
using System.Text.RegularExpressions;

namespace CarServ.MVC.Controllers
{
    public class BlogController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<BlogController> _logger;
        private const int PageSize = 9;

        public BlogController(CarServContext context, ILogger<BlogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Blog
        public async Task<IActionResult> Index(int? categoryId, string? search, int page = 1)
        {
            var query = _context.BlogPosts
                .Include(p => p.Category)
                .Where(p => p.IsPublished == true);

            // Filter by category
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

            // Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Title.Contains(search) || 
                    (p.ShortDescription != null && p.ShortDescription.Contains(search)) ||
                    (p.Content != null && p.Content.Contains(search)));
            }

            // Order by published date (newest first)
            query = query.OrderByDescending(p => p.PublishedDate ?? DateTime.MinValue).ThenByDescending(p => p.Id);

            // Get total count
            var totalPosts = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalPosts / (double)PageSize);

            // Pagination
            var posts = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(p => new BlogPostItemViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Thumbnail = p.Thumbnail,
                    ShortDescription = p.ShortDescription,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategoryId = p.CategoryId,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    PublishedDate = p.PublishedDate,
                    ViewCount = p.ViewCount
                })
                .ToListAsync();

            // Get all active categories
            var categories = await _context.BlogCategories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            // Get current category name
            string? currentCategoryName = null;
            if (categoryId.HasValue)
            {
                var currentCategory = await _context.BlogCategories.FindAsync(categoryId.Value);
                currentCategoryName = currentCategory?.Name;
            }

            // Get latest posts for sidebar
            var latestPosts = await _context.BlogPosts
                .Include(p => p.Category)
                .Where(p => p.IsPublished == true)
                .OrderByDescending(p => p.PublishedDate ?? DateTime.MinValue).ThenByDescending(p => p.Id)
                .Take(5)
                .Select(p => new BlogPostItemViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Thumbnail = p.Thumbnail,
                    PublishedDate = p.PublishedDate,
                    ViewCount = p.ViewCount
                })
                .ToListAsync();

            var viewModel = new BlogListViewModel
            {
                Posts = posts,
                Categories = categories,
                CurrentCategoryId = categoryId,
                CurrentCategoryName = currentCategoryName,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = PageSize,
                TotalPosts = totalPosts,
                SearchQuery = search
            };

            ViewData["LatestPosts"] = latestPosts;
            return View(viewModel);
        }

        // GET: Blog/Detail/{slug}
        public async Task<IActionResult> Detail(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            var post = await _context.BlogPosts
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished == true);

            if (post == null)
            {
                return NotFound();
            }

            // Increment view count
            post.ViewCount++;
            await _context.SaveChangesAsync();

            // Get related posts (same category, exclude current post)
            var relatedPosts = await _context.BlogPosts
                .Include(p => p.Category)
                .Where(p => p.IsPublished == true 
                    && p.Id != post.Id 
                    && (post.CategoryId == null || p.CategoryId == post.CategoryId))
                .OrderByDescending(p => p.PublishedDate ?? DateTime.MinValue).ThenByDescending(p => p.Id)
                .Take(5)
                .Select(p => new BlogPostItemViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Thumbnail = p.Thumbnail,
                    ShortDescription = p.ShortDescription,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    CategoryId = p.CategoryId,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    PublishedDate = p.PublishedDate,
                    ViewCount = p.ViewCount
                })
                .ToListAsync();

            // Get all active categories
            var categories = await _context.BlogCategories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            // Get latest posts for sidebar
            var latestPosts = await _context.BlogPosts
                .Include(p => p.Category)
                .Where(p => p.IsPublished == true && p.Id != post.Id)
                .OrderByDescending(p => p.PublishedDate ?? DateTime.MinValue).ThenByDescending(p => p.Id)
                .Take(5)
                .Select(p => new BlogPostItemViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Slug = p.Slug,
                    Thumbnail = p.Thumbnail,
                    PublishedDate = p.PublishedDate,
                    ViewCount = p.ViewCount
                })
                .ToListAsync();

            var viewModel = new BlogDetailViewModel
            {
                Post = post,
                RelatedPosts = relatedPosts,
                Categories = categories
            };

            ViewData["LatestPosts"] = latestPosts;
            return View(viewModel);
        }

        // Helper method to generate slug (similar to ProductController)
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
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}

