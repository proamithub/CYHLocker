using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Logic.Services;
using CYHLocker.Repository.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CYHLocker.Tests;

public class SubscriptionIntegrationTests
{
    private readonly IConfiguration _config;

    public SubscriptionIntegrationTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" },
                { "Razorpay:KeyId", "rzp_test_CYHLocker2026Key" },
                { "Razorpay:KeySecret", "CYHLockerRazorpaySecretKey2026" }
            })
            .Build();
    }

    [Fact]
    public async Task Subscription_GetPlans_ReturnsSeedTiers()
    {
        var repo = new SubscriptionRepository(_config);
        var plans = await repo.GetActivePlansAsync();

        Assert.NotNull(plans);
        Assert.True(plans.Count >= 3);
        Assert.Contains(plans, p => p.PlanCode == "FREE");
        Assert.Contains(plans, p => p.PlanCode == "PRO_MONTHLY" && p.PriceINR == 199.00m);
        Assert.Contains(plans, p => p.PlanCode == "PRO_ANNUAL" && p.PriceINR == 1499.00m);
    }

    [Fact]
    public async Task Subscription_UserStartsOnFreePlan_AndChecksQuota()
    {
        var repo = new SubscriptionRepository(_config);

        string userId;
        await using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await con.OpenAsync();
            await using var cmd = new SqlCommand("SELECT TOP 1 Id FROM Users_CYH WHERE NormalizedEmail = 'USER@CYHLOCKER.COM'", con);
            userId = (await cmd.ExecuteScalarAsync())?.ToString() ?? throw new Exception("User not found");

            // Reset subscription to ensure idempotent test
            await using var cleanCmd = new SqlCommand("DELETE FROM UserSubscriptions_CYH WHERE UserId = @Uid", con);
            cleanCmd.Parameters.AddWithValue("@Uid", userId);
            await cleanCmd.ExecuteNonQueryAsync();
        }

        var userSub = await repo.GetUserSubscriptionAsync(userId);
        Assert.NotNull(userSub);
        Assert.Equal("FREE", userSub.PlanCode);
        Assert.Equal(15, userSub.MaxPasswords);
        Assert.Equal(2, userSub.MaxCards);
        Assert.Equal(1, userSub.MaxDocuments);

        // Check quota check
        var quota = await repo.CheckQuotaAsync(userId, "Login");
        Assert.True(quota.IsAllowed);
    }

    [Fact]
    public async Task Subscription_RazorpayOrder_GeneratesOrderWithPaise()
    {
        var repo = new SubscriptionRepository(_config);
        var adminRepo = new AdminRepository(_config);
        var email = new EmailService(_config);
        var subService = new SubscriptionService(repo, adminRepo, email, _config);

        var order = await subService.CreateRazorpayOrderAsync(
            "dummy_user", 
            "test@cyhlocker.com", 
            "Test User", 
            "PRO_MONTHLY");

        Assert.NotNull(order);
        Assert.Equal(19900, order.AmountPaise); // ₹199 * 100 paise
        Assert.Equal("INR", order.Currency);
        Assert.StartsWith("order_", order.OrderId);
        Assert.Equal("rzp_test_CYHLocker2026Key", order.KeyId);
    }

    [Fact]
    public async Task Subscription_VerifyAndActivate_UpgradesUserToPro()
    {
        var repo = new SubscriptionRepository(_config);
        var adminRepo = new AdminRepository(_config);
        var email = new EmailService(_config);
        var subService = new SubscriptionService(repo, adminRepo, email, _config);

        string userId;
        await using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await con.OpenAsync();
            await using var cmd = new SqlCommand("SELECT TOP 1 Id FROM Users_CYH WHERE NormalizedEmail = 'USER@CYHLOCKER.COM'", con);
            userId = (await cmd.ExecuteScalarAsync())?.ToString() ?? throw new Exception("User not found");
        }

        // Test activation with mock verification signature
        var verifyDto = new RazorpayPaymentVerifyDto
        {
            PlanCode = "PRO_MONTHLY",
            RazorpayOrderId = "order_test_123456",
            RazorpayPaymentId = "pay_test_987654",
            RazorpaySignature = "mock_test_sig_abc123"
        };

        var (success, msg) = await subService.VerifyAndActivateSubscriptionAsync(
            userId, 
            "user@cyhlocker.com", 
            "CYH User", 
            verifyDto);

        Assert.True(success);

        // Verify user is now Pro
        var userSub = await subService.GetUserSubscriptionAsync(userId);
        Assert.Equal("PRO_MONTHLY", userSub.PlanCode);
        Assert.True(userSub.IsPro);
        Assert.True(userSub.CanSync);

        // Revert back to FREE for ongoing test idempotency
        await repo.AssignPlanAsync(userId, "PLAN_FREE", "FREE", null, null, null);
    }

    [Fact]
    public async Task EmailService_PasswordReset_DispatchesSuccessfully()
    {
        var emailService = new EmailService(_config);
        var ok = await emailService.SendPasswordResetEmailAsync(
            "user@cyhlocker.com", 
            "CYH User", 
            "token12345", 
            "https://localhost:7124/Auth/ResetPassword?token=token12345");

        Assert.True(ok);
    }
}
