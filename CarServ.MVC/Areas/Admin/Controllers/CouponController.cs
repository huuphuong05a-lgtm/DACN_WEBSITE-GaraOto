using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class CouponController : Controller
    {
        private readonly CarServContext _context;

        public CouponController(CarServContext context)
        {
            _context = context;
        }

        // GET: Admin/Coupon
        public async Task<IActionResult> Index(string searchString, string statusFilter, string typeFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentType"] = typeFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["DateSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_desc" : "";

            var coupons = _context.Coupons.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                coupons = coupons.Where(c => 
                    c.Code.Contains(searchString)
                    || (c.Name != null && c.Name.Contains(searchString))
                    || (c.Description != null && c.Description.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "Active")
                {
                    coupons = coupons.Where(c => c.IsActive == true);
                }
                else if (statusFilter == "Inactive")
                {
                    coupons = coupons.Where(c => c.IsActive == false || c.IsActive == null);
                }
                else if (statusFilter == "Expired")
                {
                    coupons = coupons.Where(c => c.EndDate.HasValue && c.EndDate.Value < DateTime.Now);
                }
                else if (statusFilter == "Valid")
                {
                    coupons = coupons.Where(c => 
                        c.IsActive == true 
                        && (!c.StartDate.HasValue || c.StartDate.Value <= DateTime.Now)
                        && (!c.EndDate.HasValue || c.EndDate.Value >= DateTime.Now));
                }
            }

            // Filter by discount type
            if (!string.IsNullOrEmpty(typeFilter))
            {
                coupons = coupons.Where(c => c.DiscountType == typeFilter);
            }

            // Sort
            switch (sortOrder)
            {
                case "date_desc":
                    coupons = coupons.OrderByDescending(c => c.CreatedDate);
                    break;
                case "code":
                    coupons = coupons.OrderBy(c => c.Code);
                    break;
                case "code_desc":
                    coupons = coupons.OrderByDescending(c => c.Code);
                    break;
                default:
                    coupons = coupons.OrderByDescending(c => c.CreatedDate);
                    break;
            }

            var couponList = await coupons.ToListAsync();

            // Get discount types for filter
            var discountTypes = await _context.Coupons
                .Where(c => c.DiscountType != null)
                .Select(c => c.DiscountType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            ViewBag.DiscountTypes = discountTypes;

            return View(couponList);
        }

        // GET: Admin/Coupon/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(m => m.CouponId == id);

            if (coupon == null)
            {
                return NotFound();
            }

            return View(coupon);
        }

        // GET: Admin/Coupon/Create
        public IActionResult Create()
        {
            // Discount type options
            var discountTypeList = new[] { "Percentage", "Fixed" };
            ViewBag.DiscountTypeList = discountTypeList;

            return View();
        }

        // POST: Admin/Coupon/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name,Description,DiscountType,DiscountValue,MinimumOrder,MaximumDiscount,UsageLimit,StartDate,EndDate")] Coupon coupon)
        {
            // Handle checkbox manually
            coupon.IsActive = Request.Form["IsActive"].Contains("true");

            // Check if code already exists
            if (await _context.Coupons.AnyAsync(c => c.Code == coupon.Code))
            {
                ModelState.AddModelError("Code", "Mã giảm giá này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                coupon.UsedCount = 0;
                coupon.CreatedDate = DateTime.Now;

                _context.Add(coupon);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var discountTypeList = new[] { "Percentage", "Fixed" };
            ViewBag.DiscountTypeList = discountTypeList;

            return View(coupon);
        }

        // GET: Admin/Coupon/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null)
            {
                return NotFound();
            }

            // Discount type options
            var discountTypeList = new[] { "Percentage", "Fixed" };
            ViewBag.DiscountTypeList = discountTypeList;

            return View(coupon);
        }

        // POST: Admin/Coupon/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CouponId,Code,Name,Description,DiscountType,DiscountValue,MinimumOrder,MaximumDiscount,UsageLimit,UsedCount,StartDate,EndDate,CreatedDate")] Coupon coupon)
        {
            if (id != coupon.CouponId)
            {
                return NotFound();
            }

            // Handle checkbox manually
            coupon.IsActive = Request.Form["IsActive"].Contains("true");

            // Check if code already exists (excluding current coupon)
            if (await _context.Coupons.AnyAsync(c => c.Code == coupon.Code && c.CouponId != coupon.CouponId))
            {
                ModelState.AddModelError("Code", "Mã giảm giá này đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCoupon = await _context.Coupons.FindAsync(id);
                    if (existingCoupon == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingCoupon.Code = coupon.Code;
                    existingCoupon.Name = coupon.Name;
                    existingCoupon.Description = coupon.Description;
                    existingCoupon.DiscountType = coupon.DiscountType;
                    existingCoupon.DiscountValue = coupon.DiscountValue;
                    existingCoupon.MinimumOrder = coupon.MinimumOrder;
                    existingCoupon.MaximumDiscount = coupon.MaximumDiscount;
                    existingCoupon.UsageLimit = coupon.UsageLimit;
                    existingCoupon.UsedCount = coupon.UsedCount;
                    existingCoupon.StartDate = coupon.StartDate;
                    existingCoupon.EndDate = coupon.EndDate;
                    existingCoupon.IsActive = coupon.IsActive;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CouponExists(coupon.CouponId))
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

            var discountTypeList = new[] { "Percentage", "Fixed" };
            ViewBag.DiscountTypeList = discountTypeList;

            return View(coupon);
        }

        // GET: Admin/Coupon/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(m => m.CouponId == id);

            if (coupon == null)
            {
                return NotFound();
            }

            return View(coupon);
        }

        // POST: Admin/Coupon/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon != null)
            {
                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CouponExists(int id)
        {
            return _context.Coupons.Any(e => e.CouponId == id);
        }
    }
}


