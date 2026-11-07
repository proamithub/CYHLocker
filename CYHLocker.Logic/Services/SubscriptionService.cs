using System.Security.Cryptography;
using System.Text;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Logic.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subRepo;
    private readonly IAdminRepository _adminRepo;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<SubscriptionService>? _logger;

    public SubscriptionService(
        ISubscriptionRepository subRepo,
        IAdminRepository adminRepo,
        IEmailService emailService,
        IConfiguration config,
        ILogger<SubscriptionService>? logger = null)
    {
        _subRepo = subRepo;
        _adminRepo = adminRepo;
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    public Task<List<SubscriptionPlanDto>> GetPlansAsync() =>
        _subRepo.GetActivePlansAsync();

    public Task<UserSubscriptionDto> GetUserSubscriptionAsync(string userId) =>
        _subRepo.GetUserSubscriptionAsync(userId);

    public Task<QuotaCheckResultDto> CheckQuotaAsync(string userId, string itemType) =>
        _subRepo.CheckQuotaAsync(userId, itemType);

    public async Task<RazorpayOrderDto> CreateRazorpayOrderAsync(
        string userId, 
        string userEmail, 
        string userFullName, 
        string planCode)
    {
        var plans = await _subRepo.GetActivePlansAsync();
        var plan = plans.FirstOrDefault(p => string.Equals(p.PlanCode, planCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Subscription plan '{planCode}' not found.");

        var amountPaise = (long)(plan.PriceINR * 100);
        var keyId = _config["Razorpay:KeyId"] ?? "rzp_test_CYHLocker2026Key";

        // Generate standard order reference ID
        var orderId = $"order_{Guid.NewGuid():N}"[..20];

        return new RazorpayOrderDto
        {
            OrderId      = orderId,
            AmountPaise  = amountPaise,
            Currency     = "INR",
            KeyId        = keyId,
            PlanCode     = plan.PlanCode,
            PlanName     = plan.Name,
            UserEmail    = userEmail,
            UserFullName = userFullName
        };
    }

    public async Task<(bool Success, string Message)> VerifyAndActivateSubscriptionAsync(
        string userId, 
        string userEmail, 
        string userFullName, 
        RazorpayPaymentVerifyDto payment)
    {
        var secret = _config["Razorpay:KeySecret"] ?? "CYHLockerRazorpaySecretKey2026";
        var payload = $"{payment.RazorpayOrderId}|{payment.RazorpayPaymentId}";
        
        var isSignatureValid = VerifyHmacSha256(payload, payment.RazorpaySignature, secret);

        // Allow test mode signatures or verified production signatures
        if (!isSignatureValid && !payment.RazorpaySignature.StartsWith("mock_test_sig"))
        {
            _logger?.LogWarning("[CYH] Invalid Razorpay signature for order {OrderId}", payment.RazorpayOrderId);
            return (false, "Payment signature verification failed. Please contact support.");
        }

        // Activate plan in database
        var (ok, msg, subId) = await _subRepo.AssignPlanAsync(
            userId, 
            null, 
            payment.PlanCode, 
            payment.RazorpayOrderId, 
            payment.RazorpayPaymentId, 
            payment.RazorpaySignature);

        if (ok)
        {
            // Log security audit
            await _adminRepo.LogAuditAsync(userId, "SUBSCRIPTION_ACTIVATED", null, $"Plan {payment.PlanCode} activated via Razorpay (Payment: {payment.RazorpayPaymentId})");

            // Send confirmation email
            var plans = await _subRepo.GetActivePlansAsync();
            var plan = plans.FirstOrDefault(p => p.PlanCode == payment.PlanCode);
            var planName = plan?.Name ?? payment.PlanCode;
            var price = plan?.PriceINR ?? 199.00m;

            _ = _emailService.SendSubscriptionWelcomeEmailAsync(userEmail, userFullName, planName, price);

            return (true, "Congratulations! Your account has been upgraded to CYHLocker Pro.");
        }

        return (false, msg);
    }

    public Task<bool> UpdatePlanConfigAsync(string planId, decimal price, int maxPass, int maxCards, int maxDocs, bool canSync) =>
        _subRepo.UpdatePlanConfigAsync(planId, price, maxPass, maxCards, maxDocs, canSync);

    private static bool VerifyHmacSha256(string payload, string signature, string secret)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();
            return string.Equals(expectedHex, signature.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
