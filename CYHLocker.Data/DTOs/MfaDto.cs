namespace CYHLocker.Data.DTOs;

public class MfaSetupDto
{
    public string Secret    { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public string ManualKey { get; set; } = string.Empty;
}

public class MfaVerifyDto
{
    public string Code { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword     { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
