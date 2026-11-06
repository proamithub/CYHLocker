namespace CYHLocker.Data.DTOs;

public class LoginRequestDto
{
    public string  Email    { get; set; } = string.Empty;
    public string  Password { get; set; } = string.Empty;
    public string? MfaCode  { get; set; }  // optional — only if MFA enabled
}
