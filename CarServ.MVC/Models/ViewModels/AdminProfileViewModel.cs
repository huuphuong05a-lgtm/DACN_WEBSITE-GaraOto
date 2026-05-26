using System.ComponentModel.DataAnnotations;

namespace CarServ.MVC.Models.ViewModels
{
    public class AdminProfileViewModel
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string? Role { get; set; }

        [Display(Name = "Họ tên")]
        [StringLength(100)]
        public string? FullName { get; set; }

        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(100)]
        public string? Email { get; set; }

        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20)]
        public string? Phone { get; set; }

        public bool? IsActive { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? LastLogin { get; set; }

        [Display(Name = "Mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [Display(Name = "Mật khẩu mới")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
        public string? NewPassword { get; set; }

        [Display(Name = "Nhập lại mật khẩu mới")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
        public string? ConfirmNewPassword { get; set; }
    }
}
