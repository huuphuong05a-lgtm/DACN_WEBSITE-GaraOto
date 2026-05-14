using System;
using System.ComponentModel.DataAnnotations;

namespace CarServ.MVC.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;  // Lưu tên đăng nhập hoặc Session ID
        public string Message { get; set; } = string.Empty; // Nội dung tin nhắn
        public bool IsBot { get; set; }     // true = Bot, false = Khách
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
