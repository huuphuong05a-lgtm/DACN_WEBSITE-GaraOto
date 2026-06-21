using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CarServ.MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace CarServ.MVC.Helpers;

public static class SampleDataSeeder
{
    public static async Task SeedGarageCatalogAsync(CarServContext context)
    {
        var now = DateTime.Now;
        var categoryIds = await EnsureProductCategoriesAsync(context, now);
        var supplierId = await EnsureSupplierAsync(context, now);

        var productImages = new[]
        {
            "/images/products/z7302682378453_ea1c488bf2ef520951afdabdce853c99.jpg",
            "/images/products/z7302682386182_795857484b0101ab48c75ed8981be032.jpg",
            "/images/products/z7302682497060_afbc07681e587916a785d9ab54961eca.jpg",
            "/images/products/z7302682530762_faa94de1c8f4f46af5633de6054f4eab.jpg",
            "/images/products/z7302682563510_29621ffe1e65dce75eeb0b427f69d82b.jpg",
            "/images/products/z7302682563750_3a5da12c2984e69e8a8346ccc49df6a0.jpg"
        };

        var products = new[]
        {
            new ProductSeed("Lọc dầu động cơ Toyota Camry", "Phụ tùng ô tô", 380000m, 350000m, "Lọc dầu chính hãng cho Toyota Camry, giúp bảo vệ động cơ.", "Lọc dầu động cơ giúp loại bỏ cặn bẩn trong dầu nhớt, tăng tuổi thọ động cơ và đảm bảo khả năng vận hành ổn định.", 25, 6, "LOCDAU-CAMRY-01", "Toyota", productImages[0], true, true),
            new ProductSeed("Dầu nhớt Castrol GTX 5W-30 4L", "Dầu nhớt", 650000m, 620000m, "Dầu nhớt cao cấp cho động cơ xăng.", "Dầu nhớt Castrol GTX 5W-30 giúp giảm ma sát, bảo vệ động cơ và tiết kiệm nhiên liệu.", 20, 0, "DN-CGTX-5W30", "Castrol", productImages[1], true, true),
            new ProductSeed("Lốp Michelin Pilot Sport 4 225/45R17", "Lốp xe", 3500000m, 3300000m, "Lốp hiệu suất cao, bám đường tốt.", "Lốp Michelin Pilot Sport 4 kích thước 225/45R17, phù hợp xe sedan và xe thể thao, tăng độ ổn định khi vận hành.", 12, 12, "LOP-MIC-2254517", "Michelin", productImages[2], true, false),
            new ProductSeed("Má phanh trước Honda Civic", "Phụ tùng ô tô", 750000m, 690000m, "Má phanh trước cho Honda Civic.", "Má phanh chất lượng cao, đảm bảo khả năng phanh ổn định, giảm tiếng ồn và tăng độ an toàn khi vận hành.", 18, 6, "MAPHANH-CIVIC-F", "Honda", productImages[3], false, true),
            new ProductSeed("Ắc quy khô 12V 70Ah", "Ắc quy", 1850000m, 1750000m, "Ắc quy khô 12V dùng cho ô tô.", "Ắc quy khô dung lượng 70Ah, phù hợp nhiều dòng xe du lịch, tuổi thọ cao và ít cần bảo dưỡng.", 15, 12, "ACQUY-12V-70AH", "GS", productImages[4], false, false),
            new ProductSeed("Lọc gió động cơ Ford Everest", "Phụ tùng ô tô", 420000m, 390000m, "Lọc gió động cơ cho Ford Everest.", "Lọc gió giúp ngăn bụi bẩn đi vào buồng đốt, hỗ trợ động cơ hoạt động ổn định và tiết kiệm nhiên liệu.", 30, 3, "LOCGIO-EVEREST-01", "Ford", productImages[5], false, false),
            new ProductSeed("Nước làm mát động cơ 4L", "Chăm sóc xe", 320000m, 290000m, "Dung dịch làm mát động cơ ô tô.", "Nước làm mát giúp ổn định nhiệt độ động cơ, chống đóng cặn và bảo vệ hệ thống két nước.", 35, 0, "COOLANT-4L", "NHP-AUTO", productImages[0], false, false),
            new ProductSeed("Bugi Iridium Denso", "Phụ tùng ô tô", 280000m, 250000m, "Bugi Iridium cho động cơ xăng.", "Bugi Iridium giúp đánh lửa ổn định, tăng hiệu suất đốt cháy nhiên liệu và cải thiện khả năng khởi động.", 40, 6, "BUGI-DENSO-IR", "Denso", productImages[1], false, false)
        };

        var services = new[]
        {
            new ServiceSeed("Bảo dưỡng định kỳ ô tô", "Bảo dưỡng", 850000m, 120, "Kiểm tra và bảo dưỡng tổng quát xe.", "Dịch vụ bao gồm thay dầu động cơ, kiểm tra lọc dầu, lọc gió, phanh, hệ thống treo, ắc quy và các hạng mục cơ bản theo khuyến nghị.", "/images/services/z7302682378453_ea1c488bf2ef520951afdabdce853c99.jpg", true, 10),
            new ServiceSeed("Sửa chữa hệ thống phanh", "Cơ khí", 450000m, 90, "Kiểm tra, sửa chữa và thay thế hệ thống phanh.", "Kiểm tra má phanh, đĩa phanh, dầu phanh, heo phanh và xử lý các lỗi liên quan đến hệ thống phanh.", "/images/services/z7302682530762_faa94de1c8f4f46af5633de6054f4eab.jpg", true, 20),
            new ServiceSeed("Sửa chữa hệ thống treo", "Cơ khí", 6500000m, 180, "Kiểm tra và sửa chữa hệ thống treo.", "Kiểm tra giảm xóc, rotuyn, càng A, cao su càng và các chi tiết liên quan đến hệ thống treo.", "/images/services/z7302682563750_3a5da12c2984e69e8a8346ccc49df6a0.jpg", true, 30),
            new ServiceSeed("Sửa chữa hệ thống điện", "Điện", 550000m, 120, "Kiểm tra và sửa chữa hệ thống điện ô tô.", "Kiểm tra bình ắc quy, máy phát, hệ thống đèn, cảm biến, cầu chì và các lỗi điện cơ bản trên xe.", "/images/services/z7302682497060_afbc07681e587916a785d9ab54961eca.jpg", false, 40),
            new ServiceSeed("Thay lốp xe", "Gầm xe", 300000m, 60, "Thay thế lốp xe và kiểm tra áp suất.", "Dịch vụ thay lốp, cân bằng động bánh xe, kiểm tra áp suất và tư vấn tình trạng lốp.", "/images/services/z7302682386182_795857484b0101ab48c75ed8981be032.jpg", false, 50),
            new ServiceSeed("Vệ sinh nội thất ô tô", "Chăm sóc xe", 700000m, 150, "Làm sạch nội thất xe chuyên sâu.", "Vệ sinh ghế, taplo, sàn xe, cửa gió điều hòa, khử mùi và làm sạch khoang nội thất.", "/img/service-4.jpg", false, 60),
            new ServiceSeed("Chẩn đoán lỗi động cơ bằng máy", "Điện tử", 300000m, 60, "Kiểm tra lỗi động cơ bằng thiết bị chẩn đoán.", "Sử dụng máy chẩn đoán để đọc mã lỗi, kiểm tra cảm biến, hệ thống phun xăng, đánh lửa và các thông số vận hành.", "/images/services/z7302682563510_29621ffe1e65dce75eeb0b427f69d82b.jpg", true, 70),
            new ServiceSeed("Sơn dặm và xử lý trầy xước", "Đồng sơn", 1200000m, 180, "Khắc phục vết trầy xước, bong sơn và va quẹt nhẹ.", "Dịch vụ sơn dặm giúp phục hồi bề mặt thân vỏ, xử lý trầy xước cục bộ, cân màu sơn và đánh bóng khu vực sửa chữa.", "/images/services/z7302682378453_ea1c488bf2ef520951afdabdce853c99.jpg", false, 80),
            new ServiceSeed("Phủ ceramic bảo vệ sơn", "Chăm sóc xe", 2500000m, 240, "Phủ lớp bảo vệ bề mặt sơn, tăng độ bóng và hạn chế bám bẩn.", "Phủ ceramic tạo lớp màng bảo vệ sơn xe khỏi tia UV, bụi bẩn, nước mưa và giúp xe giữ độ bóng lâu hơn.", "/img/service-2.jpg", true, 90),
            new ServiceSeed("Vệ sinh khoang máy", "Chăm sóc xe", 450000m, 90, "Làm sạch khoang động cơ an toàn và chuyên nghiệp.", "Vệ sinh khoang máy bằng dung dịch chuyên dụng, loại bỏ bụi bẩn, dầu mỡ bám lâu ngày và kiểm tra nhanh các chi tiết trong khoang động cơ.", "/img/service-3.jpg", false, 100),
            new ServiceSeed("Cân chỉnh thước lái", "Gầm xe", 600000m, 90, "Cân chỉnh góc đặt bánh xe giúp xe chạy ổn định.", "Kiểm tra và cân chỉnh độ chụm, góc camber, caster để hạn chế mòn lốp lệch, nhao lái và tăng độ ổn định khi vận hành.", "/images/services/z7302682386182_795857484b0101ab48c75ed8981be032.jpg", false, 110),
            new ServiceSeed("Kiểm tra và nạp gas điều hòa", "Điện lạnh", 550000m, 75, "Kiểm tra khả năng làm lạnh và bổ sung gas điều hòa.", "Dịch vụ kiểm tra rò rỉ, vệ sinh cơ bản hệ thống điều hòa, hút chân không và nạp gas theo đúng tiêu chuẩn kỹ thuật.", "/img/service-1.jpg", false, 120)
        };

        var addedProducts = 0;
        foreach (var item in products)
        {
            var slug = GenerateSlug(item.Name);
            var exists = await context.Products.AnyAsync(p =>
                p.Sku == item.Sku || p.Slug == slug || p.ProductName == item.Name);

            if (exists)
            {
                continue;
            }

            context.Products.Add(new Product
            {
                ProductName = item.Name,
                Slug = slug,
                CategoryId = categoryIds[item.CategoryName],
                SupplierId = supplierId,
                Price = item.Price,
                SalePrice = item.SalePrice,
                ShortDescription = item.ShortDescription,
                Description = item.Description,
                StockQuantity = item.StockQuantity,
                WarrantyMonths = item.WarrantyMonths,
                Sku = item.Sku,
                Brand = item.Brand,
                ImageUrl = item.ImageUrl,
                IsActive = true,
                IsFeatured = item.IsFeatured,
                IsNew = item.IsNew,
                ViewCount = 0,
                MetaTitle = item.Name,
                MetaDescription = item.ShortDescription,
                CreatedDate = now,
                UpdatedDate = now
            });
            addedProducts++;
        }

        var addedServices = 0;
        foreach (var item in services)
        {
            var slug = GenerateSlug(item.Name);
            var exists = await context.Services.AnyAsync(s => s.Slug == slug || s.ServiceName == item.Name);

            if (exists)
            {
                continue;
            }

            context.Services.Add(new Service
            {
                ServiceName = item.Name,
                Slug = slug,
                ServiceCategory = item.CategoryName,
                Price = item.Price,
                EstimatedDuration = item.EstimatedDuration,
                ShortDescription = item.ShortDescription,
                Description = item.Description,
                ImageUrl = item.ImageUrl,
                IsActive = true,
                IsFeatured = item.IsFeatured,
                SortOrder = item.SortOrder,
                MetaTitle = item.Name,
                MetaDescription = item.ShortDescription,
                CreatedDate = now,
                UpdatedDate = now
            });
            addedServices++;
        }

        if (addedProducts > 0 || addedServices > 0)
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task<Dictionary<string, int>> EnsureProductCategoriesAsync(CarServContext context, DateTime now)
    {
        var categoryNames = new[] { "Phụ tùng ô tô", "Dầu nhớt", "Lốp xe", "Ắc quy", "Chăm sóc xe" };
        var categoryIds = new Dictionary<string, int>();

        foreach (var name in categoryNames)
        {
            var category = await context.Categories.FirstOrDefaultAsync(c => c.CategoryName == name);
            if (category == null)
            {
                category = new Category
                {
                    CategoryName = name,
                    Description = $"Danh mục {name.ToLowerInvariant()} tại NHP-AUTO.",
                    IconClass = "fa fa-cogs",
                    SortOrder = categoryIds.Count + 1,
                    IsActive = true,
                    MetaTitle = name,
                    MetaDescription = $"Sản phẩm thuộc danh mục {name} tại NHP-AUTO.",
                    CreatedDate = now,
                    UpdatedDate = now
                };
                context.Categories.Add(category);
                await context.SaveChangesAsync();
            }

            categoryIds[name] = category.CategoryId;
        }

        return categoryIds;
    }

    private static async Task<int?> EnsureSupplierAsync(CarServContext context, DateTime now)
    {
        const string supplierCode = "NHP-AUTO-PARTS";
        var supplier = await context.Suppliers.FirstOrDefaultAsync(s => s.SupplierCode == supplierCode);
        if (supplier == null)
        {
            supplier = new Supplier
            {
                SupplierName = "Kho phụ tùng NHP-AUTO",
                SupplierCode = supplierCode,
                ContactPerson = "NHP-AUTO",
                Email = "nhpauto@example.com",
                Phone = "0357062771",
                Address = "Diễn Châu, Nghệ An",
                City = "Nghệ An",
                Notes = "Nhà cung cấp mẫu dùng cho dữ liệu demo sản phẩm.",
                IsActive = true,
                CreatedDate = now,
                UpdatedDate = now
            };
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();
        }

        return supplier.SupplierId;
    }

    private static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c == 'đ' ? 'd' : c);
            }
        }

        var slug = builder.ToString().Normalize(NormalizationForm.FormC);
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = Regex.Replace(slug, @"\-+", "-").Trim('-');

        return slug;
    }

    private sealed record ProductSeed(
        string Name,
        string CategoryName,
        decimal Price,
        decimal SalePrice,
        string ShortDescription,
        string Description,
        int StockQuantity,
        int WarrantyMonths,
        string Sku,
        string Brand,
        string ImageUrl,
        bool IsFeatured,
        bool IsNew);

    private sealed record ServiceSeed(
        string Name,
        string CategoryName,
        decimal Price,
        int EstimatedDuration,
        string ShortDescription,
        string Description,
        string ImageUrl,
        bool IsFeatured,
        int SortOrder);
}
