using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Controllers
{
    public class BookingController : Controller
    {
        private readonly CarServContext _context;

        public BookingController(CarServContext context)
        {
            _context = context;
        }

        // GET: Booking
        public async Task<IActionResult> Index(int? serviceId)
        {
            ViewData["Services"] = new SelectList(
                await _context.Services
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.ServiceName)
                    .ToListAsync(),
                "ServiceId",
                "ServiceName",
                serviceId);

            ViewData["Technicians"] = new SelectList(
                await _context.Technicians
                    .Where(t => t.IsActive == true)
                    .OrderBy(t => t.FullName)
                    .ToListAsync(),
                "TechnicianId",
                "FullName");

            if (serviceId.HasValue)
            {
                ViewData["SelectedService"] = await _context.Services
                    .FirstOrDefaultAsync(s => s.ServiceId == serviceId && s.IsActive);
            }

            return View();
        }

        // POST: Booking/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("CustomerName,CustomerEmail,CustomerPhone,ServiceId,TechnicianId,AppointmentDate,Notes,VehicleInfo")] Appointment appointment)
        {
            // Get customer ID from claims
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out int customerId))
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đặt lịch.";
                return RedirectToAction("Login", "Account");
            }

            // Get customer info from database
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
                return RedirectToAction("Login", "Account");
            }

            // Auto-fill customer information from logged-in user
            appointment.CustomerId = customerId;
            if (string.IsNullOrEmpty(appointment.CustomerName))
                appointment.CustomerName = customer.FullName;
            if (string.IsNullOrEmpty(appointment.CustomerEmail))
                appointment.CustomerEmail = customer.Email;
            if (string.IsNullOrEmpty(appointment.CustomerPhone))
                appointment.CustomerPhone = customer.Phone;

            if (ModelState.IsValid)
            {
                appointment.Status = "Pending";
                appointment.CreatedDate = DateTime.Now;
                appointment.UpdatedDate = DateTime.Now;

                // Generate appointment code
                var count = await _context.Appointments.CountAsync() + 1;
                appointment.AppointmentCode = "APT" + DateTime.Now.ToString("yyyyMMdd") + count.ToString("D4");

                _context.Add(appointment);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Đặt lịch thành công! Mã đặt lịch của bạn là: " + appointment.AppointmentCode;
                return RedirectToAction(nameof(Index));
            }

            ViewData["Services"] = new SelectList(
                await _context.Services
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.ServiceName)
                    .ToListAsync(),
                "ServiceId",
                "ServiceName",
                appointment.ServiceId);

            ViewData["Technicians"] = new SelectList(
                await _context.Technicians
                    .Where(t => t.IsActive == true)
                    .OrderBy(t => t.FullName)
                    .ToListAsync(),
                "TechnicianId",
                "FullName",
                appointment.TechnicianId);

            return View("Index", appointment);
        }
    }
}

