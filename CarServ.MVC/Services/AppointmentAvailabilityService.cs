using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Services
{
    public class AvailableSlotDto
    {
        public string TimeSlot { get; set; } = null!;
        public int TechnicianId { get; set; }
        public string TechnicianName { get; set; } = null!;
    }

    public class DateAvailabilityDto
    {
        public string DateLabel { get; set; } = null!;
        public List<AvailableSlotDto> AvailableSlots { get; set; } = new();
    }

    public interface IAppointmentAvailabilityService
    {
        Task<DateAvailabilityDto> GetAvailableSlotsAsync(DateTime date);
        Task<List<DateAvailabilityDto>> GetAvailableSlotsRangeAsync(DateTime startDate, DateTime endDate);
        Task<string> GetAvailabilityPromptTextAsync(DateTime startDate, int daysCount);
        Task<List<string>> GetAvailableTimeSlotsAsync(DateTime date);
        Task<List<string>> GetAvailableTechniciansAsync(DateTime date, int slotHour);
    }

    public class AppointmentAvailabilityService : IAppointmentAvailabilityService
    {
        private readonly CarServContext _context;

        public AppointmentAvailabilityService(CarServContext context)
        {
            _context = context;
        }

        private static readonly int[] StartHours = { 8, 9, 10, 11, 13, 14, 15, 16, 17 };

        public async Task<DateAvailabilityDto> GetAvailableSlotsAsync(DateTime date)
        {
            var results = await GetAvailableSlotsRangeAsync(date.Date, date.Date);
            return results.FirstOrDefault() ?? new DateAvailabilityDto { DateLabel = date.ToString("dd/MM/yyyy") };
        }

        public async Task<List<DateAvailabilityDto>> GetAvailableSlotsRangeAsync(DateTime startDate, DateTime endDate)
        {
            var start = startDate.Date;
            var end = endDate.Date;

            // Fetch active technicians
            var technicians = await _context.Technicians
                .Where(t => t.IsActive == true)
                .ToListAsync();

            var activeTechnicianCount = Math.Max(technicians.Count, 1);

            // Fetch appointments in range that are not canceled, completed, or no-show
            var appointments = await _context.Appointments
                .Where(a => a.AppointmentDate >= start && a.AppointmentDate < end.AddDays(1))
                .Where(a => a.Status != AppConstants.AppointmentStatus.Canceled
                    && a.Status != AppConstants.AppointmentStatus.Completed
                    && a.Status != AppConstants.AppointmentStatus.NoShow)
                .ToListAsync();

            var responseList = new List<DateAvailabilityDto>();

            for (var currentDate = start; currentDate <= end; currentDate = currentDate.AddDays(1))
            {
                var dayDto = new DateAvailabilityDto
                {
                    DateLabel = currentDate.ToString("dd/MM/yyyy")
                };

                // Find appointments for this specific day
                var dayAppointments = appointments
                    .Where(a => a.AppointmentDate.Date == currentDate)
                    .ToList();

                foreach (var hour in StartHours)
                {
                    var slotStart = currentDate.AddHours(hour);
                    var slotEnd = slotStart.AddHours(1);

                    // Count total overlapping appointments in this hourly slot
                    var slotAppointments = dayAppointments
                        .Where(a => a.AppointmentDate < slotEnd
                            && a.AppointmentDate.AddMinutes(a.EstimatedDuration ?? 60) > slotStart)
                        .ToList();

                    // If total overlapping bookings equals or exceeds the number of active technicians, the slot is full
                    if (slotAppointments.Count >= activeTechnicianCount)
                    {
                        continue;
                    }

                    // A technician is busy if they have an explicitly assigned appointment overlapping this slot
                    var busyTechnicianIds = slotAppointments
                        .Where(a => a.TechnicianId.HasValue)
                        .Select(a => a.TechnicianId!.Value)
                        .ToHashSet();

                    foreach (var tech in technicians)
                    {
                        if (!busyTechnicianIds.Contains(tech.TechnicianId))
                        {
                            dayDto.AvailableSlots.Add(new AvailableSlotDto
                            {
                                TimeSlot = $"{hour:D2}:00 - {(hour + 1):D2}:00",
                                TechnicianId = tech.TechnicianId,
                                TechnicianName = tech.FullName
                            });
                        }
                    }
                }

                responseList.Add(dayDto);
            }

            return responseList;
        }

        public async Task<string> GetAvailabilityPromptTextAsync(DateTime startDate, int daysCount)
        {
            var endDate = startDate.AddDays(daysCount - 1);
            var rangeData = await GetAvailableSlotsRangeAsync(startDate, endDate);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("DỮ LIỆU LỊCH TRỐNG THỰC TẾ TỪ CƠ SỞ DỮ LIỆU GARA:");
            foreach (var day in rangeData)
            {
                sb.AppendLine($"- Ngày {day.DateLabel}:");
                if (day.AvailableSlots.Any())
                {
                    // Group by time slot to present it cleanly
                    var groupedBySlot = day.AvailableSlots
                        .GroupBy(s => s.TimeSlot)
                        .OrderBy(g => g.Key);

                    foreach (var group in groupedBySlot)
                    {
                        var techNames = string.Join(", ", group.Select(s => s.TechnicianName));
                        sb.AppendLine($"  + Khung giờ {group.Key}: kỹ thuật viên trống: {techNames}");
                    }
                }
                else
                {
                    sb.AppendLine("  + Đã kín lịch toàn bộ các khung giờ hoặc gara không có kỹ thuật viên làm việc.");
                }
            }

            return sb.ToString();
        }

        public async Task<List<string>> GetAvailableTimeSlotsAsync(DateTime date)
        {
            var dateData = await GetAvailableSlotsAsync(date);
            return dateData.AvailableSlots
                .Select(s => s.TimeSlot)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        public async Task<List<string>> GetAvailableTechniciansAsync(DateTime date, int slotHour)
        {
            var dateData = await GetAvailableSlotsAsync(date);
            var slotLabel = $"{slotHour:D2}:00 - {(slotHour + 1):D2}:00";
            return dateData.AvailableSlots
                .Where(s => s.TimeSlot == slotLabel)
                .Select(s => s.TechnicianName)
                .OrderBy(t => t)
                .ToList();
        }
    }
}
