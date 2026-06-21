using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Controllers
{
    public class ServiceController : Controller
    {
        private readonly CarServContext _context;

        public ServiceController(CarServContext context)
        {
            _context = context;
        }

        // GET: Service
        public async Task<IActionResult> Index(string? searchString, string? keyword, string? categoryFilter, int page = 1)
        {
            const int pageSize = 6;
            searchString = string.IsNullOrWhiteSpace(searchString) ? keyword : searchString;
            page = Math.Max(page, 1);

            ViewData["CurrentFilter"] = searchString;
            ViewData["CategoryFilter"] = categoryFilter;

            var services = _context.Services.Where(s => s.IsActive).AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                services = services.Where(s => s.ServiceName.Contains(searchString) 
                    || (s.Description != null && s.Description.Contains(searchString)));
            }

            // Filter by category
            if (!string.IsNullOrEmpty(categoryFilter))
            {
                services = services.Where(s => s.ServiceCategory == categoryFilter);
            }

            // Get distinct categories for filter dropdown
            ViewData["Categories"] = await _context.Services
                .Where(s => s.IsActive && !string.IsNullOrEmpty(s.ServiceCategory))
                .Select(s => s.ServiceCategory)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            services = services.OrderBy(s => s.SortOrder).ThenByDescending(s => s.CreatedDate);

            var totalItems = await services.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            ViewData["CurrentPage"] = page;
            ViewData["PageSize"] = pageSize;
            ViewData["TotalItems"] = totalItems;
            ViewData["TotalPages"] = totalPages;

            var pagedServices = await services
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(pagedServices);
        }

        // GET: Service/Details/5
        public async Task<IActionResult> Details(int? id, string slug)
        {
            if (id == null && string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            Service? service = null;

            if (id.HasValue)
            {
                service = await _context.Services
                    .FirstOrDefaultAsync(m => m.ServiceId == id && m.IsActive);
            }
            else if (!string.IsNullOrEmpty(slug))
            {
                service = await _context.Services
                    .FirstOrDefaultAsync(m => m.Slug == slug && m.IsActive);
            }

            if (service == null)
            {
                return NotFound();
            }

            // Get related services
            ViewData["RelatedServices"] = await _context.Services
                .Where(s => s.IsActive && s.ServiceId != service.ServiceId 
                    && (s.ServiceCategory == service.ServiceCategory || s.IsFeatured))
                .OrderBy(s => s.SortOrder)
                .Take(4)
                .ToListAsync();

            // Get reviews (chỉ hiển thị đã duyệt)
            var reviews = await _context.Reviews
                .Include(r => r.Customer)
                .Where(r => r.ServiceId == service.ServiceId && r.IsApproved)
                .OrderByDescending(r => r.CreatedDate)
                .Take(10)
                .ToListAsync();

            ViewData["Reviews"] = reviews;
            ViewData["AverageRating"] = ReviewController.GetAverageRatingForService(_context, service.ServiceId);
            ViewData["ReviewCount"] = ReviewController.GetReviewCountForService(_context, service.ServiceId);

            return View(service);
        }
    }
}


