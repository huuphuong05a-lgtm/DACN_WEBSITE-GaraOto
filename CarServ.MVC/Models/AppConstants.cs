namespace CarServ.MVC.Models
{
    public static class AppConstants
    {
        public static class AdminRole
        {
            public const string Admin = "Admin";
            public const string Staff = "Staff";
            public const string Technician = "Technician";

            public const string AdminOnly = Admin;
            public const string AdminOrStaff = Admin + "," + Staff;
            public const string AdminStaffOrTechnician = Admin + "," + Staff + "," + Technician;

            public static readonly string[] All =
            {
                Admin,
                Staff,
                Technician
            };

            public static string GetDisplayName(string? role)
            {
                return role switch
                {
                    Admin => "Quản trị viên",
                    Staff => "Nhân viên",
                    Technician => "Kỹ thuật viên",
                    _ => role ?? string.Empty
                };
            }
        }

        public static class OrderStatus
        {
            public const string Pending = "Chờ xử lý";
            public const string Processing = "Đang xử lý";
            public const string Shipping = "Đang giao hàng";
            public const string Delivered = "Đã giao hàng";
            public const string Canceled = "Đã hủy";
            public const string Returned = "Hoàn trả";

            public static readonly string[] All =
            {
                Pending,
                Processing,
                Shipping,
                Delivered,
                Canceled,
                Returned
            };
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

            public static readonly string[] All =
            {
                Unpaid,
                Pending,
                OnlinePending,
                Paid,
                Failed,
                Canceled,
                Refunded,
                Partial
            };
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
            public const string Assigned = "Đã phân công";
            public const string Inspecting = "Đang kiểm tra";
            public const string Repairing = "Đang sửa chữa";
            public const string WaitingParts = "Chờ phụ tùng";
            public const string InProgress = "Đang thực hiện";
            public const string Completed = "Hoàn thành";
            public const string Canceled = "Đã hủy";
            public const string NoShow = "Khách không đến";

            public static readonly string[] All =
            {
                Pending,
                Confirmed,
                Assigned,
                Inspecting,
                Repairing,
                WaitingParts,
                InProgress,
                Completed,
                Canceled,
                NoShow
            };

            public static readonly string[] Blocking =
            {
                Pending,
                Confirmed,
                Assigned,
                Inspecting,
                Repairing,
                WaitingParts,
                InProgress
            };

            public static readonly string[] CustomerCancelable =
            {
                Pending,
                Confirmed
            };

            public static bool CanCustomerCancel(string? status)
            {
                return CustomerCancelable.Contains(status);
            }
        }
    }
}
