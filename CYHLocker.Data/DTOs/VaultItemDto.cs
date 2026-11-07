namespace CYHLocker.Data.DTOs;

public class VaultItemDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string ItemType { get; set; } = "Login"; // Login, Social, Card, Document, ApiKey
    public string Title { get; set; } = string.Empty;

    // Decrypted fields for client display
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? CustomFieldsJson { get; set; }

    // Card Specific Decrypted Fields
    public string? CardholderName { get; set; }
    public string? CardNumber { get; set; }
    public string? CardBrand { get; set; }      // Visa, MasterCard, RuPay, Amex
    public string? CardLast4 { get; set; }      // Plain text for quick UI display
    public string? CardExpiryMonth { get; set; }
    public string? CardExpiryYear { get; set; }
    public string? CardCvv { get; set; }
    public string? CardPin { get; set; }

    // Document Specific Decrypted Fields
    public string? DocContent { get; set; }
    public string? DocFileName { get; set; }
    public string? DocFileSize { get; set; }
    public string? DocMimeType { get; set; }

    // ApiKey Specific Decrypted Fields
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? ApiEnvironment { get; set; }

    // General Display Subtitle (e.g. username, •••• 4021, 1.8 MB, sk_live_...)
    public string? SecondaryInfo { get; set; }

    // Plain metadata
    public string? UrlDomain { get; set; }
    public string? ItemIcon { get; set; }
    public bool IsFavorite { get; set; }
    public string PasswordStrength { get; set; } = "Strong";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
