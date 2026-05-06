using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public int CustomerId { get; set; }

    public int? BrandId { get; set; }

    public int? ModelId { get; set; }

    public string LicensePlate { get; set; } = null!;

    public string? VehicleName { get; set; }

    public int? Year { get; set; }

    public string? Color { get; set; }

    public string? ChassisNumber { get; set; }

    public string? EngineNumber { get; set; }

    public DateTime? RegistrationDate { get; set; }

    public int? Mileage { get; set; }

    public string? FuelType { get; set; }

    public string? ImageUrl { get; set; }

    public string? Notes { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual VehicleBrand? Brand { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual VehicleModel? Model { get; set; }

    public virtual ICollection<ServiceHistory> ServiceHistories { get; set; } = new List<ServiceHistory>();
}
