using System.ComponentModel.DataAnnotations;

namespace CYHLocker.Data.DTOs;

public class CreateVaultItemRequestDto
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? CategoryId { get; set; }
    public string ItemType { get; set; } = "Login";

    public string? Username { get; set; }

    [Required(ErrorMessage = "Password is required for login items")]
    public string Password { get; set; } = string.Empty;

    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? CustomFieldsJson { get; set; }
    public string? ItemIcon { get; set; }
    public bool IsFavorite { get; set; }
}

public class UpdateVaultItemRequestDto : CreateVaultItemRequestDto
{
    [Required]
    public string Id { get; set; } = string.Empty;
}
