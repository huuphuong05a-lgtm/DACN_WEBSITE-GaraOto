using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class OrderItem
{
    public int OrderItemId { get; set; }

    public int? OrderId { get; set; }

    public int? ProductId { get; set; }

    public string? ProductName { get; set; }

    public string? ProductImage { get; set; }

    public int? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? Discount { get; set; }

    public decimal? TotalPrice { get; set; }

    public string? Notes { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Product? Product { get; set; }
}
