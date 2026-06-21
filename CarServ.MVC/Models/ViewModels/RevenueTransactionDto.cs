namespace CarServ.MVC.Models.ViewModels
{
    public class RevenueTransactionDto
    {
        public string TransactionCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Source { get; set; } = string.Empty; // "Đơn hàng", "Hóa đơn", "Lịch hẹn"
    }
}
