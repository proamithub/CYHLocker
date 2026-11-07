namespace CYHLocker.Data.DTOs;

public class DeviceRegistrationRequestDto
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = "CYHLocker Client";
    public string DeviceType { get; set; } = "BrowserExtension"; // BrowserExtension, Mobile, Web, Desktop
    public string? ClientVersion { get; set; } = "1.0.0";
}

public class DeviceRegistrationResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string? ClientVersion { get; set; }
    public DateTime LastSyncUtc { get; set; }
    public DateTime RegisteredAt { get; set; }
    public bool IsActive { get; set; }
}

public class SyncPullRequestDto
{
    public DateTime? SinceUtc { get; set; }
    public string? DeviceId { get; set; }
}

public class SyncVaultItemDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
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
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class SyncPullResponseDto
{
    public List<SyncVaultItemDto> Items { get; set; } = new();
    public DateTime ServerTimestampUtc { get; set; } = DateTime.UtcNow;
    public int TotalItems { get; set; }
    public DateTime? SinceUtc { get; set; }
}

public class SyncPushItemDto
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
    public string? EncryptionIV { get; set; }
    public string? UrlDomain { get; set; }
    public string? ItemIcon { get; set; }
    public bool IsFavorite { get; set; }
    public string? PasswordStrength { get; set; } = "Strong";
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public string? SecondaryInfo { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? ClientUpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SyncPushRequestDto
{
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceType { get; set; }
    public List<SyncPushItemDto> Items { get; set; } = new();
}

public class SyncPushItemResultDto
{
    public string ItemId { get; set; } = string.Empty;
    public string Status { get; set; } = "INSERTED"; // INSERTED, UPDATED, CONFLICT, FAILED
    public DateTime? ServerUpdatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SyncPushResponseDto
{
    public bool Success { get; set; } = true;
    public int ProcessedCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int ConflictCount { get; set; }
    public DateTime ServerTimestampUtc { get; set; } = DateTime.UtcNow;
    public List<SyncPushItemResultDto> ItemResults { get; set; } = new();
}

public class SyncStatusDto
{
    public int ActiveVaultItems { get; set; }
    public int TrashVaultItems { get; set; }
    public DateTime? LastVaultUpdateUtc { get; set; }
    public DateTime CurrentServerUtc { get; set; } = DateTime.UtcNow;
    public List<DeviceRegistrationResponseDto> RegisteredDevices { get; set; } = new();
}
