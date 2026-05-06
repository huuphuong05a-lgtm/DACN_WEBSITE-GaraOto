using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Testimonial
{
    public int TestimonialId { get; set; }

    public int? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPosition { get; set; }

    public string? CustomerAvatar { get; set; }

    public string Content { get; set; } = null!;

    public int? Rating { get; set; }

    public string? ServiceType { get; set; }

    public string? ImageUrl { get; set; }

    public bool? IsPublished { get; set; }

    public int? SortOrder { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual Customer? Customer { get; set; }
}
