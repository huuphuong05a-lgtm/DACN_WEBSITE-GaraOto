namespace CarServ.MVC.Models
{
    public static class AppConstants
    {
        public static class OrderStatus
        {
            public const string Pending = "Chờ xử lý";
            public const string Processing = "Đang xử lý";
            public const string Shipping = "Đang giao hàng";
            public const string Delivered = "Đã giao hàng";
            public const string Canceled = "Đã hủy";
            public const string Returned = "Hoàn trả";
        }

        public static class PaymentStatus
        {
            public const string Unpaid = "Chưa thanh toán";
            public const string Pending = "Chờ thanh toán";
            public const string OnlinePending = "Chờ thanh toán online";
            public const string Paid = "Đã thanh toán";
            public const string Failed = "Thất bại";
            public const string Canceled = "Đã hủy";
            public const string Refunded = "Hoàn tiền";
            public const string Partial = "Thanh toán một phần";
        }

        public static class PaymentMethod
        {
            public const string COD = "COD";
            public const string VNPay = "VNPay";
            public const string Momo = "Momo";
            public const string Cash = "Tiền mặt";
            public const string BankTransfer = "Chuyển khoản";
            public const string CreditCard = "Thẻ tín dụng";
            public const string EWallet = "Ví điện tử";
        }

        public static class AppointmentStatus
        {
            public const string Pending = "Chờ xác nhận";
            public const string Confirmed = "Đã xác nhận";
            public const string InProgress = "Đang thực hiện";
            public const string Completed = "Hoàn thành";
            public const string Canceled = "Đã hủy";
        }
    }
}
