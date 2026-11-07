using CYHLocker.Data.DTOs;

namespace CYHLocker.Mobile.Sync;

public record OfflineSyncReport(
    bool Success,
    int ItemsPushed,
    int ItemsPulled,
    int Conflicts,
    DateTime ServerTimestampUtc,
    string? ErrorMessage = null
);

public interface IOfflineSyncEngine
{
    void Configure(string apiBaseUrl, string jwtToken, string deviceId, string deviceName = "CYHLocker Mobile");
    Task<bool> RegisterDeviceAsync();
    Task<OfflineSyncReport> SyncNowAsync();
    void StartPeriodicSync(TimeSpan interval);
    void StopPeriodicSync();
    bool IsPeriodicSyncRunning { get; }
}
