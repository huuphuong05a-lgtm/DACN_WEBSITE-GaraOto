using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;

namespace CarServ.MVC.Helpers
{
    public static class AdminUserSeeder
    {
        /// <summary>
        /// Tạo tài khoản admin mặc định nếu bảng AdminUsers rỗng
        /// </summary>
        public static async Task SeedAdminUserAsync(CarServContext context)
        {
            try
            {
                // Kiểm tra xem đã có admin user chưa
                var hasAdmin = await context.AdminUsers.AnyAsync();

                if (!hasAdmin)
                {
                    // Hash password "123456"
                    string passwordHash = AdminPasswordHelper.HashPassword("123456");

                    var adminUser = new AdminUser
                    {
                        Username = "admin",
                        PasswordHash = passwordHash,
                        FullName = "Quản trị viên",
                        Role = "Admin",
                        Email = null,
                        Phone = null,
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        LastLogin = null
                    };

                    context.AdminUsers.Add(adminUser);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Log exception but don't throw - allow app to continue even if seeding fails
                System.Diagnostics.Debug.WriteLine($"Admin seeding error: {ex.Message}");
            }
        }
    }
}


