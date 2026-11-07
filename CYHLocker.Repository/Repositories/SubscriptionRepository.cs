using System.Data;
using CYHLocker.Data.DTOs;
using CYHLocker.Repository.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Repository.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly string _conn;
    private readonly ILogger<SubscriptionRepository>? _logger;

    public SubscriptionRepository(IConfiguration config, ILogger<SubscriptionRepository>? logger = null)
    {
        _conn = config.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("'DefaultConnection' not found.");
        _logger = logger;
    }

    public async Task<List<SubscriptionPlanDto>> GetActivePlansAsync()
    {
        var plans = new List<SubscriptionPlanDto>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSubscriptions_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_PLANS");

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                plans.Add(new SubscriptionPlanDto
                {
                    Id           = r["Id"].ToString()!,
                    PlanCode     = r["PlanCode"].ToString()!,
                    Name         = r["Name"].ToString()!,
                    Description  = r["Description"] != DBNull.Value ? r["Description"].ToString() : null,
                    PriceINR     = Convert.ToDecimal(r["PriceINR"]),
                    BillingCycle = r["BillingCycle"].ToString()!,
                    MaxPasswords = Convert.ToInt32(r["MaxPasswords"]),
                    MaxCards     = Convert.ToInt32(r["MaxCards"]),
                    MaxDocuments = Convert.ToInt32(r["MaxDocuments"]),
                    CanSync      = Convert.ToBoolean(r["CanSync"]),
                    IsActive     = Convert.ToBoolean(r["IsActive"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetActivePlansAsync failed");
        }
        return plans;
    }

    public async Task<UserSubscriptionDto> GetUserSubscriptionAsync(string userId)
    {
        var sub = new UserSubscriptionDto();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSubscriptions_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_USER_PLAN");
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                sub.PlanId             = r["PlanId"].ToString()!;
                sub.PlanCode           = r["PlanCode"].ToString()!;
                sub.PlanName           = r["PlanName"].ToString()!;
                sub.PriceINR           = Convert.ToDecimal(r["PriceINR"]);
                sub.BillingCycle       = r["BillingCycle"].ToString()!;
                sub.MaxPasswords       = Convert.ToInt32(r["MaxPasswords"]);
                sub.MaxCards           = Convert.ToInt32(r["MaxCards"]);
                sub.MaxDocuments       = Convert.ToInt32(r["MaxDocuments"]);
                sub.CanSync            = Convert.ToBoolean(r["CanSync"]);
                sub.SubscriptionStatus = r["SubscriptionStatus"].ToString()!;
                sub.ExpirationDate     = r["ExpirationDate"] != DBNull.Value ? Convert.ToDateTime(r["ExpirationDate"]) : null;
                sub.CurrentPasswords   = Convert.ToInt32(r["CurrentPasswords"]);
                sub.CurrentCards       = Convert.ToInt32(r["CurrentCards"]);
                sub.CurrentDocuments   = Convert.ToInt32(r["CurrentDocuments"]);
                sub.CurrentApiKeys     = Convert.ToInt32(r["CurrentApiKeys"]);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetUserSubscriptionAsync failed for {UserId}", userId);
        }
        return sub;
    }

    public async Task<(bool Success, string Message, string? SubId)> AssignPlanAsync(
        string userId, 
        string? planId, 
        string? planCode, 
        string? orderId, 
        string? paymentId, 
        string? signature)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSubscriptions_CYH");
            cmd.Parameters.AddWithValue("@Flag", "ASSIGN_PLAN");
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@PlanId", (object?)planId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PlanCode", (object?)planCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RazorpayOrderId", (object?)orderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RazorpayPaymentId", (object?)paymentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RazorpaySignature", (object?)signature ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var success = Convert.ToInt32(r["Success"]) > 0;
                var msg = r["Message"].ToString()!;
                var subId = HasColumn(r, "SubscriptionId") && r["SubscriptionId"] != DBNull.Value ? r["SubscriptionId"].ToString() : null;
                return (success, msg, subId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] AssignPlanAsync failed for {UserId}", userId);
        }
        return (false, "Database execution error", null);
    }

    public async Task<QuotaCheckResultDto> CheckQuotaAsync(string userId, string itemType)
    {
        var result = new QuotaCheckResultDto { ItemType = itemType, IsAllowed = true, CurrentCount = 0, MaxLimit = 999999 };
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSubscriptions_CYH");
            cmd.Parameters.AddWithValue("@Flag", "CHECK_QUOTA");
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ItemType", itemType);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                result.IsAllowed    = Convert.ToBoolean(r["IsAllowed"]);
                result.CurrentCount = Convert.ToInt32(r["CurrentCount"]);
                result.MaxLimit     = Convert.ToInt32(r["MaxLimit"]);

                if (!result.IsAllowed)
                {
                    result.Message = $"Free plan limit reached ({result.CurrentCount}/{result.MaxLimit} {itemType}s). Please upgrade to CYHLocker Pro for unlimited storage.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] CheckQuotaAsync failed for {UserId}", userId);
        }
        return result;
    }

    public async Task<bool> UpdatePlanConfigAsync(string planId, decimal price, int maxPass, int maxCards, int maxDocs, bool canSync)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAdminPlans_CYH");
            cmd.Parameters.AddWithValue("@Flag", "UPDATE_PLAN");
            cmd.Parameters.AddWithValue("@PlanId", planId);
            cmd.Parameters.AddWithValue("@PriceINR", price);
            cmd.Parameters.AddWithValue("@MaxPasswords", maxPass);
            cmd.Parameters.AddWithValue("@MaxCards", maxCards);
            cmd.Parameters.AddWithValue("@MaxDocuments", maxDocs);
            cmd.Parameters.AddWithValue("@CanSync", canSync);

            var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] UpdatePlanConfigAsync failed for {PlanId}", planId);
            return false;
        }
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
