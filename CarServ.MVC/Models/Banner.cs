using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Banner
{
    public int BannerId { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public string? MobileImageUrl { get; set; }

    public string? LinkUrl { get; set; }

    public string? ButtonText { get; set; }

    public string? ButtonColor { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }

    public string? BannerType { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
