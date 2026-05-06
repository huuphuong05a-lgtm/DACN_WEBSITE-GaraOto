using System;

namespace CarServ.MVC.Models
{
    public partial class AdminUser
    {
        public int Id { get; set; }

        public string Username { get; set; } = null!;

        public string? PasswordHash { get; set; }

        public string? FullName { get; set; }

        public string? Role { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public bool? IsActive { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? LastLogin { get; set; }
    }
}


