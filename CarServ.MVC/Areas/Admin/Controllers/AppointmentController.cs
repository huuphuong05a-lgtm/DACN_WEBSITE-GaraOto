using CarServ.MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminStaffOrTechnician)]
    public class AppointmentController : Controller
    {
        private const int WorkdayStartHour = 8;
        private const int WorkdayEndHour = 17;
        private const int LunchStartHour = 12;
        private const int LunchEndHour = 13;
        private const int SlotStepMinutes = 30;
        private const int DefaultServiceDurationMinutes = 60;

        private readonly CarServContext _context;

        public AppointmentController(CarServContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchString, string statusFilter, string dateFilter, string sortOrder)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentDate"] = dateFilter;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["DateSortParm"] = string.IsNullOrEmpty(sortOrder) ? "date_desc" : "";

            var appointments = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Technician)
                .Include(a => a.Service)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Brand)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Model)
                .AsQueryable();

            appointments = await ApplyTechnicianScopeAsync(appointments);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                appointments = appointments.Where(a =>
                    (a.AppointmentCode != null && a.AppointmentCode.Contains(searchString))
                    || (a.CustomerName != null && a.CustomerName.Contains(searchString))
                    || (a.CustomerPhone != null && a.CustomerPhone.Contains(searchString))
                    || (a.CustomerEmail != null && a.CustomerEmail.Contains(searchString))
                    || (a.ServiceType != null && a.ServiceType.Contains(searchString)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                appointments = appointments.Where(a => a.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(dateFilter) && DateTime.TryParse(dateFilter, out var filterDate))
            {
                appointments = appointments.Where(a => a.AppointmentDate.Date == filterDate.Date);
            }

            appointments = sortOrder == "date_desc"
                ? appointments.OrderByDescending(a => a.AppointmentDate)
                : appointments.OrderByDescending(a => a.AppointmentId);

            ViewBag.StatusList = AppConstants.AppointmentStatus.All;
            return View(await appointments.ToListAsync());
        }

        public async Task<IActionResult> Calendar(DateTime? month, int? technicianId, string statusFilter)
        {
            var selectedMonth = month?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            selectedMonth = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var nextMonth = selectedMonth.AddMonths(1);

            var appointments = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Technician)
                .Include(a => a.Service)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Brand)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Model)
                .Where(a => a.AppointmentDate >= selectedMonth && a.AppointmentDate < nextMonth)
                .AsQueryable();

            if (User.IsInRole(AppConstants.AdminRole.Technician))
            {
                var currentTechnicianId = await GetCurrentTechnicianIdAsync();
                if (!currentTechnicianId.HasValue)
                {
                    TempData["ErrorMessage"] = "Tài khoản kỹ thuật viên chưa được liên kết với hồ sơ kỹ thuật viên.";
                    appointments = appointments.Where(a => false);
                    technicianId = null;
                }
                else
                {
                    technicianId = currentTechnicianId.Value;
                    appointments = appointments.Where(a => a.TechnicianId == currentTechnicianId.Value);
                }
            }

            if (technicianId.HasValue)
            {
                appointments = appointments.Where(a => a.TechnicianId == technicianId.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                appointments = appointments.Where(a => a.Status == statusFilter);
            }

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.PreviousMonth = selectedMonth.AddMonths(-1);
            ViewBag.NextMonth = selectedMonth.AddMonths(1);
            ViewBag.CurrentTechnicianId = technicianId;
            ViewBag.CurrentStatus = statusFilter;
            ViewBag.StatusList = AppConstants.AppointmentStatus.All;
            ViewBag.Technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .Select(t => new SelectListItem
                {
                    Value = t.TechnicianId.ToString(),
                    Text = t.FullName
                })
                .ToListAsync();

            return View(await appointments.OrderBy(a => a.AppointmentDate).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Technician)
                .Include(a => a.Service)
                .Include(a => a.ServiceHistories)
                    .ThenInclude(h => h.Invoices)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Brand)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Model)
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment != null && !await CurrentTechnicianCanAccessAppointmentAsync(appointment))
            {
                return Forbid();
            }

            return appointment == null ? NotFound() : View(appointment);
        }

        [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
        public async Task<IActionResult> Create([Bind("CustomerId,TechnicianId,VehicleId,ServiceId,ServiceType,AppointmentDate,EstimatedDuration,Notes,Status,TotalPrice,CustomerName,CustomerPhone,CustomerEmail,VehicleInfo")] Appointment appointment)
        {
            await EnrichAppointmentAsync(appointment);
            await ValidateAppointmentSlotAsync(appointment, null);

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return View(appointment);
            }

            appointment.AppointmentCode = await GenerateAppointmentCodeAsync();
            appointment.Status = string.IsNullOrWhiteSpace(appointment.Status)
                ? AppConstants.AppointmentStatus.Pending
                : appointment.Status;
            appointment.CreatedDate = DateTime.Now;
            appointment.UpdatedDate = DateTime.Now;

            _context.Add(appointment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            await LoadDropdownsAsync();
            return View(appointment);
        }

        public async Task<IActionResult> Complete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Technician)
                .Include(a => a.Service)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Brand)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Model)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            if (!await CurrentTechnicianCanAccessAppointmentAsync(appointment))
            {
                return Forbid();
            }

            if (!appointment.VehicleId.HasValue)
            {
                TempData["ErrorMessage"] = "Lịch hẹn cần gắn với xe trước khi tạo lịch sử sửa chữa.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (IsClosedWithoutCompletion(appointment.Status))
            {
                TempData["ErrorMessage"] = "Không thể hoàn thành lịch hẹn đã hủy hoặc khách không đến.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var history = await _context.ServiceHistories
                .FirstOrDefaultAsync(h => h.AppointmentId == appointment.AppointmentId);

            var model = new CompleteAppointmentViewModel
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentCode = appointment.AppointmentCode,
                CustomerName = appointment.Customer?.FullName ?? appointment.CustomerName,
                VehicleInfo = appointment.Vehicle != null
                    ? $"{appointment.Vehicle.LicensePlate} - {appointment.Vehicle.Brand?.BrandName} {appointment.Vehicle.Model?.ModelName}".Trim()
                    : appointment.VehicleInfo,
                ServiceName = appointment.Service?.ServiceName ?? appointment.ServiceType,
                TechnicianName = appointment.Technician?.FullName,
                AppointmentDate = appointment.AppointmentDate,
                ActualDuration = appointment.ActualDuration ?? appointment.EstimatedDuration,
                Mileage = history?.Mileage ?? appointment.Vehicle?.Mileage,
                LaborCost = history?.LaborCost ?? appointment.TotalPrice,
                LaborCostInput = FormatMoney(history?.LaborCost ?? appointment.TotalPrice),
                PartsCost = history?.PartsCost,
                PartsCostInput = FormatMoney(history?.PartsCost),
                TotalCost = history?.TotalCost ?? appointment.TotalPrice,
                TotalCostInput = FormatMoney(history?.TotalCost ?? appointment.TotalPrice),
                PartsReplaced = history?.PartsReplaced,
                Description = history?.Description ?? appointment.Notes,
                Notes = history?.Notes,
                NextServiceDate = history?.NextServiceDate ?? appointment.AppointmentDate.AddMonths(6),
                NextServiceMileage = history?.NextServiceMileage ?? (appointment.Vehicle?.Mileage.HasValue == true ? appointment.Vehicle.Mileage.Value + 5000 : null),
                WarrantyExpiryDate = history?.WarrantyExpiryDate
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(CompleteAppointmentViewModel model)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(a => a.AppointmentId == model.AppointmentId);

            if (appointment == null)
            {
                return NotFound();
            }

            if (!await CurrentTechnicianCanAccessAppointmentAsync(appointment))
            {
                return Forbid();
            }

            if (!appointment.VehicleId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Lịch hẹn cần gắn với xe trước khi tạo lịch sử sửa chữa.");
            }

            if (IsClosedWithoutCompletion(appointment.Status))
            {
                ModelState.AddModelError(string.Empty, "Không thể hoàn thành lịch hẹn đã hủy hoặc khách không đến.");
            }

            model.LaborCost = ParseMoney(model.LaborCostInput);
            model.PartsCost = ParseMoney(model.PartsCostInput);
            model.TotalCost = ParseMoney(model.TotalCostInput);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var totalCost = model.TotalCost ?? ((model.LaborCost ?? 0) + (model.PartsCost ?? 0));
            var history = await _context.ServiceHistories
                .FirstOrDefaultAsync(h => h.AppointmentId == appointment.AppointmentId);

            if (history == null)
            {
                history = new ServiceHistory
                {
                    AppointmentId = appointment.AppointmentId,
                    VehicleId = appointment.VehicleId!.Value,
                    ServiceCode = "SVH" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    CreatedBy = User.Identity?.Name,
                    CreatedDate = DateTime.Now
                };
                _context.ServiceHistories.Add(history);
            }

            history.ServiceId = appointment.ServiceId;
            history.TechnicianId = appointment.TechnicianId;
            history.ServiceDate = appointment.AppointmentDate;
            history.ServiceName = appointment.ServiceType;
            history.Description = model.Description;
            history.PartsReplaced = model.PartsReplaced;
            history.LaborCost = model.LaborCost;
            history.PartsCost = model.PartsCost;
            history.TotalCost = totalCost;
            history.Mileage = model.Mileage;
            history.NextServiceDate = model.NextServiceDate;
            history.NextServiceMileage = model.NextServiceMileage;
            history.WarrantyExpiryDate = model.WarrantyExpiryDate;
            history.Status = AppConstants.AppointmentStatus.Completed;
            history.Notes = model.Notes;
            history.UpdatedDate = DateTime.Now;

            appointment.Status = AppConstants.AppointmentStatus.Completed;
            appointment.ActualDuration = model.ActualDuration;
            appointment.TotalPrice = totalCost;
            appointment.UpdatedDate = DateTime.Now;

            await EnsureInvoiceForServiceHistoryAsync(appointment, history, totalCost);

            if (appointment.Vehicle != null && model.Mileage.HasValue)
            {
                appointment.Vehicle.Mileage = model.Mileage;
                appointment.Vehicle.UpdatedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã hoàn thành lịch hẹn và cập nhật lịch sử sửa chữa.";
            return RedirectToAction(nameof(Details), new { id = appointment.AppointmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
        public async Task<IActionResult> Edit(int id, [Bind("AppointmentId,AppointmentCode,CustomerId,TechnicianId,VehicleId,ServiceId,ServiceType,AppointmentDate,EstimatedDuration,ActualDuration,Notes,Status,TotalPrice,CustomerName,CustomerPhone,CustomerEmail,VehicleInfo,CreatedDate")] Appointment appointment)
        {
            if (id != appointment.AppointmentId)
            {
                return NotFound();
            }

            var existingAppointment = await _context.Appointments.FindAsync(id);
            if (existingAppointment == null)
            {
                return NotFound();
            }

            if (existingAppointment.Status != AppConstants.AppointmentStatus.Completed
                && appointment.Status == AppConstants.AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Vui lòng sử dụng màn hình Hoàn thành để nhập kết quả sửa chữa.";
                return RedirectToAction(nameof(Complete), new { id });
            }

            await EnrichAppointmentAsync(appointment);
            var keepsExistingPastSlot = existingAppointment.AppointmentDate == appointment.AppointmentDate
                && appointment.AppointmentDate <= DateTime.Now;
            var isClosingAppointment = appointment.Status == AppConstants.AppointmentStatus.Completed
                || appointment.Status == AppConstants.AppointmentStatus.Canceled
                || appointment.Status == AppConstants.AppointmentStatus.NoShow;

            await ValidateAppointmentSlotAsync(appointment, id, keepsExistingPastSlot || isClosingAppointment);

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return View(appointment);
            }

            existingAppointment.AppointmentCode = string.IsNullOrWhiteSpace(existingAppointment.AppointmentCode)
                ? await GenerateAppointmentCodeAsync()
                : existingAppointment.AppointmentCode;
            existingAppointment.CustomerId = appointment.CustomerId;
            existingAppointment.TechnicianId = appointment.TechnicianId;
            existingAppointment.VehicleId = appointment.VehicleId;
            existingAppointment.ServiceId = appointment.ServiceId;
            existingAppointment.ServiceType = appointment.ServiceType;
            existingAppointment.AppointmentDate = appointment.AppointmentDate;
            existingAppointment.EstimatedDuration = appointment.EstimatedDuration;
            existingAppointment.ActualDuration = appointment.ActualDuration;
            existingAppointment.Notes = appointment.Notes;
            existingAppointment.Status = appointment.Status;
            existingAppointment.TotalPrice = appointment.TotalPrice;
            existingAppointment.CustomerName = appointment.CustomerName;
            existingAppointment.CustomerPhone = appointment.CustomerPhone;
            existingAppointment.CustomerEmail = appointment.CustomerEmail;
            existingAppointment.VehicleInfo = appointment.VehicleInfo;
            existingAppointment.UpdatedDate = DateTime.Now;

            await EnsureServiceHistoryForCompletedAppointmentAsync(existingAppointment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            if (!AppConstants.AppointmentStatus.All.Contains(status))
            {
                return BadRequest();
            }

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            if (status == AppConstants.AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Vui lòng sử dụng màn hình Hoàn thành để nhập kết quả sửa chữa.";
                return RedirectToAction(nameof(Complete), new { id });
            }

            if (!await CurrentTechnicianCanAccessAppointmentAsync(appointment))
            {
                return Forbid();
            }

            if (User.IsInRole(AppConstants.AdminRole.Technician))
            {
                if (!IsTechnicianWorkStatus(status)
                    || appointment.Status == AppConstants.AppointmentStatus.Completed
                    || appointment.Status == AppConstants.AppointmentStatus.Canceled
                    || appointment.Status == AppConstants.AppointmentStatus.NoShow)
                {
                    return Forbid();
                }
            }

            appointment.Status = status;
            appointment.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật trạng thái lịch hẹn.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Technician)
                .Include(a => a.Service)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Brand)
                .Include(a => a.Vehicle!)
                    .ThenInclude(v => v.Model)
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            return appointment == null ? NotFound() : View(appointment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(AuthenticationSchemes = "AdminAuth", Roles = AppConstants.AdminRole.AdminOrStaff)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(DateTime date, int? serviceId, int? technicianId, int? excludeAppointmentId)
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

            var slots = await BuildAvailableSlotsAsync(date.Date, duration, technicianId, excludeAppointmentId);
            return Json(slots);
        }

        private static bool IsTechnicianWorkStatus(string? status)
        {
            return status == AppConstants.AppointmentStatus.Assigned
                || status == AppConstants.AppointmentStatus.Inspecting
                || status == AppConstants.AppointmentStatus.Repairing
                || status == AppConstants.AppointmentStatus.WaitingParts;
        }

        private static bool IsClosedWithoutCompletion(string? status)
        {
            return status == AppConstants.AppointmentStatus.Canceled
                || status == AppConstants.AppointmentStatus.NoShow;
        }

        private static IEnumerable<string> GetQuickUpdateStatuses()
        {
            return AppConstants.AppointmentStatus.All
                .Where(status => status != AppConstants.AppointmentStatus.Completed);
        }

        private static decimal? ParseMoney(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().Replace(",", string.Empty).Replace(".", string.Empty);
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }

        private static string? FormatMoney(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("N0", CultureInfo.InvariantCulture)
                : null;
        }

        private async Task<IQueryable<Appointment>> ApplyTechnicianScopeAsync(IQueryable<Appointment> appointments)
        {
            if (!User.IsInRole(AppConstants.AdminRole.Technician))
            {
                return appointments;
            }

            var technicianId = await GetCurrentTechnicianIdAsync();
            if (!technicianId.HasValue)
            {
                TempData["ErrorMessage"] = "Tài khoản kỹ thuật viên chưa được liên kết với hồ sơ kỹ thuật viên.";
                return appointments.Where(a => false);
            }

            return appointments.Where(a => a.TechnicianId == technicianId.Value);
        }

        private async Task<bool> CurrentTechnicianCanAccessAppointmentAsync(Appointment appointment)
        {
            if (!User.IsInRole(AppConstants.AdminRole.Technician))
            {
                return true;
            }

            var technicianId = await GetCurrentTechnicianIdAsync();
            return technicianId.HasValue && appointment.TechnicianId == technicianId.Value;
        }

        private async Task<int?> GetCurrentTechnicianIdAsync()
        {
            var userIdValue = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrWhiteSpace(userIdValue) || !int.TryParse(userIdValue, out var userId))
            {
                return null;
            }

            var email = await _context.AdminUsers
                .Where(user => user.Id == userId && user.IsActive == true)
                .Select(user => user.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return await _context.Technicians
                .Where(technician => technician.IsActive == true && technician.Email == email)
                .Select(technician => (int?)technician.TechnicianId)
                .FirstOrDefaultAsync();
        }

        private async Task LoadDropdownsAsync()
        {
            ViewBag.Services = await _context.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.ServiceName)
                .Select(s => new SelectListItem
                {
                    Value = s.ServiceId.ToString(),
                    Text = s.ServiceName
                })
                .ToListAsync();

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .Select(c => new SelectListItem
                {
                    Value = c.CustomerId.ToString(),
                    Text = c.FullName + " - " + c.Phone
                })
                .ToListAsync();

            ViewBag.Technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .Select(t => new SelectListItem
                {
                    Value = t.TechnicianId.ToString(),
                    Text = t.FullName + " - " + t.Position
                })
                .ToListAsync();

            ViewBag.Vehicles = await _context.Vehicles
                .Include(v => v.Customer)
                .Include(v => v.Brand)
                .Include(v => v.Model)
                .Where(v => v.IsActive == true)
                .OrderBy(v => v.LicensePlate)
                .Select(v => new SelectListItem
                {
                    Value = v.VehicleId.ToString(),
                    Text = v.LicensePlate + " - " + (v.Customer != null ? v.Customer.FullName : "Không rõ khách") + " - " + ((v.Brand != null ? v.Brand.BrandName : "") + " " + (v.Model != null ? v.Model.ModelName : "")).Trim()
                })
                .ToListAsync();

            ViewBag.StatusList = GetQuickUpdateStatuses();
        }

        private async Task EnrichAppointmentAsync(Appointment appointment)
        {
            if (appointment.CustomerId.HasValue)
            {
                var customer = await _context.Customers.FindAsync(appointment.CustomerId.Value);
                if (customer != null)
                {
                    appointment.CustomerName = customer.FullName;
                    appointment.CustomerPhone = customer.Phone;
                    appointment.CustomerEmail = customer.Email;
                }
            }

            if (appointment.ServiceId.HasValue)
            {
                var service = await _context.Services.FindAsync(appointment.ServiceId.Value);
                if (service != null)
                {
                    appointment.ServiceType = service.ServiceName;
                    appointment.EstimatedDuration ??= service.EstimatedDuration;
                    appointment.TotalPrice ??= service.Price;
                }
            }

            if (appointment.VehicleId.HasValue)
            {
                var vehicle = await _context.Vehicles
                    .Include(v => v.Brand)
                    .Include(v => v.Model)
                    .FirstOrDefaultAsync(v => v.VehicleId == appointment.VehicleId.Value);

                if (vehicle != null && string.IsNullOrWhiteSpace(appointment.VehicleInfo))
                {
                    var vehicleName = string.Join(" ", new[] { vehicle.Brand?.BrandName, vehicle.Model?.ModelName }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    appointment.VehicleInfo = string.IsNullOrWhiteSpace(vehicleName)
                        ? vehicle.LicensePlate
                        : $"{vehicle.LicensePlate} - {vehicleName}";
                }
            }
        }

        private async Task ValidateAppointmentSlotAsync(Appointment appointment, int? excludeAppointmentId, bool allowPastDate = false)
        {
            if (!allowPastDate && appointment.AppointmentDate <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(Appointment.AppointmentDate), "Vui lòng chọn thời gian hẹn trong tương lai.");
                return;
            }

            var duration = appointment.EstimatedDuration ?? DefaultServiceDurationMinutes;
            var appointmentEnd = appointment.AppointmentDate.AddMinutes(duration);
            if (!IsInsideWorkingTime(appointment.AppointmentDate, appointmentEnd))
            {
                ModelState.AddModelError(
                    nameof(Appointment.AppointmentDate),
                    "Vui lòng chọn khung giờ trong giờ làm việc 08:00-17:00 và tránh giờ nghỉ trưa 12:00-13:00.");
                return;
            }

            if (!await IsSlotAvailableAsync(appointment.AppointmentDate, duration, appointment.TechnicianId, excludeAppointmentId))
            {
                ModelState.AddModelError(nameof(Appointment.AppointmentDate), "Khung giờ này đã kín lịch. Vui lòng chọn khung giờ khác.");
            }
        }

        private async Task<string> GenerateAppointmentCodeAsync()
        {
            var now = DateTime.Now;
            var count = await _context.Appointments.CountAsync(a => a.CreatedDate.HasValue && a.CreatedDate.Value.Date == now.Date) + 1;
            return $"APT{now:yyyyMMdd}{count:D4}";
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.AppointmentId == id);
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

            if (technicianId.HasValue)
            {
                return !await HasTechnicianConflictAsync(slotStart, slotEnd, technicianId.Value, excludeAppointmentId);
            }

            var overlappingCount = await CountBlockingAppointmentsAsync(slotStart, slotEnd, excludeAppointmentId);
            return overlappingCount < await GetActiveTechnicianCountAsync();
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
            if (technicianId.HasValue)
            {
                return await HasTechnicianConflictAsync(slotStart, slotEnd, technicianId.Value, excludeAppointmentId) ? 0 : 1;
            }

            var overlappingCount = await CountBlockingAppointmentsAsync(slotStart, slotEnd, excludeAppointmentId);
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

        private async Task EnsureServiceHistoryForCompletedAppointmentAsync(Appointment appointment)
        {
            if (appointment.Status != AppConstants.AppointmentStatus.Completed || !appointment.VehicleId.HasValue)
            {
                return;
            }

            var vehicle = await _context.Vehicles.FindAsync(appointment.VehicleId.Value);
            int? nextServiceMileage = vehicle?.Mileage.HasValue == true ? vehicle.Mileage.Value + 5000 : null;

            var history = await _context.ServiceHistories
                .FirstOrDefaultAsync(h => h.AppointmentId == appointment.AppointmentId);

            if (history == null)
            {
                history = new ServiceHistory
            {
                AppointmentId = appointment.AppointmentId,
                VehicleId = appointment.VehicleId.Value,
                ServiceId = appointment.ServiceId,
                TechnicianId = appointment.TechnicianId,
                ServiceDate = appointment.AppointmentDate,
                ServiceCode = "SVH" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                ServiceName = appointment.ServiceType,
                Description = appointment.Notes,
                LaborCost = appointment.TotalPrice,
                TotalCost = appointment.TotalPrice,
                Mileage = vehicle?.Mileage,
                NextServiceDate = appointment.AppointmentDate.AddMonths(6),
                NextServiceMileage = nextServiceMileage,
                Status = AppConstants.AppointmentStatus.Completed,
                Notes = "Tự động tạo từ lịch hẹn " + appointment.AppointmentCode,
                CreatedBy = User.Identity?.Name,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
                };
                _context.ServiceHistories.Add(history);
            }

            if (vehicle != null)
            {
                appointment.Vehicle = vehicle;
            }

            await EnsureInvoiceForServiceHistoryAsync(appointment, history, history.TotalCost ?? appointment.TotalPrice ?? 0);
        }

        private async Task<Invoice?> EnsureInvoiceForServiceHistoryAsync(Appointment appointment, ServiceHistory history, decimal totalAmount)
        {
            var customerId = appointment.CustomerId ?? appointment.Vehicle?.CustomerId;
            if (!customerId.HasValue && appointment.VehicleId.HasValue)
            {
                customerId = await _context.Vehicles
                    .Where(v => v.VehicleId == appointment.VehicleId.Value)
                    .Select(v => (int?)v.CustomerId)
                    .FirstOrDefaultAsync();
            }

            if (!customerId.HasValue)
            {
                return null;
            }

            Invoice? invoice = null;
            if (history.ServiceHistoryId > 0)
            {
                invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.ServiceHistoryId == history.ServiceHistoryId);
            }

            if (invoice == null)
            {
                invoice = new Invoice
                {
                    InvoiceCode = await GenerateInvoiceCodeAsync(),
                    CustomerId = customerId.Value,
                    VehicleId = history.VehicleId,
                    ServiceHistory = history,
                    InvoiceDate = DateTime.Now,
                    PaymentStatus = AppConstants.PaymentStatus.Unpaid,
                    Status = "Da tao",
                    Notes = "Hoa don tu lich sua chua " + appointment.AppointmentCode,
                    CreatedBy = User.Identity?.Name,
                    CreatedDate = DateTime.Now
                };
                _context.Invoices.Add(invoice);
            }

            invoice.CustomerId = customerId.Value;
            invoice.VehicleId = history.VehicleId;
            invoice.SubTotal = totalAmount;
            invoice.TaxAmount ??= 0;
            invoice.DiscountAmount ??= 0;
            invoice.TotalAmount = totalAmount;
            invoice.UpdatedDate = DateTime.Now;

            if (string.IsNullOrWhiteSpace(invoice.PaymentStatus))
            {
                invoice.PaymentStatus = AppConstants.PaymentStatus.Unpaid;
            }

            if (string.IsNullOrWhiteSpace(invoice.Status))
            {
                invoice.Status = "Da tao";
            }

            return invoice;
        }

        private async Task<string> GenerateInvoiceCodeAsync()
        {
            var now = DateTime.Now;
            var count = await _context.Invoices.CountAsync(i => i.InvoiceDate.Date == now.Date) + 1;
            return $"INV{now:yyyyMMdd}{count:D4}";
        }
    }

    public class CompleteAppointmentViewModel
    {
        public int AppointmentId { get; set; }

        public string? AppointmentCode { get; set; }

        public string? CustomerName { get; set; }

        public string? VehicleInfo { get; set; }

        public string? ServiceName { get; set; }

        public string? TechnicianName { get; set; }

        public DateTime AppointmentDate { get; set; }

        public int? ActualDuration { get; set; }

        public int? Mileage { get; set; }

        public decimal? LaborCost { get; set; }

        public string? LaborCostInput { get; set; }

        public decimal? PartsCost { get; set; }

        public string? PartsCostInput { get; set; }

        public decimal? TotalCost { get; set; }

        public string? TotalCostInput { get; set; }

        public string? PartsReplaced { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public DateTime? NextServiceDate { get; set; }

        public int? NextServiceMileage { get; set; }

        public DateTime? WarrantyExpiryDate { get; set; }
    }
}
