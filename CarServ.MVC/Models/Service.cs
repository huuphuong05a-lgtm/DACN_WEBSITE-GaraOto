using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Service
{
    public int ServiceId { get; set; }

    public string ServiceName { get; set; } = null!;

    public string? Slug { get; set; }

    public string? Description { get; set; }

    public string? ShortDescription { get; set; }

    public decimal? Price { get; set; }

    public int? EstimatedDuration { get; set; }

    public string? ImageUrl { get; set; }

    public string? GalleryImages { get; set; }

    public string? ServiceCategory { get; set; }

    public bool IsActive { get; set; }

    public bool IsFeatured { get; set; }

    public int? SortOrder { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<ServiceHistory> ServiceHistories { get; set; } = new List<ServiceHistory>();
}
