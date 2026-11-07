using System.Data;
using CYHLocker.Data.DTOs;
using CYHLocker.Repository.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Repository.Repositories;

public class SyncRepository : ISyncRepository
{
    private readonly string _conn;
    private readonly ILogger<SyncRepository>? _logger;

    public SyncRepository(IConfiguration config, ILogger<SyncRepository>? logger = null)
    {
        _conn = config.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("'DefaultConnection' not found.");
        _logger = logger;
    }

    private static SqlCommand Sp(SqlConnection con, string spName) =>
        new(spName, con) { CommandType = CommandType.StoredProcedure };

    public async Task<DeviceRegistrationResponseDto> RegisterDeviceAsync(string userId, DeviceRegistrationRequestDto request, string? clientIp)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageSync_CYH");
        cmd.Parameters.AddWithValue("@Flag", "REGISTER_DEVICE");
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@DeviceId", request.DeviceId);
        cmd.Parameters.AddWithValue("@DeviceName", (object?)request.DeviceName ?? "CYHLocker Client");
        cmd.Parameters.AddWithValue("@DeviceType", (object?)request.DeviceType ?? "BrowserExtension");
        cmd.Parameters.AddWithValue("@ClientVersion", (object?)request.ClientVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LastIpAddress", (object?)clientIp ?? DBNull.Value);

        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            return new DeviceRegistrationResponseDto
            {
                Id            = r["Id"].ToString() ?? "",
                DeviceId      = r["DeviceId"].ToString() ?? "",
                UserId        = r["UserId"].ToString() ?? "",
                DeviceName    = r["DeviceName"].ToString() ?? "",
                DeviceType    = r["DeviceType"].ToString() ?? "",
                ClientVersion = r["ClientVersion"] == DBNull.Value ? null : r["ClientVersion"].ToString(),
                LastSyncUtc   = Convert.ToDateTime(r["LastSyncUtc"]),
                RegisteredAt  = Convert.ToDateTime(r["RegisteredAt"]),
                IsActive      = Convert.ToBoolean(r["IsActive"])
            };
        }

