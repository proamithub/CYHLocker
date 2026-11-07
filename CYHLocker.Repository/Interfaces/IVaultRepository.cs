using CYHLocker.Data.DTOs;

namespace CYHLocker.Repository.Interfaces;

public class VaultItemDbRecord
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string ItemType { get; set; } = "Login"; // Login, Social, Card, Document, ApiKey
    public string Title { get; set; } = string.Empty;
    public string? EncryptedUsername { get; set; }
    public string? EncryptedPassword { get; set; }
    public string? EncryptedUrl { get; set; }
    public string? EncryptedNotes { get; set; }
    public string? EncryptedCustomFields { get; set; }
    public string EncryptionIV { get; set; } = string.Empty;
    public string? UrlDomain { get; set; }
    public string? ItemIcon { get; set; }
    public bool IsFavorite { get; set; }
    public string PasswordStrength { get; set; } = "Strong";
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public string? SecondaryInfo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public interface IVaultRepository
{
    Task<List<VaultItemDbRecord>> GetAllItemsAsync(
        string userId, 
        string? categoryId = null, 
        string? searchQuery = null, 
        string? itemType = null, 
        bool onlyFavorites = false);

    Task<VaultItemDbRecord?> GetItemByIdAsync(string userId, string itemId);
    Task<string> InsertItemAsync(VaultItemDbRecord record);
    Task<bool> UpdateItemAsync(VaultItemDbRecord record);
    Task<bool> DeleteItemAsync(string userId, string itemId);
    Task<bool> ToggleFavoriteAsync(string userId, string itemId);
    Task<List<VaultItemDbRecord>> GetAutoFillMatchesAsync(string userId, string urlDomain);
    
    // Trash Management
    Task<List<VaultItemDbRecord>> GetTrashItemsAsync(string userId);
    Task<bool> RestoreFromTrashAsync(string userId, string itemId);
    Task<bool> PermanentlyDeleteItemAsync(string userId, string itemId);
    Task<bool> EmptyTrashAsync(string userId);

    // Sidebar Counts
    Task<VaultSidebarCountsDto> GetCountsAsync(string userId);

    // Categories
    Task<List<VaultCategoryDto>> GetCategoriesAsync(string userId);
    Task<string> InsertCategoryAsync(string userId, string name, string? icon, string? color);
    Task<bool> DeleteCategoryAsync(string userId, string categoryId);
}
