using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Controllers
{
    public class TechnicianController : Controller
    {
        private readonly CarServContext _context;

        public TechnicianController(CarServContext context)
        {
            _context = context;
        }

        // GET: Technician
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var technicians = _context.Technicians
                .Where(t => t.IsActive == true)
                .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                technicians = technicians.Where(t => t.FullName.Contains(searchString) 
                    || (t.Position != null && t.Position.Contains(searchString))
                    || (t.Skills != null && t.Skills.Contains(searchString)));
            }

            technicians = technicians.OrderBy(t => t.FullName);

            return View(await technicians.ToListAsync());
        }

        // GET: Technician/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var technician = await _context.Technicians
                .FirstOrDefaultAsync(m => m.TechnicianId == id && m.IsActive == true);

            if (technician == null)
            {
                return NotFound();
            }

            // Get other technicians
            ViewData["OtherTechnicians"] = await _context.Technicians
                .Where(t => t.IsActive == true && t.TechnicianId != technician.TechnicianId)
                .OrderBy(t => t.FullName)
                .Take(4)
                .ToListAsync();

            return View(technician);
        }
    }
}

