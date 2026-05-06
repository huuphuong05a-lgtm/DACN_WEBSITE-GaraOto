using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using CarServ.MVC.Helpers;

namespace CarServ.MVC.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly CarServContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(CarServContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Helper method to get current customer ID
        private int GetCurrentCustomerId()
        {
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim != null && int.TryParse(customerIdClaim.Value, out int customerId))
            {
                return customerId;
            }
            throw new UnauthorizedAccessException("Không tìm thấy thông tin khách hàng");
        }

        // GET: Customer/Profile
        public async Task<IActionResult> Profile()
        {
            var customerId = GetCurrentCustomerId();
            var customer = await _context.Customers.FindAsync(customerId);

            if (customer == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                Email = customer.Email ?? "",
                Phone = customer.Phone ?? "",
                Address = customer.Address ?? "",
                City = customer.City ?? "",
                District = customer.District ?? "",
                Ward = customer.Ward ?? "",
                DateOfBirth = customer.DateOfBirth,
                Gender = customer.Gender ?? "",
                Avatar = customer.Avatar ?? ""
            };

            return View(model);
        }

        // POST: Customer/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var customerId = GetCurrentCustomerId();
            var customer = await _context.Customers.FindAsync(customerId);

            if (customer == null)
            {
                return NotFound();
            }

            // Kiểm tra email trùng (nếu đổi email)
            if (customer.Email != model.Email && await _context.Customers.AnyAsync(c => c.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                return View(model);
            }

            // Kiểm tra phone trùng (nếu đổi phone)
            if (customer.Phone != model.Phone && !string.IsNullOrEmpty(model.Phone) 
                && await _context.Customers.AnyAsync(c => c.Phone == model.Phone))
            {
                ModelState.AddModelError("Phone", "Số điện thoại này đã được sử dụng.");
                return View(model);
            }

            // Cập nhật thông tin
            customer.FullName = model.FullName;
            customer.Email = model.Email;
            customer.Phone = model.Phone;
            customer.Address = model.Address;
            customer.City = model.City;
            customer.District = model.District;
            customer.Ward = model.Ward;
            customer.DateOfBirth = model.DateOfBirth;
            customer.Gender = model.Gender;
            customer.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Cập nhật Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
                new Claim(ClaimTypes.Name, customer.FullName),
                new Claim(ClaimTypes.Email, customer.Email ?? ""),
                new Claim("CustomerId", customer.CustomerId.ToString()),
                new Claim("FullName", customer.FullName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // POST: Customer/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin.";
                return RedirectToAction(nameof(Profile));
            }

            var customerId = GetCurrentCustomerId();
            var customer = await _context.Customers.FindAsync(customerId);

            if (customer == null)
            {
                return NotFound();
            }

            // Kiểm tra mật khẩu cũ
            if (string.IsNullOrEmpty(customer.PasswordHash) || string.IsNullOrEmpty(customer.Salt) ||
                !PasswordHelper.VerifyPassword(model.OldPassword, customer.PasswordHash, customer.Salt))
            {
                TempData["ErrorMessage"] = "Mật khẩu cũ không đúng.";
                return RedirectToAction(nameof(Profile));
            }

            // Cập nhật mật khẩu mới
            var (hash, salt) = PasswordHelper.HashPassword(model.NewPassword);
            customer.PasswordHash = hash;
            customer.Salt = salt;
            customer.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // GET: Customer/Orders
        public async Task<IActionResult> Orders()
        {
            var customerId = GetCurrentCustomerId();
            var orders = await _context.Orders
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: Customer/OrderDetails/5
        public async Task<IActionResult> OrderDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customerId = GetCurrentCustomerId();
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Customer/Appointments
        public async Task<IActionResult> Appointments()
        {
            var customerId = GetCurrentCustomerId();
            var appointments = await _context.Appointments
                .Include(a => a.Service)
                .Include(a => a.Technician)
                .Include(a => a.Vehicle)
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync();

            return View(appointments);
        }

        // POST: Customer/CancelAppointment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var customerId = GetCurrentCustomerId();
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == id && a.CustomerId == customerId);

            if (appointment == null)
            {
                return NotFound();
            }

            // Chỉ cho phép hủy nếu chưa hoàn thành
            if (appointment.Status == "Hoàn thành" || appointment.Status == "Đã hủy")
            {
                TempData["ErrorMessage"] = "Không thể hủy lịch hẹn này.";
                return RedirectToAction(nameof(Appointments));
            }

            appointment.Status = "Đã hủy";
            appointment.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã hủy lịch hẹn thành công.";
            return RedirectToAction(nameof(Appointments));
        }

        // GET: Customer/Vehicles
        public async Task<IActionResult> Vehicles()
        {
            var customerId = GetCurrentCustomerId();
            var vehicles = await _context.Vehicles
                .Include(v => v.Brand)
                .Include(v => v.Model)
                .Where(v => v.CustomerId == customerId)
                .OrderByDescending(v => v.CreatedDate)
                .ToListAsync();

            // Get brands and models for dropdown
            ViewBag.Brands = await _context.VehicleBrands
                .Where(b => b.IsActive == true)
                .OrderBy(b => b.BrandName)
                .ToListAsync();

            return View(vehicles);
        }

        // GET: Customer/AddVehicle
        public async Task<IActionResult> AddVehicle()
        {
            ViewBag.Brands = await _context.VehicleBrands
                .Where(b => b.IsActive == true)
                .OrderBy(b => b.BrandName)
                .ToListAsync();

            return View();
        }

        // POST: Customer/AddVehicle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVehicle(VehicleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Brands = await _context.VehicleBrands
                    .Where(b => b.IsActive == true)
                    .OrderBy(b => b.BrandName)
                    .ToListAsync();
                return View(model);
            }

            var customerId = GetCurrentCustomerId();

            // Kiểm tra biển số trùng
            if (await _context.Vehicles.AnyAsync(v => v.LicensePlate == model.LicensePlate))
            {
                ModelState.AddModelError("LicensePlate", "Biển số này đã được đăng ký.");
                ViewBag.Brands = await _context.VehicleBrands
                    .Where(b => b.IsActive == true)
                    .OrderBy(b => b.BrandName)
                    .ToListAsync();
                return View(model);
            }

            var vehicle = new Vehicle
            {
                CustomerId = customerId,
                BrandId = model.BrandId,
                ModelId = model.ModelId,
                LicensePlate = model.LicensePlate,
                VehicleName = model.VehicleName,
                Year = model.Year,
                Color = model.Color,
                ChassisNumber = model.ChassisNumber,
                EngineNumber = model.EngineNumber,
                RegistrationDate = model.RegistrationDate,
                FuelType = model.FuelType,
                Notes = model.Notes,
                IsActive = true,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm xe thành công!";
            return RedirectToAction(nameof(Vehicles));
        }

        // GET: Customer/EditVehicle/5
        public async Task<IActionResult> EditVehicle(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customerId = GetCurrentCustomerId();
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.VehicleId == id && v.CustomerId == customerId);

            if (vehicle == null)
            {
                return NotFound();
            }

            var model = new VehicleViewModel
            {
                VehicleId = vehicle.VehicleId,
                BrandId = vehicle.BrandId,
                ModelId = vehicle.ModelId,
                LicensePlate = vehicle.LicensePlate,
                VehicleName = vehicle.VehicleName,
                Year = vehicle.Year,
                Color = vehicle.Color,
                ChassisNumber = vehicle.ChassisNumber,
                EngineNumber = vehicle.EngineNumber,
                RegistrationDate = vehicle.RegistrationDate,
                FuelType = vehicle.FuelType,
                Notes = vehicle.Notes
            };

            ViewBag.Brands = await _context.VehicleBrands
                .Where(b => b.IsActive == true)
                .OrderBy(b => b.BrandName)
                .ToListAsync();

            if (vehicle.BrandId.HasValue)
            {
                ViewBag.Models = await _context.VehicleModels
                    .Where(m => m.BrandId == vehicle.BrandId && m.IsActive == true)
                    .OrderBy(m => m.ModelName)
                    .ToListAsync();
            }

            return View(model);
        }

        // POST: Customer/EditVehicle/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVehicle(int id, VehicleViewModel model)
        {
            if (id != model.VehicleId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Brands = await _context.VehicleBrands
                    .Where(b => b.IsActive == true)
                    .OrderBy(b => b.BrandName)
                    .ToListAsync();
                return View(model);
            }

            var customerId = GetCurrentCustomerId();
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.VehicleId == id && v.CustomerId == customerId);

            if (vehicle == null)
            {
                return NotFound();
            }

            // Kiểm tra biển số trùng (nếu đổi)
            if (vehicle.LicensePlate != model.LicensePlate 
                && await _context.Vehicles.AnyAsync(v => v.LicensePlate == model.LicensePlate))
            {
                ModelState.AddModelError("LicensePlate", "Biển số này đã được đăng ký.");
                ViewBag.Brands = await _context.VehicleBrands
                    .Where(b => b.IsActive == true)
                    .OrderBy(b => b.BrandName)
                    .ToListAsync();
                return View(model);
            }

            vehicle.BrandId = model.BrandId;
            vehicle.ModelId = model.ModelId;
            vehicle.LicensePlate = model.LicensePlate;
            vehicle.VehicleName = model.VehicleName;
            vehicle.Year = model.Year;
            vehicle.Color = model.Color;
            vehicle.ChassisNumber = model.ChassisNumber;
            vehicle.EngineNumber = model.EngineNumber;
            vehicle.RegistrationDate = model.RegistrationDate;
            vehicle.FuelType = model.FuelType;
            vehicle.Notes = model.Notes;
            vehicle.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật thông tin xe thành công!";
            return RedirectToAction(nameof(Vehicles));
        }

        // POST: Customer/DeleteVehicle/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            var customerId = GetCurrentCustomerId();
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.VehicleId == id && v.CustomerId == customerId);

            if (vehicle == null)
            {
                return NotFound();
            }

            // Kiểm tra xem xe có đang được sử dụng trong lịch hẹn không
            var hasAppointments = await _context.Appointments
                .AnyAsync(a => a.VehicleId == id && a.Status != "Đã hủy" && a.Status != "Hoàn thành");

            if (hasAppointments)
            {
                TempData["ErrorMessage"] = "Không thể xóa xe đang có lịch hẹn chưa hoàn thành.";
                return RedirectToAction(nameof(Vehicles));
            }

            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa xe thành công!";
            return RedirectToAction(nameof(Vehicles));
        }

        // GET: Customer/GetModels (AJAX)
        [HttpGet]
        public async Task<IActionResult> GetModels(int brandId)
        {
            var models = await _context.VehicleModels
                .Where(m => m.BrandId == brandId && m.IsActive == true)
                .OrderBy(m => m.ModelName)
                .Select(m => new { m.ModelId, m.ModelName })
                .ToListAsync();

            return Json(models);
        }
    }

    // ViewModels
    public class ProfileViewModel
    {
        public int CustomerId { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập email")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Địa chỉ")]
        public string Address { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Thành phố")]
        public string City { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Quận/Huyện")]
        public string District { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Phường/Xã")]
        public string Ward { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Ngày sinh")]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
        public DateOnly? DateOfBirth { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Giới tính")]
        public string Gender { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Ảnh đại diện")]
        public string Avatar { get; set; } = string.Empty;
    }

    public class ChangePasswordViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập mật khẩu cũ")]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Mật khẩu cũ")]
        public string OldPassword { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [System.ComponentModel.DataAnnotations.StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự.", MinimumLength = 6)]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Xác nhận mật khẩu")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class VehicleViewModel
    {
        public int? VehicleId { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Hãng xe")]
        public int? BrandId { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Dòng xe")]
        public int? ModelId { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập biển số")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Biển số")]
        [System.ComponentModel.DataAnnotations.StringLength(20)]
        public string LicensePlate { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Tên xe")]
        [System.ComponentModel.DataAnnotations.StringLength(200)]
        public string? VehicleName { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Năm sản xuất")]
        public int? Year { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Màu sắc")]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string? Color { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Số khung")]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string? ChassisNumber { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Số máy")]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string? EngineNumber { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Ngày đăng ký")]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Date)]
        public DateTime? RegistrationDate { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Loại nhiên liệu")]
        [System.ComponentModel.DataAnnotations.StringLength(50)]
        public string? FuelType { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Ghi chú")]
        [System.ComponentModel.DataAnnotations.StringLength(1000)]
        public string? Notes { get; set; }
    }
}

