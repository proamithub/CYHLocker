using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface ISyncService
{
    Task<DeviceRegistrationResponseDto> RegisterDeviceAsync(string userId, DeviceRegistrationRequestDto request, string? clientIp);
    Task<SyncPullResponseDto> PullChangesAsync(string userId, SyncPullRequestDto request);
    Task<SyncPushResponseDto> PushChangesAsync(string userId, SyncPushRequestDto request);
    Task<SyncStatusDto> GetSyncStatusAsync(string userId);
}
