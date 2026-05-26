namespace CarServ.MVC.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public decimal TodayRevenue { get; set; }
        public decimal MonthRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PaidOrderCount { get; set; }
        public int PaidInvoiceCount { get; set; }
        public int PendingAppointmentCount { get; set; }
        public int CompletedAppointmentCount { get; set; }
        public int LowStockProductCount { get; set; }
        public List<string> RevenueLabels { get; set; } = new();
        public List<decimal> RevenueValues { get; set; } = new();
        public List<string> MonthlyRevenueLabels { get; set; } = new();
        public List<decimal> MonthlyRevenueValues { get; set; } = new();
    }
}
