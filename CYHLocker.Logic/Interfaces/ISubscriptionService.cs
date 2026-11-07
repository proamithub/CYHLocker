using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface ISubscriptionService
{
    Task<List<SubscriptionPlanDto>> GetPlansAsync();
    Task<UserSubscriptionDto> GetUserSubscriptionAsync(string userId);
    Task<QuotaCheckResultDto> CheckQuotaAsync(string userId, string itemType);
    Task<RazorpayOrderDto> CreateRazorpayOrderAsync(string userId, string userEmail, string userFullName, string planCode);
    Task<(bool Success, string Message)> VerifyAndActivateSubscriptionAsync(
        string userId, 
        string userEmail, 
        string userFullName, 
        RazorpayPaymentVerifyDto payment);
    Task<bool> UpdatePlanConfigAsync(string planId, decimal price, int maxPass, int maxCards, int maxDocs, bool canSync);
}
