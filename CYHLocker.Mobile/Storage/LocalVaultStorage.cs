using System.Text.Json;
using CYHLocker.Data.DTOs;
using CYHLocker.Mobile.Models;

namespace CYHLocker.Mobile.Storage;

public class LocalVaultStorage : ILocalVaultStorage
{
    private readonly string? _storageFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, LocalVaultItem> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? _lastSyncUtc;

    public LocalVaultStorage(string? storageFilePath = null)
    {
        _storageFilePath = storageFilePath;
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_storageFilePath) || !File.Exists(_storageFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_storageFilePath);
            var container = JsonSerializer.Deserialize<DiskStorageContainer>(json);
            if (container != null)
            {
                _lastSyncUtc = container.LastSyncUtc;
                foreach (var item in container.Items)
                {
                    _cache[item.Id] = item;
                }
            }
        }
        catch
        {
            // Ignore load errors for corrupted files in offline mode
        }
    }

    private void SaveToDisk()
    {
        if (string.IsNullOrWhiteSpace(_storageFilePath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var container = new DiskStorageContainer
            {
                LastSyncUtc = _lastSyncUtc,
                Items = _cache.Values.ToList()
            };

            var json = JsonSerializer.Serialize(container, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storageFilePath, json);
        }
        catch
        {
            // Best-effort disk write
        }
    }

    public async Task<List<LocalVaultItem>> GetAllActiveItemsAsync(string? categoryId = null, string? search = null, string? itemType = null)
    {
        await _lock.WaitAsync();
        try
        {
            var query = _cache.Values.Where(i => !i.IsDeleted && i.SyncState != LocalSyncState.PendingDelete);

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                query = query.Where(i => string.Equals(i.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(itemType) && !itemType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => string.Equals(i.ItemType, itemType, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(i => 
                    (i.Title != null && i.Title.ToLowerInvariant().Contains(s)) ||
                    (i.UrlDomain != null && i.UrlDomain.ToLowerInvariant().Contains(s)) ||
                    (i.SecondaryInfo != null && i.SecondaryInfo.ToLowerInvariant().Contains(s)));
            }

            return query.OrderByDescending(i => i.IsFavorite).ThenByDescending(i => i.UpdatedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LocalVaultItem?> GetItemByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            return _cache.TryGetValue(id, out var item) ? item : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<LocalVaultItem> SaveItemLocallyAsync(LocalVaultItem item)
    {
        await _lock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                item.Id = Guid.NewGuid().ToString();
            }

            item.UpdatedAt = DateTime.UtcNow;

            if (_cache.ContainsKey(item.Id))
            {
                item.SyncState = LocalSyncState.PendingUpdate;
            }
            else
            {
                item.CreatedAt = DateTime.UtcNow;
                item.SyncState = LocalSyncState.PendingCreate;
            }

            _cache[item.Id] = item;
            SaveToDisk();
            return item;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteItemLocallyAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(id, out var item))
            {
                item.IsDeleted = true;
                item.UpdatedAt = DateTime.UtcNow;
                item.SyncState = LocalSyncState.PendingDelete;
                SaveToDisk();
                return true;
            }
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<LocalVaultItem>> GetPendingSyncItemsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return _cache.Values.Where(i => i.SyncState != LocalSyncState.Synced).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MarkItemsAsSyncedAsync(IEnumerable<string> itemIds, DateTime serverSyncTime)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var id in itemIds)
            {
                if (_cache.TryGetValue(id, out var item))
                {
                    if (item.SyncState == LocalSyncState.PendingDelete)
                    {
                        // Clean up deleted item
                        _cache.Remove(id);
                    }
                    else
                    {
                        item.SyncState = LocalSyncState.Synced;
                    }
                }
            }

            _lastSyncUtc = serverSyncTime;
            SaveToDisk();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MergeServerChangesAsync(List<SyncVaultItemDto> serverItems, DateTime serverSyncTime)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var serverItem in serverItems)
            {
                if (_cache.TryGetValue(serverItem.Id, out var localItem))
                {
                    // If local item has pending changes, only overwrite if server is strictly newer
                    if (localItem.SyncState != LocalSyncState.Synced)
                    {
                        if (serverItem.UpdatedAt > localItem.UpdatedAt)
                        {
                            _cache[serverItem.Id] = LocalVaultItem.FromServerDto(serverItem);
                        }
                    }
                    else
                    {
                        // No local pending edits -> take server version directly
                        if (serverItem.IsDeleted)
                        {
                            _cache.Remove(serverItem.Id);
                        }
                        else
                        {
                            _cache[serverItem.Id] = LocalVaultItem.FromServerDto(serverItem);
                        }
                    }
                }
                else if (!serverItem.IsDeleted)
                {
                    _cache[serverItem.Id] = LocalVaultItem.FromServerDto(serverItem);
                }
            }

            _lastSyncUtc = serverSyncTime;
            SaveToDisk();
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<DateTime?> GetLastSyncTimestampAsync() => Task.FromResult(_lastSyncUtc);

    public Task SetLastSyncTimestampAsync(DateTime timestamp)
    {
        _lastSyncUtc = timestamp;
        SaveToDisk();
        return Task.CompletedTask;
    }

    public async Task ClearAllDataAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _cache.Clear();
            _lastSyncUtc = null;
            if (!string.IsNullOrWhiteSpace(_storageFilePath) && File.Exists(_storageFilePath))
            {
                File.Delete(_storageFilePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private class DiskStorageContainer
    {
        public DateTime? LastSyncUtc { get; set; }
        public List<LocalVaultItem> Items { get; set; } = new();
    }
}
