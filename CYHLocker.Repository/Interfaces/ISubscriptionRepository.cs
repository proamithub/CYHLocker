using CYHLocker.Data.DTOs;

namespace CYHLocker.Repository.Interfaces;

public interface ISubscriptionRepository
{
    Task<List<SubscriptionPlanDto>> GetActivePlansAsync();
    Task<UserSubscriptionDto> GetUserSubscriptionAsync(string userId);
    Task<(bool Success, string Message, string? SubId)> AssignPlanAsync(
        string userId, 
        string? planId, 
        string? planCode, 
        string? orderId, 
        string? paymentId, 
        string? signature);
    Task<QuotaCheckResultDto> CheckQuotaAsync(string userId, string itemType);
    Task<bool> UpdatePlanConfigAsync(string planId, decimal price, int maxPass, int maxCards, int maxDocs, bool canSync);
}
