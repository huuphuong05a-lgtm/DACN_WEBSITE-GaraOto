using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Technician
{
    public int TechnicianId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Position { get; set; }

    public int? ExperienceYears { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? ImageUrl { get; set; }

    public string? Bio { get; set; }

    public string? Skills { get; set; }

    public decimal? Rating { get; set; }

    public int? TotalReviews { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<ServiceHistory> ServiceHistories { get; set; } = new List<ServiceHistory>();
}
