using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class MenuController : Controller
    {
        private readonly CarServContext _context;

        public MenuController(CarServContext context)
        {
            _context = context;
        }

        // GET: Admin/Menu
        public async Task<IActionResult> Index(string searchString, string statusFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["SortOrderSortParm"] = sortOrder == "SortOrder" ? "sortorder_desc" : "SortOrder";

            var menus = _context.Menus.Include(m => m.Parent).AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                menus = menus.Where(m => m.MenuName.Contains(searchString) 
                    || (m.MenuUrl != null && m.MenuUrl.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                bool isActive = statusFilter == "active";
                menus = menus.Where(m => m.IsActive == isActive);
            }

            // Sort
            switch (sortOrder)
            {
                case "name_desc":
                    menus = menus.OrderByDescending(m => m.MenuName);
                    break;
                case "SortOrder":
                    menus = menus.OrderBy(m => m.SortOrder);
                    break;
                case "sortorder_desc":
                    menus = menus.OrderByDescending(m => m.SortOrder);
                    break;
                default:
                    menus = menus.OrderBy(m => m.SortOrder);
                    break;
            }

            var menuList = await menus.ToListAsync();
            return View(menuList);
        }

        // GET: Admin/Menu/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menu = await _context.Menus
                .Include(m => m.Parent)
                .Include(m => m.InverseParent)
                .FirstOrDefaultAsync(m => m.MenuId == id);

            if (menu == null)
            {
                return NotFound();
            }

            return View(menu);
        }

        // GET: Admin/Menu/Create
        public IActionResult Create()
        {
            ViewData["ParentId"] = new SelectList(_context.Menus.Where(m => m.ParentId == null), "MenuId", "MenuName");
            return View();
        }

        // POST: Admin/Menu/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MenuName,MenuUrl,ParentId,SortOrder,IconClass")] Menu menu)
        {
            if (ModelState.IsValid)
            {
                menu.CreatedDate = DateTime.Now;
                menu.UpdatedDate = DateTime.Now;
                // Xử lý checkbox: khi có hidden input và checkbox cùng tên
                // Nếu checkbox được check, sẽ có cả "false" và "true"
                // Nếu không check, chỉ có "false"
                var isActiveValues = Request.Form["IsActive"];
                menu.IsActive = isActiveValues.Contains("true");
                menu.SortOrder ??= 0;

                _context.Add(menu);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["ParentId"] = new SelectList(_context.Menus.Where(m => m.ParentId == null), "MenuId", "MenuName", menu.ParentId);
            return View(menu);
        }

        // GET: Admin/Menu/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menu = await _context.Menus.FindAsync(id);
            if (menu == null)
            {
                return NotFound();
            }

            // Không cho phép chọn chính nó làm parent
            ViewData["ParentId"] = new SelectList(_context.Menus.Where(m => m.ParentId == null && m.MenuId != id), "MenuId", "MenuName", menu.ParentId);
            return View(menu);
        }

        // POST: Admin/Menu/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MenuId,MenuName,MenuUrl,ParentId,SortOrder,IconClass,CreatedDate")] Menu menu)
        {
            if (id != menu.MenuId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Load entity từ database để đảm bảo tracking đúng
                    var existingMenu = await _context.Menus.FindAsync(id);
                    if (existingMenu == null)
                    {
                        return NotFound();
                    }

                    // Update các properties
                    existingMenu.MenuName = menu.MenuName;
                    existingMenu.MenuUrl = menu.MenuUrl;
                    existingMenu.ParentId = menu.ParentId;
                    existingMenu.SortOrder = menu.SortOrder;
                    existingMenu.IconClass = menu.IconClass;
                    existingMenu.UpdatedDate = DateTime.Now;
                    
                    // Xử lý checkbox: khi có hidden input và checkbox cùng tên
                    // Nếu checkbox được check, sẽ có cả "false" và "true"
                    // Nếu không check, chỉ có "false"
                    var isActiveValues = Request.Form["IsActive"];
                    existingMenu.IsActive = isActiveValues.Contains("true");
                    
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuExists(menu.MenuId))
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

            ViewData["ParentId"] = new SelectList(_context.Menus.Where(m => m.ParentId == null && m.MenuId != id), "MenuId", "MenuName", menu.ParentId);
            return View(menu);
        }

        // GET: Admin/Menu/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menu = await _context.Menus
                .Include(m => m.Parent)
                .Include(m => m.InverseParent)
                .FirstOrDefaultAsync(m => m.MenuId == id);

            if (menu == null)
            {
                return NotFound();
            }

            return View(menu);
        }

        // POST: Admin/Menu/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var menu = await _context.Menus
                .Include(m => m.InverseParent)
                .FirstOrDefaultAsync(m => m.MenuId == id);

            if (menu != null)
            {
                // Xóa các menu con trước
                if (menu.InverseParent.Any())
                {
                    _context.Menus.RemoveRange(menu.InverseParent);
                }
                _context.Menus.Remove(menu);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MenuExists(int id)
        {
            return _context.Menus.Any(e => e.MenuId == id);
        }
    }
}

