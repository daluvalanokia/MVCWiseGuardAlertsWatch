using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MVCWiseGuardAlertsWatch;
using MVCWiseGuardAlertsWatch.Controllers;
using MVCWiseGuardAlertsWatch.Models;
using MVCWiseGuardAlertsWatch.Services;

var builder = WebApplication.CreateBuilder(args);

// Database (SQLite — change connection string for production)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=wiseguard.db"));

// Services
builder.Services.AddSingleton<CryptoService>();
builder.Services.AddHttpClient<GeocodeService>();
builder.Services.AddHttpClient<DynamicDataService>();
builder.Services.AddHttpClient<TranscribeService>();

builder.Services.AddControllersWithViews();

// Cookie authentication for the standalone account module
var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

// Google OAuth: register the scheme ONLY when credentials are configured —
// an unregistered (empty) ClientId makes the handler throw on every request.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var app = builder.Build();

// Auto-create database on first run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSchemaUpgrader.Apply(db); // upgrade DBs created by older builds (adds missing columns)

    // Default admin account (admin / admin@123) — always available, created once.
    var adminEmail = "admin";
    if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
    {
        db.Users.Add(new User
        {
            Email = adminEmail,
            FullName = "Administrator",
            PasswordHash = AccountController.HashPassword("admin@123")
        });
        await db.SaveChangesAsync();
        Console.WriteLine("[auth] seeded default admin account (admin / admin@123)");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
