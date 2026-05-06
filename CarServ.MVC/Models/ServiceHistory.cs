using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class ServiceHistory
{
    public int ServiceHistoryId { get; set; }

    public int VehicleId { get; set; }

    public int? ServiceId { get; set; }

    public int? TechnicianId { get; set; }

    public int? AppointmentId { get; set; }

    public DateTime ServiceDate { get; set; }

    public string? ServiceCode { get; set; }

    public string? ServiceName { get; set; }

    public string? Description { get; set; }

    public string? PartsReplaced { get; set; }

    public decimal? LaborCost { get; set; }

    public decimal? PartsCost { get; set; }

    public decimal? TotalCost { get; set; }

    public int? Mileage { get; set; }

    public DateTime? NextServiceDate { get; set; }

    public int? NextServiceMileage { get; set; }

    public DateTime? WarrantyExpiryDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Service? Service { get; set; }

    public virtual Technician? Technician { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;
}
