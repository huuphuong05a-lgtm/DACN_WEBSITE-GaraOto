using System.Security.Claims;
using CarServ.MVC.Helpers;
using CarServ.MVC.Models;
using CarServ.MVC.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class AccountController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(CarServContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var adminUser = await GetCurrentAdminUserAsync();
            if (adminUser == null)
            {
                return Challenge("AdminAuth");
            }

            return View(ToProfileViewModel(adminUser));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(AdminProfileViewModel model)
        {
            var adminUser = await GetCurrentAdminUserAsync();
            if (adminUser == null)
            {
                return Challenge("AdminAuth");
            }

            if (adminUser.Id != model.Id)
            {
                return Forbid();
            }

            var wantsPasswordChange = !string.IsNullOrWhiteSpace(model.CurrentPassword)
                || !string.IsNullOrWhiteSpace(model.NewPassword)
                || !string.IsNullOrWhiteSpace(model.ConfirmNewPassword);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Vui lòng nhập mật khẩu hiện tại.");
                }

                if (string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    ModelState.AddModelError(nameof(model.NewPassword), "Vui lòng nhập mật khẩu mới.");
                }

                if (string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
                {
                    ModelState.AddModelError(nameof(model.ConfirmNewPassword), "Vui lòng nhập lại mật khẩu mới.");
                }

                if (!string.IsNullOrWhiteSpace(model.CurrentPassword)
                    && (string.IsNullOrWhiteSpace(adminUser.PasswordHash)
                        || !AdminPasswordHelper.VerifyPassword(model.CurrentPassword, adminUser.PasswordHash)))
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Username = adminUser.Username;
                model.Role = adminUser.Role;
                model.IsActive = adminUser.IsActive;
                model.CreatedDate = adminUser.CreatedDate;
                model.LastLogin = adminUser.LastLogin;
                return View(model);
            }

            adminUser.FullName = model.FullName?.Trim();
            adminUser.Email = model.Email?.Trim();
            adminUser.Phone = model.Phone?.Trim();

            if (wantsPasswordChange && !string.IsNullOrWhiteSpace(model.NewPassword))
            {
                adminUser.PasswordHash = AdminPasswordHelper.HashPassword(model.NewPassword);
            }

            await _context.SaveChangesAsync();
            await RefreshAdminSignInAsync(adminUser);

            _logger.LogInformation("Admin profile updated: {Username}", adminUser.Username);
            TempData["SuccessMessage"] = wantsPasswordChange
                ? "Thông tin tài khoản và mật khẩu đã được cập nhật."
                : "Thông tin tài khoản đã được cập nhật.";

            return RedirectToAction(nameof(Profile));
        }

        private async Task<AdminUser?> GetCurrentAdminUserAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
            return int.TryParse(userIdValue, out var userId)
                ? await _context.AdminUsers.FirstOrDefaultAsync(a => a.Id == userId && a.IsActive == true)
                : null;
        }

        private static AdminProfileViewModel ToProfileViewModel(AdminUser adminUser)
        {
            return new AdminProfileViewModel
            {
                Id = adminUser.Id,
                Username = adminUser.Username,
                Role = adminUser.Role,
                FullName = adminUser.FullName,
                Email = adminUser.Email,
                Phone = adminUser.Phone,
                IsActive = adminUser.IsActive,
                CreatedDate = adminUser.CreatedDate,
                LastLogin = adminUser.LastLogin
            };
        }

        private async Task RefreshAdminSignInAsync(AdminUser adminUser)
        {
            var claims = new List<Claim>
            {
                new Claim("UserId", adminUser.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
                new Claim("Username", adminUser.Username),
                new Claim(ClaimTypes.Name, adminUser.FullName ?? adminUser.Username),
                new Claim("FullName", adminUser.FullName ?? adminUser.Username),
                new Claim("Role", adminUser.Role ?? AppConstants.AdminRole.Admin),
                new Claim(ClaimTypes.Role, adminUser.Role ?? AppConstants.AdminRole.Admin)
            };

            var identity = new ClaimsIdentity(claims, "AdminAuth");
            await HttpContext.SignInAsync("AdminAuth", new ClaimsPrincipal(identity));
        }
    }
}
