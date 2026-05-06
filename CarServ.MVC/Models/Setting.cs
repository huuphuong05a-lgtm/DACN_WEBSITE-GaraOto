using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Setting
{
    public int SettingId { get; set; }

    public string SettingKey { get; set; } = null!;

    public string? SettingValue { get; set; }

    public string? SettingGroup { get; set; }

    public string? DataType { get; set; }

    public string? Description { get; set; }

    public bool? IsSystem { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
