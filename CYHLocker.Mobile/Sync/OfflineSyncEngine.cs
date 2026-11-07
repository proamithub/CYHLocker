using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CYHLocker.Data.DTOs;
using CYHLocker.Mobile.Storage;

namespace CYHLocker.Mobile.Sync;

public class OfflineSyncEngine : IOfflineSyncEngine, IDisposable
{
    private readonly ILocalVaultStorage _storage;
    private readonly HttpClient _httpClient;
    private string _apiBaseUrl = "http://localhost:5201/api";
    private string? _jwtToken;
    private string _deviceId = Guid.NewGuid().ToString();
    private string _deviceName = "CYHLocker Client";
    private string _deviceType = "Mobile";
    private Timer? _periodicTimer;
    private bool _isSyncing;

    public bool IsPeriodicSyncRunning => _periodicTimer != null;

    public OfflineSyncEngine(ILocalVaultStorage storage, HttpClient? httpClient = null)
    {
        _storage = storage;
        _httpClient = httpClient ?? new HttpClient();
    }

    public void Configure(string apiBaseUrl, string jwtToken, string deviceId, string deviceName = "CYHLocker Mobile")
    {
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _jwtToken = jwtToken;
        _deviceId = deviceId;
        _deviceName = deviceName;
    }

    private void SetAuthHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_jwtToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
        }
    }

    public async Task<bool> RegisterDeviceAsync()
    {
        try
        {
            var url = $"{_apiBaseUrl}/sync/register-device";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            SetAuthHeader(req);
            req.Content = JsonContent.Create(new DeviceRegistrationRequestDto
            {
                DeviceId = _deviceId,
                DeviceName = _deviceName,
                DeviceType = _deviceType,
                ClientVersion = "1.0.0"
            });

            var res = await _httpClient.SendAsync(req);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<OfflineSyncReport> SyncNowAsync()
    {
        if (_isSyncing)
        {
            return new OfflineSyncReport(false, 0, 0, 0, DateTime.UtcNow, "Sync already in progress.");
        }

        _isSyncing = true;
        try
        {
            int pushed = 0;
            int pulled = 0;
            int conflicts = 0;
            var serverTime = DateTime.UtcNow;

            // ── STEP 1: PUSH LOCAL PENDING CHANGES ─────────────────────────
            var pendingItems = await _storage.GetPendingSyncItemsAsync();
            if (pendingItems.Count > 0)
            {
                var pushDto = new SyncPushRequestDto
                {
                    DeviceId = _deviceId,
                    DeviceName = _deviceName,
                    DeviceType = _deviceType,
                    Items = pendingItems.Select(p => p.ToPushDto()).ToList()
                };

                var pushUrl = $"{_apiBaseUrl}/sync/push";
                using var pushReq = new HttpRequestMessage(HttpMethod.Post, pushUrl);
                SetAuthHeader(pushReq);
                pushReq.Content = JsonContent.Create(pushDto);

                var pushRes = await _httpClient.SendAsync(pushReq);
                if (pushRes.IsSuccessStatusCode)
                {
                    var pushResult = await pushRes.Content.ReadFromJsonAsync<ApiResponse<SyncPushResponseDto>>(
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (pushResult?.Data != null)
                    {
                        pushed = pushResult.Data.InsertedCount + pushResult.Data.UpdatedCount;
                        conflicts = pushResult.Data.ConflictCount;
                        serverTime = pushResult.Data.ServerTimestampUtc;

                        var successfulIds = pushResult.Data.ItemResults
                            .Where(r => r.Status is "INSERTED" or "UPDATED")
                            .Select(r => r.ItemId)
                            .ToList();

                        await _storage.MarkItemsAsSyncedAsync(successfulIds, serverTime);
                    }
                }
                else
                {
                    var err = await pushRes.Content.ReadAsStringAsync();
                    return new OfflineSyncReport(false, 0, 0, 0, DateTime.UtcNow, $"Push failed: {pushRes.StatusCode} - {err}");
                }
            }

            // ── STEP 2: PULL SERVER CHANGES ─────────────────────────────────
            var lastSyncUtc = await _storage.GetLastSyncTimestampAsync();
            var pullDto = new SyncPullRequestDto
            {
                DeviceId = _deviceId,
                SinceUtc = lastSyncUtc
            };

            var pullUrl = $"{_apiBaseUrl}/sync/pull";
            using var pullReq = new HttpRequestMessage(HttpMethod.Post, pullUrl);
            SetAuthHeader(pullReq);
            pullReq.Content = JsonContent.Create(pullDto);

            var pullRes = await _httpClient.SendAsync(pullReq);
            if (pullRes.IsSuccessStatusCode)
            {
                var pullResult = await pullRes.Content.ReadFromJsonAsync<ApiResponse<SyncPullResponseDto>>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (pullResult?.Data != null)
                {
                    pulled = pullResult.Data.Items.Count;
                    serverTime = pullResult.Data.ServerTimestampUtc;

                    await _storage.MergeServerChangesAsync(pullResult.Data.Items, serverTime);
                }
            }
            else
            {
                var err = await pullRes.Content.ReadAsStringAsync();
                return new OfflineSyncReport(false, pushed, 0, conflicts, serverTime, $"Pull failed: {pullRes.StatusCode} - {err}");
            }

            return new OfflineSyncReport(true, pushed, pulled, conflicts, serverTime);
        }
        catch (Exception ex)
        {
            return new OfflineSyncReport(false, 0, 0, 0, DateTime.UtcNow, ex.Message);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void StartPeriodicSync(TimeSpan interval)
    {
        StopPeriodicSync();
        _periodicTimer = new Timer(async _ =>
        {
            try
            {
                await SyncNowAsync();
            }
            catch
            {
                // Background sync silently catches transient network errors
            }
        }, null, interval, interval);
    }

    public void StopPeriodicSync()
    {
        _periodicTimer?.Dispose();
        _periodicTimer = null;
    }

    public void Dispose()
    {
        StopPeriodicSync();
    }
}
