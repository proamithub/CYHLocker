namespace CYHLocker.Data.DTOs;

public class AutoFillMatchDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? UrlDomain { get; set; }
    public string? ItemIcon { get; set; }
}
