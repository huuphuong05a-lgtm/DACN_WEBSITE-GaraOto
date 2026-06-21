using CarServ.MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CarServ.MVC.Controllers
{
    public class BookingController : Controller
    {
        private const int WorkdayStartHour = 8;
        private const int WorkdayEndHour = 17;
        private const int LunchStartHour = 12;
        private const int LunchEndHour = 13;
        private const int SlotStepMinutes = 30;
        private const int DefaultServiceDurationMinutes = 60;

        private readonly CarServContext _context;

        public BookingController(CarServContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? serviceId, int? vehicleId)
        {
            var appointment = new Appointment
            {
                ServiceId = serviceId
            };

            if (vehicleId.HasValue)
            {
                var customerIdClaim = User.FindFirst("CustomerId");
                if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out var customerId))
                {
                    await LoadBookingOptionsAsync(serviceId, null, null);
                    return View(appointment);
                }

                var vehicle = await _context.Vehicles
                    .Include(v => v.Brand)
                    .Include(v => v.Model)
                    .FirstOrDefaultAsync(v => v.VehicleId == vehicleId.Value
                        && v.CustomerId == customerId
                        && v.IsActive == true);

                if (vehicle == null)
                {
                    return Forbid();
                }

                appointment.VehicleId = vehicle.VehicleId;
                appointment.VehicleInfo = BuildVehicleLabel(vehicle);
                ViewData["SelectedVehicleLabel"] = appointment.VehicleInfo;
            }

            await LoadBookingOptionsAsync(serviceId, null, appointment.VehicleId);
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("CustomerName,CustomerEmail,CustomerPhone,ServiceId,TechnicianId,VehicleId,AppointmentDate,Notes,VehicleInfo")] Appointment appointment)
        {
            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim == null || !int.TryParse(customerIdClaim.Value, out var customerId))
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đặt lịch.";
                return RedirectToAction("Login", "Account");
            }

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng.";
                return RedirectToAction("Login", "Account");
            }

            appointment.CustomerId = customerId;
            appointment.CustomerName = string.IsNullOrWhiteSpace(appointment.CustomerName) ? customer.FullName : appointment.CustomerName;
            appointment.CustomerEmail = string.IsNullOrWhiteSpace(appointment.CustomerEmail) ? customer.Email : appointment.CustomerEmail;
            appointment.CustomerPhone = string.IsNullOrWhiteSpace(appointment.CustomerPhone) ? customer.Phone : appointment.CustomerPhone;

            if (appointment.VehicleId.HasValue)
            {
                var vehicle = await _context.Vehicles
                    .Include(v => v.Brand)
                    .Include(v => v.Model)
                    .FirstOrDefaultAsync(v => v.VehicleId == appointment.VehicleId.Value && v.CustomerId == customerId);

                if (vehicle == null)
                {
                    ModelState.AddModelError(nameof(Appointment.VehicleId), "Vui lòng chọn xe hợp lệ.");
                }
                else if (string.IsNullOrWhiteSpace(appointment.VehicleInfo))
                {
                    appointment.VehicleInfo = BuildVehicleLabel(vehicle);
                }
            }

            var service = appointment.ServiceId.HasValue
                ? await _context.Services.FirstOrDefaultAsync(s => s.ServiceId == appointment.ServiceId.Value && s.IsActive)
                : null;

            if (service == null)
            {
                ModelState.AddModelError(nameof(Appointment.ServiceId), "Vui lòng chọn dịch vụ hợp lệ.");
            }

            if (appointment.AppointmentDate <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(Appointment.AppointmentDate), "Vui lòng chọn thời gian hẹn trong tương lai.");
            }

            var duration = service?.EstimatedDuration ?? DefaultServiceDurationMinutes;
            var appointmentEnd = appointment.AppointmentDate.AddMinutes(duration);
            if (!IsInsideWorkingTime(appointment.AppointmentDate, appointmentEnd))
            {
                ModelState.AddModelError(
                    nameof(Appointment.AppointmentDate),
                    "Vui lòng chọn khung giờ trong giờ làm việc 08:00-17:00 và tránh giờ nghỉ trưa 12:00-13:00.");
            }

            if (ModelState.IsValid && !await IsSlotAvailableAsync(appointment.AppointmentDate, duration, appointment.TechnicianId, null))
            {
                ModelState.AddModelError(nameof(Appointment.AppointmentDate), "Khung giờ này đã kín lịch. Vui lòng chọn khung giờ khác.");
            }

            if (!ModelState.IsValid)
            {
                await LoadBookingOptionsAsync(appointment.ServiceId, appointment.TechnicianId, appointment.VehicleId);
                return View("Index", appointment);
            }

            appointment.ServiceType = service!.ServiceName;
            appointment.EstimatedDuration = duration;
            appointment.TotalPrice = service.Price;
            appointment.Status = AppConstants.AppointmentStatus.Pending;
            appointment.CreatedDate = DateTime.Now;
            appointment.UpdatedDate = DateTime.Now;
            appointment.AppointmentCode = await GenerateAppointmentCodeAsync();

            _context.Add(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đặt lịch thành công! Mã đặt lịch của bạn là: " + appointment.AppointmentCode;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(DateTime date, int? serviceId, int? technicianId)
        {
            var duration = DefaultServiceDurationMinutes;

            if (serviceId.HasValue)
            {
                var service = await _context.Services
                    .Where(s => s.ServiceId == serviceId.Value && s.IsActive)
                    .Select(s => new { s.EstimatedDuration })
                    .FirstOrDefaultAsync();

                duration = service?.EstimatedDuration ?? duration;
            }

            var slots = await BuildAvailableSlotsAsync(date.Date, duration, technicianId, null);
            return Json(slots);
        }

        private async Task LoadBookingOptionsAsync(int? serviceId, int? technicianId, int? vehicleId)
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
                "FullName",
                technicianId);

            if (serviceId.HasValue)
            {
                ViewData["SelectedService"] = await _context.Services
                    .FirstOrDefaultAsync(s => s.ServiceId == serviceId && s.IsActive);
            }

            var customerIdClaim = User.FindFirst("CustomerId");
            if (customerIdClaim != null && int.TryParse(customerIdClaim.Value, out var customerId))
            {
                var vehicles = await _context.Vehicles
                    .Include(v => v.Brand)
                    .Include(v => v.Model)
                    .Where(v => v.CustomerId == customerId && v.IsActive == true)
                    .OrderBy(v => v.LicensePlate)
                    .ToListAsync();

                ViewData["Vehicles"] = new SelectList(
                    vehicles.Select(v => new { v.VehicleId, Label = BuildVehicleLabel(v) }),
                    "VehicleId",
                    "Label",
                    vehicleId);

                ViewData["VehicleLabels"] = vehicles.ToDictionary(v => v.VehicleId, BuildVehicleLabel);

                if (vehicleId.HasValue)
                {
                    var selectedVehicle = vehicles.FirstOrDefault(v => v.VehicleId == vehicleId.Value);
                    if (selectedVehicle != null)
                    {
                        ViewData["SelectedVehicleLabel"] = BuildVehicleLabel(selectedVehicle);
                    }
                }
            }
        }

        private static string BuildVehicleLabel(Vehicle vehicle)
        {
            var brand = vehicle.Brand?.BrandName;
            var model = vehicle.Model?.ModelName;
            var name = !string.IsNullOrWhiteSpace(vehicle.VehicleName) ? vehicle.VehicleName : string.Join(" ", new[] { brand, model }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return string.IsNullOrWhiteSpace(name)
                ? vehicle.LicensePlate
                : $"{vehicle.LicensePlate} - {name}";
        }

        private async Task<string> GenerateAppointmentCodeAsync()
        {
            var now = DateTime.Now;
            var count = await _context.Appointments.CountAsync(a => a.CreatedDate.HasValue && a.CreatedDate.Value.Date == now.Date) + 1;
            return $"APT{now:yyyyMMdd}{count:D4}";
        }

        private async Task<List<object>> BuildAvailableSlotsAsync(DateTime date, int durationMinutes, int? technicianId, int? excludeAppointmentId)
        {
            var slots = new List<object>();
            var start = date.AddHours(WorkdayStartHour);
            var end = date.AddHours(WorkdayEndHour);
            var lunchStart = date.AddHours(LunchStartHour);
            var lunchEnd = date.AddHours(LunchEndHour);
            var activeTechnicianCount = await GetActiveTechnicianCountAsync();

            for (var slotStart = start; slotStart.AddMinutes(durationMinutes) <= end; slotStart = slotStart.AddMinutes(SlotStepMinutes))
            {
                var slotEnd = slotStart.AddMinutes(durationMinutes);
                var crossesLunch = slotStart < lunchEnd && slotEnd > lunchStart;
                var isPast = slotStart <= DateTime.Now;
                var available = !crossesLunch && !isPast && await IsSlotAvailableAsync(slotStart, durationMinutes, technicianId, excludeAppointmentId);
                var remaining = available
                    ? await CountRemainingTechniciansAsync(slotStart, slotEnd, technicianId, activeTechnicianCount, excludeAppointmentId)
                    : 0;

                slots.Add(new
                {
                    value = slotStart.ToString("yyyy-MM-ddTHH:mm"),
                    label = $"{slotStart:HH:mm} - {slotEnd:HH:mm}",
                    available,
                    remaining
                });
            }

            return slots;
        }

        private async Task<bool> IsSlotAvailableAsync(DateTime slotStart, int durationMinutes, int? technicianId, int? excludeAppointmentId)
        {
            var slotEnd = slotStart.AddMinutes(durationMinutes);

            if (!IsInsideWorkingTime(slotStart, slotEnd) || slotStart <= DateTime.Now)
            {
                return false;
            }

            var activeTechnicianCount = await GetActiveTechnicianCountAsync();
            var overlappingCount = await CountBlockingAppointmentsAsync(slotStart, slotEnd, excludeAppointmentId);
            if (overlappingCount >= activeTechnicianCount)
            {
                return false;
            }

            if (technicianId.HasValue)
            {
                return !await HasTechnicianConflictAsync(slotStart, slotEnd, technicianId.Value, excludeAppointmentId);
            }

            return true;
        }

        private static bool IsInsideWorkingTime(DateTime slotStart, DateTime slotEnd)
        {
            var startsAfterOpening = slotStart.TimeOfDay >= TimeSpan.FromHours(WorkdayStartHour);
            var endsBeforeClosing = slotEnd.TimeOfDay <= TimeSpan.FromHours(WorkdayEndHour);
            var crossesLunch = slotStart.TimeOfDay < TimeSpan.FromHours(LunchEndHour)
                && slotEnd.TimeOfDay > TimeSpan.FromHours(LunchStartHour);

            return startsAfterOpening && endsBeforeClosing && !crossesLunch;
        }

        private async Task<int> CountRemainingTechniciansAsync(DateTime slotStart, DateTime slotEnd, int? technicianId, int activeTechnicianCount, int? excludeAppointmentId)
        {
            var overlappingCount = await CountBlockingAppointmentsAsync(slotStart, slotEnd, excludeAppointmentId);
            if (overlappingCount >= activeTechnicianCount)
            {
                return 0;
            }

            if (technicianId.HasValue)
            {
                return await HasTechnicianConflictAsync(slotStart, slotEnd, technicianId.Value, excludeAppointmentId) ? 0 : 1;
            }

            return Math.Max(activeTechnicianCount - overlappingCount, 0);
        }

        private async Task<int> GetActiveTechnicianCountAsync()
        {
            var activeTechnicianCount = await _context.Technicians.CountAsync(t => t.IsActive == true);
            return Math.Max(activeTechnicianCount, 1);
        }

        private Task<bool> HasTechnicianConflictAsync(DateTime slotStart, DateTime slotEnd, int technicianId, int? excludeAppointmentId)
        {
            return GetBlockingAppointments(slotStart, slotEnd, excludeAppointmentId)
                .AnyAsync(a => a.TechnicianId == technicianId);
        }

        private Task<int> CountBlockingAppointmentsAsync(DateTime slotStart, DateTime slotEnd, int? excludeAppointmentId)
        {
            return GetBlockingAppointments(slotStart, slotEnd, excludeAppointmentId).CountAsync();
        }

        private IQueryable<Appointment> GetBlockingAppointments(DateTime slotStart, DateTime slotEnd, int? excludeAppointmentId)
        {
            return _context.Appointments
                .Where(a => !excludeAppointmentId.HasValue || a.AppointmentId != excludeAppointmentId.Value)
                .Where(a => a.Status != AppConstants.AppointmentStatus.Canceled
                    && a.Status != AppConstants.AppointmentStatus.Completed
                    && a.Status != AppConstants.AppointmentStatus.NoShow)
                .Where(a => a.AppointmentDate < slotEnd
                    && a.AppointmentDate.AddMinutes(a.EstimatedDuration ?? DefaultServiceDurationMinutes) > slotStart);
        }
    }
}
