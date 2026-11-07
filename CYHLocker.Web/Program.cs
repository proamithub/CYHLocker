using CYHLocker.Logic.Encryption;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Logic.Services;
using CYHLocker.Repository.Interfaces;
using CYHLocker.Repository.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// DI — same Logic and Repository as API (reusable!)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService,    AuthService>();

// Vault Core Services
builder.Services.AddSingleton<ICryptoEngine, AesGcmCryptoEngine>();
builder.Services.AddSingleton<IPasswordGeneratorService, PasswordGeneratorService>();
builder.Services.AddSingleton<IFaviconService, FaviconService>();
builder.Services.AddScoped<IVaultRepository, VaultRepository>();
builder.Services.AddScoped<IVaultService,    VaultService>();

// Admin & Reporting Services
builder.Services.AddScoped<IAdminRepository,  AdminRepository>();
builder.Services.AddScoped<IAdminService,     AdminService>();
builder.Services.AddScoped<IReportingService, ReportingService>();

// Email & Subscription Services
builder.Services.AddScoped<IEmailService,           EmailService>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<ISubscriptionService,    SubscriptionService>();

// Cloud & Offline Sync Services
builder.Services.AddScoped<ISyncRepository, SyncRepository>();
builder.Services.AddScoped<ISyncService,    SyncService>();

// Cookie Auth for web session
builder.Services.AddAuthentication("CYHCookie")
    .AddCookie("CYHCookie", opt =>
    {
        opt.LoginPath        = "/Auth/Login";
        opt.LogoutPath       = "/Auth/Logout";
        opt.AccessDeniedPath = "/Auth/AccessDenied";
        opt.ExpireTimeSpan   = TimeSpan.FromHours(8);
        opt.Cookie.HttpOnly  = true;
        opt.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
