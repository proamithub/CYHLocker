using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface IVaultService
{
    Task<List<VaultItemDto>> GetVaultItemsAsync(string userId, string? categoryId = null, string? searchQuery = null);
    Task<VaultItemDto?> GetVaultItemByIdAsync(string userId, string itemId);
    Task<string> CreateVaultItemAsync(string userId, CreateVaultItemRequestDto request);
    Task<bool> UpdateVaultItemAsync(string userId, UpdateVaultItemRequestDto request);
    Task<bool> DeleteVaultItemAsync(string userId, string itemId);
    Task<bool> ToggleFavoriteAsync(string userId, string itemId);
    Task<List<AutoFillMatchDto>> GetAutoFillMatchesAsync(string userId, string urlOrDomain);
    Task<List<VaultCategoryDto>> GetCategoriesAsync(string userId);
    Task<string> CreateCategoryAsync(string userId, CreateCategoryRequestDto request);
    Task<bool> DeleteCategoryAsync(string userId, string categoryId);
}
