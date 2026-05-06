using CarServ.MVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add Authentication - Support both Admin and Customer
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; // Default for customers
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "CustomerAuth"; // Separate cookie for customer
    })
    .AddCookie("AdminAuth", options =>
    {
        options.LoginPath = "/Admin/Auth/Login";
        options.AccessDeniedPath = "/Admin/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name = "AdminAuth"; // Separate cookie for admin
    });

builder.Services.AddControllersWithViews(options =>
{
    options.MaxModelBindingCollectionSize = 100;
});
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

// Configure form options for file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10_485_760; // 10MB
    options.ValueLengthLimit = 10_485_760; // 10MB
});

// Add Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add Authorization with Roles
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AdminOrStaff", policy => policy.RequireRole("Admin", "Staff"));
});

builder.Services.AddDbContext<CarServContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CarServDb")));

// Register Payment Gateway Service
builder.Services.AddScoped<CarServ.MVC.Services.IPaymentGatewayService, CarServ.MVC.Services.PaymentGatewayService>();

var app = builder.Build();

// Seed admin user nếu chưa có
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<CarServContext>();
        await CarServ.MVC.Helpers.AdminUserSeeder.SeedAdminUserAsync(context);
    }
}
catch (Exception ex)
{
    // Log but don't crash - database may not exist yet
    Console.WriteLine($"Warning: Admin seeding failed: {ex.Message}");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

// Custom routes for friendly URLs - must be before default route
// Route for services index (must be before slug route to avoid conflict)
app.MapControllerRoute(
    name: "services",
    pattern: "services",
    defaults: new { controller = "Service", action = "Index" }
);

// Route for service details by slug
app.MapControllerRoute(
    name: "service-details-slug",
    pattern: "services/{slug}",
    defaults: new { controller = "Service", action = "Details" }
);

// Route for products index
app.MapControllerRoute(
    name: "products",
    pattern: "products",
    defaults: new { controller = "Product", action = "Index" }
);

// Route for product details by slug
app.MapControllerRoute(
    name: "product-details-slug",
    pattern: "products/{slug}",
    defaults: new { controller = "Product", action = "Details" }
);

// Route for blog index
app.MapControllerRoute(
    name: "blog",
    pattern: "blog",
    defaults: new { controller = "Blog", action = "Index" }
);

// Route for blog details by slug
app.MapControllerRoute(
    name: "blog-details-slug",
    pattern: "blog/{slug}",
    defaults: new { controller = "Blog", action = "Detail" }
);

app.MapControllerRoute(
    name: "cart",
    pattern: "cart",
    defaults: new { controller = "Cart", action = "Index" }
);

app.MapControllerRoute(
    name: "payment",
    pattern: "payment/{action=Index}",
    defaults: new { controller = "Payment" }
);

app.MapControllerRoute(
    name: "about",
    pattern: "about",
    defaults: new { controller = "Home", action = "About" }
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
