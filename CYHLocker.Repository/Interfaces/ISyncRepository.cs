using CYHLocker.Data.DTOs;

namespace CYHLocker.Repository.Interfaces;

public interface ISyncRepository
{
    Task<DeviceRegistrationResponseDto> RegisterDeviceAsync(string userId, DeviceRegistrationRequestDto request, string? clientIp);
    Task<SyncPullResponseDto> PullChangesAsync(string userId, DateTime? sinceUtc, string? deviceId);
    Task<SyncPushItemResultDto> PushItemAsync(string userId, string? deviceId, SyncPushItemDto item);
    Task<SyncStatusDto> GetSyncStatusAsync(string userId);
}
