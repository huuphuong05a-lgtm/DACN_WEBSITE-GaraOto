using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class BannerController : Controller
    {
        private readonly CarServContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BannerController(CarServContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/Banner
        public async Task<IActionResult> Index(string searchString, string typeFilter, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentType"] = typeFilter;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["TitleSortParm"] = string.IsNullOrEmpty(sortOrder) ? "title_desc" : "";
            ViewData["SortOrderSortParm"] = sortOrder == "SortOrder" ? "sortorder_desc" : "SortOrder";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            var banners = _context.Banners.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                banners = banners.Where(b => 
                    (b.Title != null && b.Title.Contains(searchString)) ||
                    (b.Subtitle != null && b.Subtitle.Contains(searchString)) ||
                    (b.Description != null && b.Description.Contains(searchString)));
            }

            // Filter by type
            if (!string.IsNullOrEmpty(typeFilter))
            {
                banners = banners.Where(b => b.BannerType == typeFilter);
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                bool isActive = statusFilter == "active";
                banners = banners.Where(b => b.IsActive == isActive);
            }

            // Sort
            switch (sortOrder)
            {
                case "title_desc":
                    banners = banners.OrderByDescending(b => b.Title);
                    break;
                case "SortOrder":
                    banners = banners.OrderBy(b => b.SortOrder ?? 0);
                    break;
                case "sortorder_desc":
                    banners = banners.OrderByDescending(b => b.SortOrder ?? 0);
                    break;
                case "Date":
                    banners = banners.OrderBy(b => b.CreatedDate ?? DateTime.MinValue);
                    break;
                case "date_desc":
                    banners = banners.OrderByDescending(b => b.CreatedDate ?? DateTime.MinValue);
                    break;
                default:
                    banners = banners.OrderBy(b => b.SortOrder ?? 0).ThenByDescending(b => b.CreatedDate ?? DateTime.MinValue);
                    break;
            }

            var bannerList = await banners.ToListAsync();

            // Get banner types for filter dropdown
            var bannerTypes = await _context.Banners
                .Where(b => b.BannerType != null)
                .Select(b => b.BannerType)
                .Distinct()
                .ToListAsync();

            ViewBag.BannerTypes = bannerTypes;

            return View(bannerList);
        }

        // GET: Admin/Banner/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var banner = await _context.Banners
                .FirstOrDefaultAsync(m => m.BannerId == id);

            if (banner == null)
            {
                return NotFound();
            }

            return View(banner);
        }

        // GET: Admin/Banner/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Banner/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Subtitle,Description,ImageUrl,MobileImageUrl,LinkUrl,ButtonText,ButtonColor,SortOrder,IsActive,BannerType,StartDate,EndDate")] Banner banner)
        {
            if (ModelState.IsValid)
            {
                banner.CreatedDate = DateTime.Now;
                banner.UpdatedDate = DateTime.Now;
                banner.SortOrder ??= 0;
                banner.IsActive ??= true;
                banner.BannerType ??= "Homepage";
                banner.ButtonColor ??= "primary";

                // Handle checkbox
                var isActiveValues = Request.Form["IsActive"];
                banner.IsActive = isActiveValues.Contains("true");

                _context.Add(banner);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(banner);
        }

        // GET: Admin/Banner/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var banner = await _context.Banners.FindAsync(id);
            if (banner == null)
            {
                return NotFound();
            }

            return View(banner);
        }

        // POST: Admin/Banner/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BannerId,Title,Subtitle,Description,ImageUrl,MobileImageUrl,LinkUrl,ButtonText,ButtonColor,SortOrder,IsActive,BannerType,StartDate,EndDate,CreatedDate")] Banner banner)
        {
            if (id != banner.BannerId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Load existing banner
                    var existingBanner = await _context.Banners.FindAsync(id);
                    if (existingBanner == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingBanner.Title = banner.Title;
                    existingBanner.Subtitle = banner.Subtitle;
                    existingBanner.Description = banner.Description;
                    existingBanner.ImageUrl = banner.ImageUrl;
                    existingBanner.MobileImageUrl = banner.MobileImageUrl;
                    existingBanner.LinkUrl = banner.LinkUrl;
                    existingBanner.ButtonText = banner.ButtonText;
                    existingBanner.ButtonColor = banner.ButtonColor;
                    existingBanner.SortOrder = banner.SortOrder;
                    existingBanner.BannerType = banner.BannerType;
                    existingBanner.StartDate = banner.StartDate;
                    existingBanner.EndDate = banner.EndDate;
                    existingBanner.UpdatedDate = DateTime.Now;

                    // Handle checkbox
                    var isActiveValues = Request.Form["IsActive"];
                    existingBanner.IsActive = isActiveValues.Contains("true");

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BannerExists(banner.BannerId))
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

            return View(banner);
        }

        // GET: Admin/Banner/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var banner = await _context.Banners
                .FirstOrDefaultAsync(m => m.BannerId == id);

            if (banner == null)
            {
                return NotFound();
            }

            return View(banner);
        }

        // POST: Admin/Banner/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner != null)
            {
                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool BannerExists(int id)
        {
            return _context.Banners.Any(e => e.BannerId == id);
        }
    }
}

