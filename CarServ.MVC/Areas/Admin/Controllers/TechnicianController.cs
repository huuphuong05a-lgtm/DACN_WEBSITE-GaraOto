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
    public class TechnicianController : Controller
    {
        private readonly CarServContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TechnicianController(CarServContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/Technician
        public async Task<IActionResult> Index(string searchString, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            var technicians = _context.Technicians.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                technicians = technicians.Where(t => 
                    t.FullName.Contains(searchString)
                    || (t.Position != null && t.Position.Contains(searchString))
                    || (t.Phone != null && t.Phone.Contains(searchString))
                    || (t.Email != null && t.Email.Contains(searchString))
                    || (t.Skills != null && t.Skills.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "Active")
                {
                    technicians = technicians.Where(t => t.IsActive == true);
                }
                else if (statusFilter == "Inactive")
                {
                    technicians = technicians.Where(t => t.IsActive == false || t.IsActive == null);
                }
            }

            // Sort
            switch (sortOrder)
            {
                case "name_desc":
                    technicians = technicians.OrderByDescending(t => t.FullName);
                    break;
                case "Date":
                    technicians = technicians.OrderBy(t => t.CreatedDate);
                    break;
                case "date_desc":
                    technicians = technicians.OrderByDescending(t => t.CreatedDate);
                    break;
                case "rating_desc":
                    technicians = technicians.OrderByDescending(t => t.Rating);
                    break;
                default:
                    technicians = technicians.OrderByDescending(t => t.CreatedDate);
                    break;
            }

            var technicianList = await technicians.ToListAsync();
            return View(technicianList);
        }

        // GET: Admin/Technician/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var technician = await _context.Technicians
                .Include(t => t.Appointments)
                .FirstOrDefaultAsync(m => m.TechnicianId == id);

            if (technician == null)
            {
                return NotFound();
            }

            return View(technician);
        }

        // GET: Admin/Technician/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Technician/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Position,ExperienceYears,Phone,Email,ImageUrl,Bio,Skills,Rating,TotalReviews")] Technician technician)
        {
            // Handle checkbox manually
            technician.IsActive = Request.Form["IsActive"].Contains("true");

            if (ModelState.IsValid)
            {
                technician.CreatedDate = DateTime.Now;
                technician.UpdatedDate = DateTime.Now;

                _context.Add(technician);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(technician);
        }

        // GET: Admin/Technician/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var technician = await _context.Technicians.FindAsync(id);
            if (technician == null)
            {
                return NotFound();
            }

            return View(technician);
        }

        // POST: Admin/Technician/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TechnicianId,FullName,Position,ExperienceYears,Phone,Email,ImageUrl,Bio,Skills,Rating,TotalReviews,CreatedDate")] Technician technician)
        {
            if (id != technician.TechnicianId)
            {
                return NotFound();
            }

            // Handle checkbox manually
            technician.IsActive = Request.Form["IsActive"].Contains("true");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingTechnician = await _context.Technicians.FindAsync(id);
                    if (existingTechnician == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingTechnician.FullName = technician.FullName;
                    existingTechnician.Position = technician.Position;
                    existingTechnician.ExperienceYears = technician.ExperienceYears;
                    existingTechnician.Phone = technician.Phone;
                    existingTechnician.Email = technician.Email;
                    existingTechnician.ImageUrl = technician.ImageUrl;
                    existingTechnician.Bio = technician.Bio;
                    existingTechnician.Skills = technician.Skills;
                    existingTechnician.Rating = technician.Rating;
                    existingTechnician.TotalReviews = technician.TotalReviews;
                    existingTechnician.IsActive = technician.IsActive;
                    existingTechnician.UpdatedDate = DateTime.Now;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TechnicianExists(technician.TechnicianId))
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

            return View(technician);
        }

        // GET: Admin/Technician/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var technician = await _context.Technicians
                .Include(t => t.Appointments)
                .FirstOrDefaultAsync(m => m.TechnicianId == id);

            if (technician == null)
            {
                return NotFound();
            }

            return View(technician);
        }

        // POST: Admin/Technician/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var technician = await _context.Technicians
                .Include(t => t.Appointments)
                .FirstOrDefaultAsync(t => t.TechnicianId == id);

            if (technician != null)
            {
                // Check if technician has appointments
                if (technician.Appointments != null && technician.Appointments.Any())
                {
                    TempData["ErrorMessage"] = "Không thể xóa kỹ thuật viên này vì đã có lịch hẹn liên quan.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }

                _context.Technicians.Remove(technician);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Technician/BrowseImages
        public IActionResult BrowseImages()
        {
            var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "technicians");
            var images = new List<object>();

            if (Directory.Exists(imagePath))
            {
                var files = Directory.GetFiles(imagePath)
                    .Where(f => f.ToLower().EndsWith(".jpg") || f.ToLower().EndsWith(".jpeg") || f.ToLower().EndsWith(".png") || f.ToLower().EndsWith(".gif"))
                    .OrderBy(f => f);

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var fileInfo = new FileInfo(file);
                    images.Add(new
                    {
                        url = $"/images/technicians/{fileName}",
                        name = fileName,
                        size = fileInfo.Length
                    });
                }
            }

            return Json(images);
        }

        private bool TechnicianExists(int id)
        {
            return _context.Technicians.Any(e => e.TechnicianId == id);
        }
    }
}

