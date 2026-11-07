using CYHLocker.Data.DTOs;

namespace CYHLocker.Mobile.Models;

public enum LocalSyncState
{
    Synced = 0,
    PendingCreate = 1,
    PendingUpdate = 2,
    PendingDelete = 3
}

public class LocalVaultItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? CategoryId { get; set; }
    public string ItemType { get; set; } = "Login";
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
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public LocalSyncState SyncState { get; set; } = LocalSyncState.Synced;

    public SyncPushItemDto ToPushDto() => new()
    {
        Id                    = Id,
        CategoryId            = CategoryId,
        ItemType              = ItemType,
        Title                 = Title,
        EncryptedUsername     = EncryptedUsername,
        EncryptedPassword     = EncryptedPassword,
        EncryptedUrl          = EncryptedUrl,
        EncryptedNotes        = EncryptedNotes,
        EncryptedCustomFields = EncryptedCustomFields,
        EncryptionIV          = EncryptionIV,
        UrlDomain             = UrlDomain,
        ItemIcon              = ItemIcon,
        IsFavorite            = IsFavorite,
        PasswordStrength      = PasswordStrength,
        CardBrand             = CardBrand,
        CardLast4             = CardLast4,
        SecondaryInfo         = SecondaryInfo,
        IsDeleted             = IsDeleted || SyncState == LocalSyncState.PendingDelete,
        ClientUpdatedAt       = UpdatedAt
    };

    public static LocalVaultItem FromServerDto(SyncVaultItemDto dto) => new()
    {
        Id                    = dto.Id,
        CategoryId            = dto.CategoryId,
        ItemType              = dto.ItemType,
        Title                 = dto.Title,
        EncryptedUsername     = dto.EncryptedUsername,
        EncryptedPassword     = dto.EncryptedPassword,
        EncryptedUrl          = dto.EncryptedUrl,
        EncryptedNotes        = dto.EncryptedNotes,
        EncryptedCustomFields = dto.EncryptedCustomFields,
        EncryptionIV          = dto.EncryptionIV,
        UrlDomain             = dto.UrlDomain,
        ItemIcon              = dto.ItemIcon,
        IsFavorite            = dto.IsFavorite,
        PasswordStrength      = dto.PasswordStrength,
        CardBrand             = dto.CardBrand,
        CardLast4             = dto.CardLast4,
        SecondaryInfo         = dto.SecondaryInfo,
        IsDeleted             = dto.IsDeleted,
        CreatedAt             = dto.CreatedAt,
        UpdatedAt             = dto.UpdatedAt,
        SyncState             = LocalSyncState.Synced
    };
}
