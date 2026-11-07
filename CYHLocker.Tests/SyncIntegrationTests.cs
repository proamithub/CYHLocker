using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Encryption;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Logic.Services;
using CYHLocker.Mobile.Models;
using CYHLocker.Mobile.Storage;
using CYHLocker.Repository.Interfaces;
using CYHLocker.Repository.Repositories;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CYHLocker.Tests;

public class SyncIntegrationTests
{
    private readonly IConfiguration _config;
    private readonly ISyncRepository _syncRepo;
    private readonly ISyncService _syncService;
    private readonly ICryptoEngine _crypto;
    private const string TestUserId = "DDB63CD8-D2FD-452A-9DD6-BCB332BD3C7B";

    public SyncIntegrationTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" }
            })
            .Build();

        _syncRepo = new SyncRepository(_config);
        _syncService = new SyncService(_syncRepo);
        _crypto = new AesGcmCryptoEngine(_config);
    }

    [Fact]
    public async Task DeviceRegistration_SavesAndRetrievesDevice()
    {
        var deviceId = "ext-test-" + Guid.NewGuid().ToString("N")[..8];
        var req = new DeviceRegistrationRequestDto
        {
            DeviceId = deviceId,
            DeviceName = "Chrome Test Extension",
            DeviceType = "BrowserExtension",
            ClientVersion = "1.0.0"
        };

        var reg = await _syncService.RegisterDeviceAsync(TestUserId, req, "127.0.0.1");

        Assert.NotNull(reg);
        Assert.Equal(deviceId, reg.DeviceId);
        Assert.Equal(TestUserId, reg.UserId);
        Assert.True(reg.IsActive);

        var status = await _syncService.GetSyncStatusAsync(TestUserId);
        Assert.NotNull(status);
        Assert.Contains(status.RegisteredDevices, d => d.DeviceId == deviceId);
    }

    [Fact]
    public async Task SyncPull_InitialSync_ReturnsVaultItems()
    {
        var pullReq = new SyncPullRequestDto
        {
            SinceUtc = null, // Initial sync pulls all items
            DeviceId = "unit-test-device"
        };

        var result = await _syncService.PullChangesAsync(TestUserId, pullReq);

        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.ServerTimestampUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task SyncPush_InsertsAndDeltaPullsItem()
    {
        var testItemId = Guid.NewGuid().ToString();
        var encUser = _crypto.Encrypt("sync_user@example.com");
        var encPass = _crypto.Encrypt("Secret123!");
        var encUrl = _crypto.Encrypt("https://sync-demo.com");

        var checkpointBeforePush = DateTime.UtcNow.AddSeconds(-2);

        // 1. Push new item
        var pushReq = new SyncPushRequestDto
        {
            DeviceId = "sync-test-dev",
            Items = new List<SyncPushItemDto>
            {
                new()
                {
                    Id = testItemId,
                    ItemType = "Login",
                    Title = "Sync Demo Test Item",
                    EncryptedUsername = encUser,
                    EncryptedPassword = encPass,
                    EncryptedUrl = encUrl,
                    UrlDomain = "sync-demo.com",
                    ClientUpdatedAt = DateTime.UtcNow
                }
            }
        };

        var pushRes = await _syncService.PushChangesAsync(TestUserId, pushReq);

        Assert.True(pushRes.Success);
        Assert.Equal(1, pushRes.InsertedCount);
        Assert.Contains(pushRes.ItemResults, r => r.ItemId == testItemId && r.Status == "INSERTED");

        // 2. Delta pull since checkpoint
        var pullReq = new SyncPullRequestDto
        {
            SinceUtc = checkpointBeforePush,
            DeviceId = "sync-test-dev"
        };

        var pullRes = await _syncService.PullChangesAsync(TestUserId, pullReq);

        Assert.NotNull(pullRes);
        var pulledItem = pullRes.Items.FirstOrDefault(i => i.Id == testItemId);
        Assert.NotNull(pulledItem);
        Assert.Equal("Sync Demo Test Item", pulledItem.Title);
        Assert.Equal("sync-demo.com", pulledItem.UrlDomain);

        // 3. Update the item via push
        var updatePushReq = new SyncPushRequestDto
        {
            DeviceId = "sync-test-dev",
            Items = new List<SyncPushItemDto>
            {
                new()
                {
                    Id = testItemId,
                    ItemType = "Login",
                    Title = "Sync Demo Test Item (Updated)",
                    EncryptedUsername = encUser,
                    EncryptedPassword = encPass,
                    EncryptedUrl = encUrl,
                    UrlDomain = "sync-demo.com",
                    ClientUpdatedAt = DateTime.UtcNow
                }
            }
        };

        var updatePushRes = await _syncService.PushChangesAsync(TestUserId, updatePushReq);
        Assert.Equal(1, updatePushRes.UpdatedCount);

        // 4. Soft-delete the item via push
        var deletePushReq = new SyncPushRequestDto
        {
            DeviceId = "sync-test-dev",
            Items = new List<SyncPushItemDto>
            {
                new()
                {
                    Id = testItemId,
                    Title = "Sync Demo Test Item",
                    IsDeleted = true,
                    ClientUpdatedAt = DateTime.UtcNow
                }
            }
        };

        var deletePushRes = await _syncService.PushChangesAsync(TestUserId, deletePushReq);
        Assert.Equal(1, deletePushRes.UpdatedCount);

        // 5. Delta pull should report it as deleted
        var finalPull = await _syncService.PullChangesAsync(TestUserId, new SyncPullRequestDto
        {
            SinceUtc = checkpointBeforePush,
            DeviceId = "sync-test-dev"
        });

        var deletedItem = finalPull.Items.FirstOrDefault(i => i.Id == testItemId);
        Assert.NotNull(deletedItem);
        Assert.True(deletedItem.IsDeleted);
    }

    [Fact]
    public async Task LocalVaultStorage_OfflineCrudAndSyncQueue()
    {
        var storage = new LocalVaultStorage(); // In-memory instance

        var item = new LocalVaultItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Offline Credential",
            UrlDomain = "offline.test",
            ItemType = "Login"
        };

        // 1. Save locally offline
        await storage.SaveItemLocallyAsync(item);

        var activeItems = await storage.GetAllActiveItemsAsync();
        Assert.Single(activeItems);
        Assert.Equal("Offline Credential", activeItems[0].Title);

        var pendingSync = await storage.GetPendingSyncItemsAsync();
        Assert.Single(pendingSync);
        Assert.Equal(LocalSyncState.PendingCreate, pendingSync[0].SyncState);

        // 2. Mark as synced
        await storage.MarkItemsAsSyncedAsync(new[] { item.Id }, DateTime.UtcNow);

        pendingSync = await storage.GetPendingSyncItemsAsync();
        Assert.Empty(pendingSync); // No longer pending

        // 3. Merge server update
        var serverItem = new SyncVaultItemDto
        {
            Id = item.Id,
            Title = "Offline Credential (Synced from Cloud)",
            UrlDomain = "offline.test",
            ItemType = "Login",
            UpdatedAt = DateTime.UtcNow.AddMinutes(1)
        };

        await storage.MergeServerChangesAsync(new List<SyncVaultItemDto> { serverItem }, DateTime.UtcNow);

        var updatedLocal = await storage.GetItemByIdAsync(item.Id);
        Assert.NotNull(updatedLocal);
        Assert.Equal("Offline Credential (Synced from Cloud)", updatedLocal.Title);

        // 4. Delete locally
        await storage.DeleteItemLocallyAsync(item.Id);
        activeItems = await storage.GetAllActiveItemsAsync();
        Assert.Empty(activeItems);

        pendingSync = await storage.GetPendingSyncItemsAsync();
        Assert.Single(pendingSync);
        Assert.Equal(LocalSyncState.PendingDelete, pendingSync[0].SyncState);
    }
}
