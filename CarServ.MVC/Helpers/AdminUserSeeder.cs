using Microsoft.EntityFrameworkCore;
using CarServ.MVC.Models;
using Microsoft.Extensions.Configuration;

namespace CarServ.MVC.Helpers
{
    public static class AdminUserSeeder
    {
        /// <summary>
        /// Tạo tài khoản admin mặc định nếu bảng AdminUsers rỗng
        /// </summary>
        public static async Task SeedAdminUserAsync(CarServContext context, IConfiguration configuration)
        {
            try
            {
                var seedPassword = Environment.GetEnvironmentVariable("NHP_AUTO_ADMIN_PASSWORD")
                    ?? configuration["AdminSeed:Password"];

                if (string.IsNullOrWhiteSpace(seedPassword))
                {
                    // Local/demo fallback only. Override it with NHP_AUTO_ADMIN_PASSWORD before deployment.
                    seedPassword = "123456";
                }

                // Kiểm tra xem đã có admin user chưa
                var hasAdmin = await context.AdminUsers.AnyAsync();

                if (!hasAdmin)
                {
                    string passwordHash = AdminPasswordHelper.HashPassword(seedPassword);

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
                else
                {
                    var defaultAdmin = await context.AdminUsers
                        .FirstOrDefaultAsync(a => a.Username == "admin");

                    if (defaultAdmin != null)
                    {
                        defaultAdmin.PasswordHash = AdminPasswordHelper.HashPassword(seedPassword);
                        defaultAdmin.Role = AppConstants.AdminRole.Admin;
                        defaultAdmin.IsActive = true;
                        await context.SaveChangesAsync();
                    }
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


