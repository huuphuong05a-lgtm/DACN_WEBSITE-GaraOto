using System.Diagnostics;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarServ.MVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CarServContext _context;

        public HomeController(ILogger<HomeController> logger, CarServContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Get active banners for carousel
            var banners = await _context.Banners
                .Where(b => b.IsActive == true 
                    && (b.StartDate == null || b.StartDate <= DateTime.Now)
                    && (b.EndDate == null || b.EndDate >= DateTime.Now))
                .OrderBy(b => b.SortOrder)
                .ThenByDescending(b => b.CreatedDate)
                .Take(5)
                .ToListAsync();
            ViewData["Banners"] = banners;

            // Get featured services
            var services = await _context.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ThenByDescending(s => s.CreatedDate)
                .Take(6)
                .ToListAsync();
            ViewData["Services"] = services;

            // Get active technicians
            var technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .Take(4)
                .ToListAsync();
            ViewData["Technicians"] = technicians;

            return View();
        }

        public async Task<IActionResult> About()
        {
            // Get active technicians
            var technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .Take(6)
                .ToListAsync();
            ViewData["Technicians"] = technicians;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
