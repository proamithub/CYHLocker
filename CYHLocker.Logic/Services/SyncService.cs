using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Repository.Interfaces;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Logic.Services;

public class SyncService : ISyncService
{
    private readonly ISyncRepository _syncRepo;
    private readonly ISubscriptionRepository? _subRepo;
    private readonly IAdminRepository? _adminRepo;
    private readonly ILogger<SyncService>? _logger;

    public SyncService(
        ISyncRepository syncRepo,
        ISubscriptionRepository? subRepo = null,
        IAdminRepository? adminRepo = null,
        ILogger<SyncService>? logger = null)
    {
        _syncRepo  = syncRepo;
        _subRepo   = subRepo;
        _adminRepo = adminRepo;
        _logger    = logger;
    }

    public async Task<DeviceRegistrationResponseDto> RegisterDeviceAsync(string userId, DeviceRegistrationRequestDto request, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new ArgumentException("DeviceId cannot be empty.", nameof(request));
        }

        var result = await _syncRepo.RegisterDeviceAsync(userId, request, clientIp);

        if (_adminRepo != null)
        {
            await _adminRepo.LogAuditAsync(
                userId, 
                "DEVICE_SYNC_REGISTER", 
                clientIp, 
                $"Device registered: {request.DeviceName} ({request.DeviceType})");
        }

        return result;
    }

    public async Task<SyncPullResponseDto> PullChangesAsync(string userId, SyncPullRequestDto request)
    {
        var result = await _syncRepo.PullChangesAsync(userId, request.SinceUtc, request.DeviceId);

        if (_adminRepo != null && result.Items.Count > 0)
        {
            await _adminRepo.LogAuditAsync(
                userId,
                "VAULT_SYNC_PULL",
                null,
                $"Delta pulled {result.Items.Count} item(s) since {request.SinceUtc?.ToString("o") ?? "beginning"}");
        }

        return result;
    }

    public async Task<SyncPushResponseDto> PushChangesAsync(string userId, SyncPushRequestDto request)
    {
        var response = new SyncPushResponseDto
        {
            ProcessedCount = request.Items.Count,
            ServerTimestampUtc = DateTime.UtcNow
        };

        if (request.Items == null || request.Items.Count == 0)
        {
            return response;
        }

        // Quota verification for new items
        if (_subRepo != null)
        {
            var newItems = request.Items.Where(i => !i.IsDeleted).ToList();
            if (newItems.Count > 0)
            {
                var quota = await _subRepo.CheckQuotaAsync(userId, newItems[0].ItemType ?? "Login");
                if (!quota.IsAllowed)
                {
                    _logger?.LogWarning("[CYH] Quota warning during sync push for user {UserId}: {Message}", userId, quota.Message);
                }
            }
        }

        foreach (var item in request.Items)
        {
            var itemResult = await _syncRepo.PushItemAsync(userId, request.DeviceId, item);
            response.ItemResults.Add(itemResult);

            if (itemResult.Status == "INSERTED")
            {
                response.InsertedCount++;
            }
            else if (itemResult.Status == "UPDATED")
            {
                response.UpdatedCount++;
            }
            else if (itemResult.Status == "CONFLICT")
            {
                response.ConflictCount++;
            }
        }

        if (_adminRepo != null)
        {
            await _adminRepo.LogAuditAsync(
                userId,
                "VAULT_SYNC_PUSH",
                null,
                $"Synced {response.ProcessedCount} item(s) from device {request.DeviceId} (Ins:{response.InsertedCount}, Upd:{response.UpdatedCount}, Conf:{response.ConflictCount})");
        }

        return response;
    }

    public Task<SyncStatusDto> GetSyncStatusAsync(string userId) =>
        _syncRepo.GetSyncStatusAsync(userId);
}
