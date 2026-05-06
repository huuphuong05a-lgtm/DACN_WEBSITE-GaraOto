using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Appointment
{
    public int AppointmentId { get; set; }

    public string? AppointmentCode { get; set; }

    public int? CustomerId { get; set; }

    public int? TechnicianId { get; set; }

    public string? ServiceType { get; set; }

    public DateTime AppointmentDate { get; set; }

    public int? EstimatedDuration { get; set; }

    public int? ActualDuration { get; set; }

    public string? Notes { get; set; }

    public string? Status { get; set; }

    public decimal? TotalPrice { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? CustomerEmail { get; set; }

    public string? VehicleInfo { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? ServiceId { get; set; }

    public int? VehicleId { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Service? Service { get; set; }

    public virtual ICollection<ServiceHistory> ServiceHistories { get; set; } = new List<ServiceHistory>();

    public virtual Technician? Technician { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
