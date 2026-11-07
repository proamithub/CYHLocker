using CYHLocker.Data.DTOs;
using CYHLocker.Mobile.Models;

namespace CYHLocker.Mobile.Storage;

public interface ILocalVaultStorage
{
    Task<List<LocalVaultItem>> GetAllActiveItemsAsync(string? categoryId = null, string? search = null, string? itemType = null);
    Task<LocalVaultItem?> GetItemByIdAsync(string id);
    Task<LocalVaultItem> SaveItemLocallyAsync(LocalVaultItem item);
    Task<bool> DeleteItemLocallyAsync(string id);
    Task<List<LocalVaultItem>> GetPendingSyncItemsAsync();
    Task MarkItemsAsSyncedAsync(IEnumerable<string> itemIds, DateTime serverSyncTime);
    Task MergeServerChangesAsync(List<SyncVaultItemDto> serverItems, DateTime serverSyncTime);
    Task<DateTime?> GetLastSyncTimestampAsync();
    Task SetLastSyncTimestampAsync(DateTime timestamp);
    Task ClearAllDataAsync();
}
