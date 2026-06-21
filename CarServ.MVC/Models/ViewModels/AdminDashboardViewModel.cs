namespace CarServ.MVC.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // --- Existing properties (unchanged) ---
        public decimal TodayRevenue { get; set; }
        public decimal WeekRevenue { get; set; }
        public decimal MonthRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PaidOrderCount { get; set; }
        public int PaidInvoiceCount { get; set; }
        public int PendingAppointmentCount { get; set; }
        public int CompletedAppointmentCount { get; set; }
        public int LowStockProductCount { get; set; }
        public List<string> RevenueLabels { get; set; } = new();
        public List<decimal> RevenueValues { get; set; } = new();

        // --- New: Transaction details for revenue card modals ---
        public List<RevenueTransactionDto> TodayTransactions { get; set; } = new();
        public List<RevenueTransactionDto> WeekTransactions { get; set; } = new();
        public List<RevenueTransactionDto> MonthTransactions { get; set; } = new();
        public List<RevenueTransactionDto> AllTransactions { get; set; } = new();

        // --- New: Daily breakdown for chart click-through ---
        public List<DailyRevenueDetailDto> DailyRevenueDetails { get; set; } = new();

        // --- New: Revenue split by source ---
        public decimal ServiceRevenue { get; set; }
        public decimal ProductRevenue { get; set; }

        // --- New: Top 5 widgets ---
        public List<TopItemDto> TopServices { get; set; } = new();
        public List<TopItemDto> TopProducts { get; set; } = new();
    }
}
