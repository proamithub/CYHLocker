using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface IVaultService
{
    Task<List<VaultItemDto>> GetVaultItemsAsync(
        string userId, 
        string? categoryId = null, 
        string? searchQuery = null, 
        string? itemType = null, 
        bool onlyFavorites = false);

    Task<VaultItemDto?> GetVaultItemByIdAsync(string userId, string itemId);
    Task<string> CreateVaultItemAsync(string userId, CreateVaultItemRequestDto request);
    Task<bool> UpdateVaultItemAsync(string userId, UpdateVaultItemRequestDto request);
    Task<bool> DeleteVaultItemAsync(string userId, string itemId);
    Task<bool> ToggleFavoriteAsync(string userId, string itemId);
    Task<List<AutoFillMatchDto>> GetAutoFillMatchesAsync(string userId, string urlOrDomain);

    // Trash Management
    Task<List<VaultItemDto>> GetTrashItemsAsync(string userId);
    Task<bool> RestoreFromTrashAsync(string userId, string itemId);
    Task<bool> PermanentlyDeleteItemAsync(string userId, string itemId);
    Task<bool> EmptyTrashAsync(string userId);

    // Sidebar Counts
    Task<VaultSidebarCountsDto> GetVaultCountsAsync(string userId);

    // Categories
    Task<List<VaultCategoryDto>> GetCategoriesAsync(string userId);
    Task<string> CreateCategoryAsync(string userId, CreateCategoryRequestDto request);
    Task<bool> UpdateCategoryAsync(string userId, string categoryId, string name, string? icon, string? color);
    Task<bool> DeleteCategoryAsync(string userId, string categoryId);
}
