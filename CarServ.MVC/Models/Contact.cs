using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Contact
{
    public int ContactId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Subject { get; set; }

    public string Message { get; set; } = null!;

    public string? Status { get; set; }

    public bool? IsRead { get; set; }

    public bool? IsReplied { get; set; }

    public string? ReplyMessage { get; set; }

    public string? RepliedBy { get; set; }

    public DateTime? RepliedDate { get; set; }

    public string? Ipaddress { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
