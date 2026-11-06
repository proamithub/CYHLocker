namespace CYHLocker.Data.DTOs;

public class UserDto
{
    public string    UserId          { get; set; } = string.Empty;
    public string    Email           { get; set; } = string.Empty;
    public string    FullName        { get; set; } = string.Empty;
    public string    Role            { get; set; } = "User";
    public string?   PhoneNumber     { get; set; }
    public string?   AvatarUrl       { get; set; }
    public bool      IsActive        { get; set; } = true;
    public bool      IsEmailVerified { get; set; } = false;
    public bool      IsMfaEnabled    { get; set; } = false;
    public DateTime  CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt     { get; set; }
}
