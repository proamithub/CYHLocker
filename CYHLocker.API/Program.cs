using System.IO.Compression;
using System.Text;
using CYHLocker.Logic.Encryption;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Logic.Services;
using CYHLocker.Repository.Interfaces;
using CYHLocker.Repository.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// High-speed Brotli & Gzip Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// DI Registration
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

// CORS for Browser Extension & Web Clients
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p =>
    {
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod();
    });
});

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? "CYHLocker_SuperSecretKey_2026_AmitLab!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseResponseCompression();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