        return new DeviceRegistrationResponseDto
        {
            DeviceId = request.DeviceId,
            UserId = userId,
            DeviceName = request.DeviceName ?? "CYHLocker Client",
            DeviceType = request.DeviceType ?? "BrowserExtension",
            LastSyncUtc = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public async Task<SyncPullResponseDto> PullChangesAsync(string userId, DateTime? sinceUtc, string? deviceId)
    {
        var response = new SyncPullResponseDto
        {
            SinceUtc = sinceUtc,
            ServerTimestampUtc = DateTime.UtcNow
        };

        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSync_CYH");
            cmd.Parameters.AddWithValue("@Flag", "PULL_CHANGES");
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@DeviceId", (object?)deviceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SinceUtc", (object?)sinceUtc ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var item = new SyncVaultItemDto
                {
                    Id                    = r["Id"].ToString() ?? "",
                    UserId                = r["UserId"].ToString() ?? "",
                    CategoryId            = r["CategoryId"] == DBNull.Value ? null : r["CategoryId"].ToString(),
                    CategoryName          = r["CategoryName"] == DBNull.Value ? null : r["CategoryName"].ToString(),
                    CategoryColor         = r["CategoryColor"] == DBNull.Value ? null : r["CategoryColor"].ToString(),
                    ItemType              = r["ItemType"].ToString() ?? "Login",
                    Title                 = r["Title"].ToString() ?? "",
                    EncryptedUsername     = r["EncryptedUsername"] == DBNull.Value ? null : r["EncryptedUsername"].ToString(),
                    EncryptedPassword     = r["EncryptedPassword"] == DBNull.Value ? null : r["EncryptedPassword"].ToString(),
                    EncryptedUrl          = r["EncryptedUrl"] == DBNull.Value ? null : r["EncryptedUrl"].ToString(),
                    EncryptedNotes        = r["EncryptedNotes"] == DBNull.Value ? null : r["EncryptedNotes"].ToString(),
                    EncryptedCustomFields = r["EncryptedCustomFields"] == DBNull.Value ? null : r["EncryptedCustomFields"].ToString(),
                    EncryptionIV          = r["EncryptionIV"].ToString() ?? "",
                    UrlDomain             = r["UrlDomain"] == DBNull.Value ? null : r["UrlDomain"].ToString(),
                    ItemIcon              = r["ItemIcon"] == DBNull.Value ? null : r["ItemIcon"].ToString(),
                    IsFavorite            = Convert.ToBoolean(r["IsFavorite"]),
                    PasswordStrength      = r["PasswordStrength"] == DBNull.Value ? "Strong" : r["PasswordStrength"].ToString()!,
                    CardBrand             = r["CardBrand"] == DBNull.Value ? null : r["CardBrand"].ToString(),
                    CardLast4             = r["CardLast4"] == DBNull.Value ? null : r["CardLast4"].ToString(),
                    SecondaryInfo         = r["SecondaryInfo"] == DBNull.Value ? null : r["SecondaryInfo"].ToString(),
                    IsDeleted             = Convert.ToBoolean(r["IsDeleted"]),
                    DeletedAt             = r["DeletedAt"] == DBNull.Value ? null : Convert.ToDateTime(r["DeletedAt"]),
                    CreatedAt             = Convert.ToDateTime(r["CreatedAt"]),
                    UpdatedAt             = Convert.ToDateTime(r["UpdatedAt"]),
                    LastUsedAt            = r["LastUsedAt"] == DBNull.Value ? null : Convert.ToDateTime(r["LastUsedAt"])
                };

                if (r["ServerTimestampUtc"] != DBNull.Value)
                {
                    response.ServerTimestampUtc = Convert.ToDateTime(r["ServerTimestampUtc"]);
                }

                response.Items.Add(item);
            }

            response.TotalItems = response.Items.Count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] SyncRepository.PullChangesAsync failed for user {UserId}", userId);
            throw;
        }

        return response;
    }

    public async Task<SyncPushItemResultDto> PushItemAsync(string userId, string? deviceId, SyncPushItemDto item)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSync_CYH");
            cmd.Parameters.AddWithValue("@Flag", "PUSH_ITEM");
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@DeviceId", (object?)deviceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ItemId", item.Id);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)item.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ItemType", (object?)item.ItemType ?? "Login");
            cmd.Parameters.AddWithValue("@Title", (object?)item.Title ?? "Untitled");
            cmd.Parameters.AddWithValue("@EncryptedUsername", (object?)item.EncryptedUsername ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EncryptedPassword", (object?)item.EncryptedPassword ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EncryptedUrl", (object?)item.EncryptedUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EncryptedNotes", (object?)item.EncryptedNotes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EncryptedCustomFields", (object?)item.EncryptedCustomFields ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EncryptionIV", (object?)item.EncryptionIV ?? "");
            cmd.Parameters.AddWithValue("@UrlDomain", (object?)item.UrlDomain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ItemIcon", (object?)item.ItemIcon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsFavorite", item.IsFavorite);
            cmd.Parameters.AddWithValue("@PasswordStrength", (object?)item.PasswordStrength ?? "Strong");
            cmd.Parameters.AddWithValue("@CardBrand", (object?)item.CardBrand ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CardLast4", (object?)item.CardLast4 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SecondaryInfo", (object?)item.SecondaryInfo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsDeleted", item.IsDeleted);
            cmd.Parameters.AddWithValue("@ClientUpdatedAt", (object?)item.ClientUpdatedAt ?? DateTime.UtcNow);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var status = r["Status"].ToString() ?? "INSERTED";
                var serverUpdated = r["ServerUpdatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ServerUpdatedAt"]);

                return new SyncPushItemResultDto
                {
                    ItemId = item.Id,
                    Status = status,
                    ServerUpdatedAt = serverUpdated
                };
            }

            return new SyncPushItemResultDto { ItemId = item.Id, Status = "INSERTED", ServerUpdatedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] SyncRepository.PushItemAsync failed for item {ItemId}", item.Id);
            return new SyncPushItemResultDto
            {
                ItemId = item.Id,
                Status = "FAILED",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<SyncStatusDto> GetSyncStatusAsync(string userId)
    {
        var status = new SyncStatusDto();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSync_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_STATUS");
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            // First result set: Registered devices
            while (await r.ReadAsync())
            {
                status.RegisteredDevices.Add(new DeviceRegistrationResponseDto
                {
                    Id            = r["Id"].ToString() ?? "",
                    DeviceId      = r["DeviceId"].ToString() ?? "",
                    UserId        = r["UserId"].ToString() ?? "",
                    DeviceName    = r["DeviceName"].ToString() ?? "",
                    DeviceType    = r["DeviceType"].ToString() ?? "",
                    ClientVersion = r["ClientVersion"] == DBNull.Value ? null : r["ClientVersion"].ToString(),
                    LastSyncUtc   = Convert.ToDateTime(r["LastSyncUtc"]),
                    RegisteredAt  = Convert.ToDateTime(r["RegisteredAt"]),
                    IsActive      = Convert.ToBoolean(r["IsActive"])
                });
            }

            // Second result set: Summary counts
            if (await r.NextResultAsync() && await r.ReadAsync())
            {
                status.ActiveVaultItems = Convert.ToInt32(r["ActiveVaultItems"]);
                status.TrashVaultItems  = Convert.ToInt32(r["TrashVaultItems"]);
                status.LastVaultUpdateUtc = r["LastVaultUpdateUtc"] == DBNull.Value ? null : Convert.ToDateTime(r["LastVaultUpdateUtc"]);
                status.CurrentServerUtc = Convert.ToDateTime(r["CurrentServerUtc"]);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] SyncRepository.GetSyncStatusAsync failed for user {UserId}", userId);
        }

        return status;
    }
}
