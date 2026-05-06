using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class VehicleModel
{
    public int ModelId { get; set; }

    public int BrandId { get; set; }

    public string ModelName { get; set; } = null!;

    public string? ModelCode { get; set; }

    public int? YearFrom { get; set; }

    public int? YearTo { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public int? SortOrder { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual VehicleBrand Brand { get; set; } = null!;

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
