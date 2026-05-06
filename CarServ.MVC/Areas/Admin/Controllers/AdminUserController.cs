using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using CarServ.MVC.Models.ViewModels;
using CarServ.MVC.Helpers;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class AdminUserController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<AdminUserController> _logger;

        public AdminUserController(CarServContext context, ILogger<AdminUserController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin/AdminUser
        public async Task<IActionResult> Index(string searchString, string roleFilter, string statusFilter)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentRole"] = roleFilter;
            ViewData["CurrentStatus"] = statusFilter;

            var adminUsers = _context.AdminUsers.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                adminUsers = adminUsers.Where(a =>
                    a.Username.Contains(searchString) ||
                    (a.FullName != null && a.FullName.Contains(searchString)) ||
                    (a.Email != null && a.Email.Contains(searchString)));
            }

            // Filter by role
            if (!string.IsNullOrEmpty(roleFilter))
            {
                adminUsers = adminUsers.Where(a => a.Role == roleFilter);
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                bool isActive = statusFilter == "active";
                adminUsers = adminUsers.Where(a => a.IsActive == isActive);
            }

            return View(await adminUsers.OrderByDescending(a => a.CreatedDate).ToListAsync());
        }

        // GET: Admin/AdminUser/Create
        public IActionResult Create()
        {
            ViewBag.Roles = new List<SelectListItem>
            {
                new SelectListItem { Text = "Admin", Value = "Admin" },
                new SelectListItem { Text = "Staff", Value = "Staff" }
            };
            return View();
        }

        // POST: Admin/AdminUser/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra username đã tồn tại chưa
                var existingUser = await _context.AdminUsers
                    .FirstOrDefaultAsync(a => a.Username == model.Username);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");
                    ViewBag.Roles = new List<SelectListItem>
                    {
                        new SelectListItem { Text = "Admin", Value = "Admin" },
                        new SelectListItem { Text = "Staff", Value = "Staff" }
                    };
                    return View(model);
                }

                // Hash password
                string passwordHash = AdminPasswordHelper.HashPassword(model.Password);

                var adminUser = new AdminUser
                {
                    Username = model.Username,
                    PasswordHash = passwordHash,
                    FullName = model.FullName,
                    Role = model.Role,
                    Email = model.Email,
                    Phone = model.Phone,
                    IsActive = model.IsActive,
                    CreatedDate = DateTime.Now,
                    LastLogin = null
                };

                _context.AdminUsers.Add(adminUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Admin user created: {Username}", model.Username);
                TempData["SuccessMessage"] = "Tài khoản quản trị viên đã được tạo thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Roles = new List<SelectListItem>
            {
                new SelectListItem { Text = "Admin", Value = "Admin" },
                new SelectListItem { Text = "Staff", Value = "Staff" }
            };
            return View(model);
        }

        // GET: Admin/AdminUser/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adminUser = await _context.AdminUsers.FindAsync(id);
            if (adminUser == null)
            {
                return NotFound();
            }

            ViewBag.Roles = new List<SelectListItem>
            {
                new SelectListItem { Text = "Admin", Value = "Admin", Selected = adminUser.Role == "Admin" },
                new SelectListItem { Text = "Staff", Value = "Staff", Selected = adminUser.Role == "Staff" }
            };

            var model = new AdminUserEditViewModel
            {
                Id = adminUser.Id,
                Username = adminUser.Username,
                FullName = adminUser.FullName,
                Role = adminUser.Role ?? "Admin",
                Email = adminUser.Email,
                Phone = adminUser.Phone,
                IsActive = adminUser.IsActive ?? true
            };

            return View(model);
        }

        // POST: Admin/AdminUser/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminUserEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var adminUser = await _context.AdminUsers.FindAsync(id);
                if (adminUser == null)
                {
                    return NotFound();
                }

                // Kiểm tra username đã tồn tại chưa (trừ chính nó)
                var existingUser = await _context.AdminUsers
                    .FirstOrDefaultAsync(a => a.Username == model.Username && a.Id != id);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");
                    ViewBag.Roles = new List<SelectListItem>
                    {
                        new SelectListItem { Text = "Admin", Value = "Admin", Selected = model.Role == "Admin" },
                        new SelectListItem { Text = "Staff", Value = "Staff", Selected = model.Role == "Staff" }
                    };
                    return View(model);
                }

                // Cập nhật thông tin
                adminUser.Username = model.Username;
                adminUser.FullName = model.FullName;
                adminUser.Role = model.Role;
                adminUser.Email = model.Email;
                adminUser.Phone = model.Phone;
                adminUser.IsActive = model.IsActive;

                // Nếu có password mới, hash và cập nhật
                if (!string.IsNullOrEmpty(model.Password))
                {
                    adminUser.PasswordHash = AdminPasswordHelper.HashPassword(model.Password);
                }

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Admin user updated: {Username}", model.Username);
                    TempData["SuccessMessage"] = "Thông tin tài khoản đã được cập nhật thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AdminUserExists(adminUser.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.Roles = new List<SelectListItem>
            {
                new SelectListItem { Text = "Admin", Value = "Admin", Selected = model.Role == "Admin" },
                new SelectListItem { Text = "Staff", Value = "Staff", Selected = model.Role == "Staff" }
            };
            return View(model);
        }

        // GET: Admin/AdminUser/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var adminUser = await _context.AdminUsers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (adminUser == null)
            {
                return NotFound();
            }

            return View(adminUser);
        }

        // POST: Admin/AdminUser/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var adminUser = await _context.AdminUsers.FindAsync(id);
            if (adminUser != null)
            {
                _context.AdminUsers.Remove(adminUser);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Admin user deleted: {Username}", adminUser.Username);
                TempData["SuccessMessage"] = "Tài khoản đã được xóa thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool AdminUserExists(int id)
        {
            return _context.AdminUsers.Any(e => e.Id == id);
        }
    }
}


