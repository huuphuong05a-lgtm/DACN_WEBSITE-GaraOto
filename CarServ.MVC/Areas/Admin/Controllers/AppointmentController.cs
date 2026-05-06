using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;

namespace CarServ.MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminAuth")]
    public class AppointmentController : Controller
    {
        private readonly CarServContext _context;

        public AppointmentController(CarServContext context)
        {
            _context = context;
        }

        // GET: Admin/Appointment
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
                .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(searchString))
            {
                appointments = appointments.Where(a => 
                    (a.AppointmentCode != null && a.AppointmentCode.Contains(searchString))
                    || (a.CustomerName != null && a.CustomerName.Contains(searchString))
                    || (a.CustomerPhone != null && a.CustomerPhone.Contains(searchString))
                    || (a.CustomerEmail != null && a.CustomerEmail.Contains(searchString))
                    || (a.ServiceType != null && a.ServiceType.Contains(searchString)));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                appointments = appointments.Where(a => a.Status == statusFilter);
            }

            // Filter by date
            if (!string.IsNullOrEmpty(dateFilter))
            {
                DateTime filterDate;
                if (DateTime.TryParse(dateFilter, out filterDate))
                {
                    appointments = appointments.Where(a => a.AppointmentDate.Date == filterDate.Date);
                }
            }

            // Sort
            switch (sortOrder)
            {
                case "date_desc":
                    appointments = appointments.OrderByDescending(a => a.AppointmentDate);
                    break;
                default:
                    appointments = appointments.OrderByDescending(a => a.CreatedDate);
                    break;
            }

            var appointmentList = await appointments.ToListAsync();

            // Get status list for filter
            var statusList = await _context.Appointments
                .Where(a => a.Status != null)
                .Select(a => a.Status)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            ViewBag.StatusList = statusList;

            return View(appointmentList);
        }

        // GET: Admin/Appointment/Details/5
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
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // GET: Admin/Appointment/Create
        public async Task<IActionResult> Create()
        {
            // Get services for dropdown
            var services = await _context.Services
                .Where(s => s.IsActive == true)
                .OrderBy(s => s.ServiceName)
                .ToListAsync();

            ViewBag.Services = services.Select(s => new SelectListItem 
            { 
                Value = s.ServiceId.ToString(), 
                Text = s.ServiceName 
            }).ToList();

            // Get customers for dropdown
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            // Get technicians for dropdown
            var technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.Technicians = technicians.Select(t => new SelectListItem 
            { 
                Value = t.TechnicianId.ToString(), 
                Text = $"{t.FullName} - {t.Position}" 
            }).ToList();

            return View();
        }

        // POST: Admin/Appointment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerId,TechnicianId,ServiceId,ServiceType,AppointmentDate,EstimatedDuration,Notes,Status,TotalPrice,CustomerName,CustomerPhone,CustomerEmail,VehicleInfo")] Appointment appointment)
        {
            if (ModelState.IsValid)
            {
                // Generate appointment code
                appointment.AppointmentCode = GenerateAppointmentCode();
                
                // If CustomerId is provided, get customer info
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

                // If ServiceId is provided, get service info
                if (appointment.ServiceId.HasValue)
                {
                    var service = await _context.Services.FindAsync(appointment.ServiceId.Value);
                    if (service != null)
                    {
                        appointment.ServiceType = service.ServiceName;
                        appointment.EstimatedDuration = service.EstimatedDuration;
                        appointment.TotalPrice = service.Price;
                    }
                }

                appointment.CreatedDate = DateTime.Now;
                appointment.UpdatedDate = DateTime.Now;

                if (string.IsNullOrEmpty(appointment.Status))
                {
                    appointment.Status = "Chờ xác nhận";
                }

                _context.Add(appointment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Reload dropdowns on error
            var services = await _context.Services
                .Where(s => s.IsActive == true)
                .OrderBy(s => s.ServiceName)
                .ToListAsync();

            ViewBag.Services = services.Select(s => new SelectListItem 
            { 
                Value = s.ServiceId.ToString(), 
                Text = s.ServiceName 
            }).ToList();

            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            var technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.Technicians = technicians.Select(t => new SelectListItem 
            { 
                Value = t.TechnicianId.ToString(), 
                Text = $"{t.FullName} - {t.Position}" 
            }).ToList();

            return View(appointment);
        }

        // GET: Admin/Appointment/Edit/5
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

            // Get services for dropdown
            var services = await _context.Services
                .Where(s => s.IsActive == true)
                .OrderBy(s => s.ServiceName)
                .ToListAsync();

            ViewBag.Services = services.Select(s => new SelectListItem 
            { 
                Value = s.ServiceId.ToString(), 
                Text = s.ServiceName 
            }).ToList();

            // Get customers for dropdown
            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            // Get technicians for dropdown
            var technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.Technicians = technicians.Select(t => new SelectListItem 
            { 
                Value = t.TechnicianId.ToString(), 
                Text = $"{t.FullName} - {t.Position}" 
            }).ToList();

            // Get status list
            var statusList = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang thực hiện", "Hoàn thành", "Đã hủy" };
            ViewBag.StatusList = statusList;

            return View(appointment);
        }

        // POST: Admin/Appointment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AppointmentId,AppointmentCode,CustomerId,TechnicianId,ServiceId,ServiceType,AppointmentDate,EstimatedDuration,ActualDuration,Notes,Status,TotalPrice,CustomerName,CustomerPhone,CustomerEmail,VehicleInfo,CreatedDate")] Appointment appointment)
        {
            if (id != appointment.AppointmentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingAppointment = await _context.Appointments.FindAsync(id);
                    if (existingAppointment == null)
                    {
                        return NotFound();
                    }

                    // Update properties
                    existingAppointment.CustomerId = appointment.CustomerId;
                    existingAppointment.TechnicianId = appointment.TechnicianId;
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

                    // If CustomerId is provided, update customer info
                    if (appointment.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(appointment.CustomerId.Value);
                        if (customer != null)
                        {
                            existingAppointment.CustomerName = customer.FullName;
                            existingAppointment.CustomerPhone = customer.Phone;
                            existingAppointment.CustomerEmail = customer.Email;
                        }
                    }

                    // If ServiceId is provided, update service info
                    if (appointment.ServiceId.HasValue)
                    {
                        var service = await _context.Services.FindAsync(appointment.ServiceId.Value);
                        if (service != null)
                        {
                            existingAppointment.ServiceType = service.ServiceName;
                            if (!existingAppointment.EstimatedDuration.HasValue)
                            {
                                existingAppointment.EstimatedDuration = service.EstimatedDuration;
                            }
                            if (!existingAppointment.TotalPrice.HasValue)
                            {
                                existingAppointment.TotalPrice = service.Price;
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentExists(appointment.AppointmentId))
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

            // Reload dropdowns on error
            var services = await _context.Services
                .Where(s => s.IsActive == true)
                .OrderBy(s => s.ServiceName)
                .ToListAsync();

            ViewBag.Services = services.Select(s => new SelectListItem 
            { 
                Value = s.ServiceId.ToString(), 
                Text = s.ServiceName 
            }).ToList();

            var customers = await _context.Customers
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new SelectListItem 
            { 
                Value = c.CustomerId.ToString(), 
                Text = $"{c.FullName} - {c.Phone}" 
            }).ToList();

            var technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.FullName)
                .ToListAsync();

            ViewBag.Technicians = technicians.Select(t => new SelectListItem 
            { 
                Value = t.TechnicianId.ToString(), 
                Text = $"{t.FullName} - {t.Position}" 
            }).ToList();

            var statusList = new[] { "Chờ xác nhận", "Đã xác nhận", "Đang thực hiện", "Hoàn thành", "Đã hủy" };
            ViewBag.StatusList = statusList;

            return View(appointment);
        }

        // GET: Admin/Appointment/Delete/5
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
                .FirstOrDefaultAsync(m => m.AppointmentId == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // POST: Admin/Appointment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
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

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.AppointmentId == id);
        }

        private string GenerateAppointmentCode()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var count = _context.Appointments.Count(a => a.CreatedDate.HasValue && a.CreatedDate.Value.Date == DateTime.Now.Date) + 1;
            return $"APT{date}{count:D4}";
        }
    }
}


