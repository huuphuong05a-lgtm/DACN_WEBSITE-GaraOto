using System;

namespace CarServ.MVC.Models
{
    public partial class Review
    {
        public int Id { get; set; }

        public int? ProductId { get; set; }

        public int? ServiceId { get; set; }

        public int? CustomerId { get; set; }

        public int? OrderItemId { get; set; }

        public int? ServiceHistoryId { get; set; }

        public string CustomerName { get; set; } = null!;

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public string? ImageUrl { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsApproved { get; set; }

        public virtual Customer? Customer { get; set; }

        public virtual OrderItem? OrderItem { get; set; }

        public virtual Product? Product { get; set; }

        public virtual Service? Service { get; set; }

        public virtual ServiceHistory? ServiceHistory { get; set; }
    }
}


