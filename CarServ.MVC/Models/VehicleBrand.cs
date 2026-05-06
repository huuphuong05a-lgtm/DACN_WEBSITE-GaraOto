using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class VehicleBrand
{
    public int BrandId { get; set; }

    public string BrandName { get; set; } = null!;

    public string? BrandCode { get; set; }

    public string? LogoUrl { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public int? SortOrder { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual ICollection<VehicleModel> VehicleModels { get; set; } = new List<VehicleModel>();

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
