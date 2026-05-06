using System;

namespace CarServ.MVC.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public string PaymentCode { get; set; } = null!;

    public int? OrderId { get; set; }

    public int? InvoiceId { get; set; }

    public int? CustomerId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public string? TransactionCode { get; set; }

    public string? GatewayTransactionId { get; set; }

    public string? GatewayName { get; set; }

    public string? GatewayResponse { get; set; }

    public DateTime PaymentDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual Order? Order { get; set; }
}

