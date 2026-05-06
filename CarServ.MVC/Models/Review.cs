using System;

namespace CarServ.MVC.Models
{
    public partial class Review
    {
        public int Id { get; set; }

        public int? ProductId { get; set; }

        public int? ServiceId { get; set; }

        public string CustomerName { get; set; } = null!;

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedDate { get; set; }

        public bool IsApproved { get; set; }

        public virtual Product? Product { get; set; }

        public virtual Service? Service { get; set; }
    }
}


