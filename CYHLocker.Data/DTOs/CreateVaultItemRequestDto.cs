using System.ComponentModel.DataAnnotations;

namespace CYHLocker.Data.DTOs;

public class CreateVaultItemRequestDto
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    public string? CategoryId { get; set; }
    
    // Login, Social, Card, Document, ApiKey
    public string ItemType { get; set; } = "Login";

    // ── Login / Social Fields ──────────────────────────────────────
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? CustomFieldsJson { get; set; }

    // ── Payment Card Fields ────────────────────────────────────────
    public string? CardholderName { get; set; }
    public string? CardNumber { get; set; }
    public string? CardBrand { get; set; }      // Visa, MasterCard, RuPay, Amex, Discover
    public string? CardExpiryMonth { get; set; }
    public string? CardExpiryYear { get; set; }
    public string? CardCvv { get; set; }
    public string? CardPin { get; set; }

    // ── Secure Document Fields ─────────────────────────────────────
    public string? DocContent { get; set; }
    public string? DocFileName { get; set; }
    public string? DocFileSize { get; set; }
    public string? DocMimeType { get; set; }

    // ── API Key Fields ─────────────────────────────────────────────
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? ApiEnvironment { get; set; }  // Production, Staging, Development

    // ── Metadata & Customization ───────────────────────────────────
    public string? ItemIcon { get; set; }
    public bool IsFavorite { get; set; }
}

public class UpdateVaultItemRequestDto : CreateVaultItemRequestDto
{
    [Required(ErrorMessage = "Item ID is required")]
    public string Id { get; set; } = string.Empty;
}
