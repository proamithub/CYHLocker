using System.ComponentModel.DataAnnotations;

namespace CYHLocker.Data.DTOs;

public class VaultCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; } = "📁";
    public string? Color { get; set; } = "#06b6d4";
    public int SortOrder { get; set; }
    public int TotalItems { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCategoryRequestDto
{
    [Required(ErrorMessage = "Category name is required")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; } = "📁";
    public string? Color { get; set; } = "#06b6d4";
}
