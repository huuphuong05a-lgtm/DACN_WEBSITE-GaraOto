namespace CarServ.MVC.Models.ViewModels
{
    public class DailyRevenueDetailDto
    {
        public string DateLabel { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<RevenueTransactionDto> Transactions { get; set; } = new();
    }
}
