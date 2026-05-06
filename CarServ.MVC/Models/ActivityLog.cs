using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class ActivityLog
{
    public long LogId { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public string? Action { get; set; }

    public string? TableName { get; set; }

    public int? RecordId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? Ipaddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime? CreatedDate { get; set; }
}
