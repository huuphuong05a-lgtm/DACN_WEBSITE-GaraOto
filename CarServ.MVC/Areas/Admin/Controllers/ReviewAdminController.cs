using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class ReviewAdminController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<ReviewAdminController> _logger;

        public ReviewAdminController(CarServContext context, ILogger<ReviewAdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin/ReviewAdmin
        public async Task<IActionResult> Index(string searchString, string approvedFilter, string typeFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentApproved"] = approvedFilter;
            ViewData["CurrentType"] = typeFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["DateSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_desc" : "";
            ViewData["RatingSortParm"] = sortOrder == "Rating" ? "rating_desc" : "Rating";

            var reviews = _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.Service)
                .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                reviews = reviews.Where(r =>
                    r.CustomerName.Contains(searchString) ||
                    (r.Comment != null && r.Comment.Contains(searchString)));
            }

            // Filter by approved status
            if (!string.IsNullOrEmpty(approvedFilter))
            {
                bool isApproved = approvedFilter == "approved";
                reviews = reviews.Where(r => r.IsApproved == isApproved);
            }

            // Filter by type (Product or Service)
            if (!string.IsNullOrEmpty(typeFilter))
            {
                if (typeFilter == "Product")
                {
                    reviews = reviews.Where(r => r.ProductId != null);
                }
                else if (typeFilter == "Service")
                {
                    reviews = reviews.Where(r => r.ServiceId != null);
                }
            }

            // Sort
            switch (sortOrder)
            {
                case "date_desc":
                    reviews = reviews.OrderByDescending(r => r.CreatedDate);
                    break;
                case "Date":
                    reviews = reviews.OrderBy(r => r.CreatedDate);
                    break;
                case "rating_desc":
                    reviews = reviews.OrderByDescending(r => r.Rating);
                    break;
                case "Rating":
                    reviews = reviews.OrderBy(r => r.Rating);
                    break;
                default:
                    reviews = reviews.OrderByDescending(r => r.CreatedDate);
                    break;
            }

            return View(await reviews.ToListAsync());
        }

        // GET: Admin/ReviewAdmin/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var review = await _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (review == null)
            {
                return NotFound();
            }

            return View(review);
        }

        // POST: Admin/ReviewAdmin/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Review {ReviewId} approved by {User}", id, User.Identity?.Name);
            TempData["SuccessMessage"] = "Đã duyệt đánh giá thành công!";

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/ReviewAdmin/Unapprove/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unapprove(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Review {ReviewId} unapproved by {User}", id, User.Identity?.Name);
            TempData["SuccessMessage"] = "Đã hủy duyệt đánh giá!";

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/ReviewAdmin/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var review = await _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (review == null)
            {
                return NotFound();
            }

            return View(review);
        }

        // POST: Admin/ReviewAdmin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Review {ReviewId} deleted by {User}", id, User.Identity?.Name);
                TempData["SuccessMessage"] = "Xóa đánh giá thành công!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}


