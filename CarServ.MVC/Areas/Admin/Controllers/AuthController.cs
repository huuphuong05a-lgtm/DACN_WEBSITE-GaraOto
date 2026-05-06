using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using CarServ.MVC.Models.ViewModels;
using CarServ.MVC.Helpers;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AllowAnonymous] // Cho phép truy cập không cần đăng nhập
    public class AuthController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(CarServContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin/Auth/Login
        public IActionResult Login()
        {
            // Kiểm tra đã đăng nhập với AdminAuth scheme
            // Chỉ redirect nếu đã đăng nhập với đúng scheme AdminAuth
            var adminAuthResult = HttpContext.AuthenticateAsync("AdminAuth").Result;
            if (adminAuthResult?.Succeeded == true)
            {
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            return View();
        }

        // POST: Admin/Auth/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Tìm admin user theo username
            var adminUser = await _context.AdminUsers
                .FirstOrDefaultAsync(a => a.Username == model.Username);

            // Kiểm tra admin user tồn tại
            if (adminUser == null)
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }

            // Kiểm tra password
            if (string.IsNullOrEmpty(adminUser.PasswordHash) || 
                !AdminPasswordHelper.VerifyPassword(model.Password, adminUser.PasswordHash))
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }

            // Kiểm tra tài khoản có bị khóa không
            if (adminUser.IsActive == false)
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.");
                return View(model);
            }

            // Tạo claims
            var claims = new List<Claim>
            {
                new Claim("UserId", adminUser.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
                new Claim("Username", adminUser.Username),
                new Claim(ClaimTypes.Name, adminUser.FullName ?? adminUser.Username),
                new Claim("FullName", adminUser.FullName ?? adminUser.Username),
                new Claim("Role", adminUser.Role ?? "Admin"),
                new Claim(ClaimTypes.Role, adminUser.Role ?? "Admin")
            };

            var claimsIdentity = new ClaimsIdentity(claims, "AdminAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            // Cập nhật LastLogin
            adminUser.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            // Đăng nhập
            await HttpContext.SignInAsync(
                "AdminAuth",
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation("Admin logged in: {Username}", model.Username);

            // Redirect về dashboard
            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        // POST: Admin/Auth/Logout
        [HttpPost]
        [IgnoreAntiforgeryToken] // Bỏ qua anti-forgery token để tránh lỗi 400 khi logout
        public async Task<IActionResult> Logout()
        {
            try
            {
                // Lưu username trước khi đăng xuất để log
                var username = User.Identity?.Name;
                
                // Xóa session
                HttpContext.Session.Clear();
                
                // Đăng xuất khỏi AdminAuth scheme
                await HttpContext.SignOutAsync("AdminAuth");
                
                _logger.LogInformation("Admin logged out: {Username}", username ?? "Unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during admin logout");
            }
            
            return RedirectToAction("Login", "Auth", new { area = "Admin" });
        }

        // GET: Admin/Auth/Logout (fallback for direct URL access)
        [HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> LogoutGet()
        {
            try
            {
                // Lưu username trước khi đăng xuất để log
                var username = User.Identity?.Name;
                
                // Xóa session
                HttpContext.Session.Clear();
                
                // Đăng xuất khỏi AdminAuth scheme
                await HttpContext.SignOutAsync("AdminAuth");
                
                _logger.LogInformation("Admin logged out via GET: {Username}", username ?? "Unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during admin logout via GET");
            }
            
            return RedirectToAction("Login", "Auth", new { area = "Admin" });
        }

        // GET: Admin/Auth/AccessDenied
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

