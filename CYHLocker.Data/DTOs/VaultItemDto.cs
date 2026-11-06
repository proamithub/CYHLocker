namespace CYHLocker.Data.DTOs;

public class VaultItemDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string ItemType { get; set; } = "Login"; // Login, Card, Identity, ApiKey
    public string Title { get; set; } = string.Empty;

    // Decrypted fields for client display
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? CustomFieldsJson { get; set; }

    // Plain metadata
    public string? UrlDomain { get; set; }
    public string? ItemIcon { get; set; }
    public bool IsFavorite { get; set; }
    public string PasswordStrength { get; set; } = "Strong";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
