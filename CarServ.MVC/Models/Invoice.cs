using System;
using System.Collections.Generic;

namespace CarServ.MVC.Models;

public partial class Invoice
{
    public int InvoiceId { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public int CustomerId { get; set; }

    public int? VehicleId { get; set; }

    public int? ServiceHistoryId { get; set; }

    public DateTime InvoiceDate { get; set; }

    public decimal? SubTotal { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentStatus { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? TransactionCode { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ServiceHistory? ServiceHistory { get; set; }

    public virtual Vehicle? Vehicle { get; set; }
}
