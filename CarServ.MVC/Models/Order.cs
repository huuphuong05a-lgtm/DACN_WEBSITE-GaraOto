using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public string? OrderCode { get; set; }

    public int? CustomerId { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal? ShippingFee { get; set; }

    public decimal? DiscountAmount { get; set; }

    public string? DiscountCode { get; set; }

    public decimal? FinalAmount { get; set; }

    public string? Status { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentStatus { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? TransactionCode { get; set; }

    public string? ShippingAddress { get; set; }

    public string? ShippingCity { get; set; }

    public string? ShippingDistrict { get; set; }

    public string? ShippingWard { get; set; }

    public string? CustomerNotes { get; set; }

    public string? AdminNotes { get; set; }

    public DateTime? OrderDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
