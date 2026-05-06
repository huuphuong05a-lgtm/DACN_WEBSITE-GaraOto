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
        public async Task<IActionResult> Index(string searchString, string categoryFilter)
        {
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

            return View(await services.ToListAsync());
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


