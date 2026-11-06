namespace CYHLocker.Data.DTOs;

public class SessionDto
{
    public string   SessionId    { get; set; } = string.Empty;
    public string   DeviceName   { get; set; } = "Web Browser";
    public string?  ClientIp     { get; set; }
    public string?  UserAgent    { get; set; }
    public DateTime CreatedAt    { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime ExpiresAt    { get; set; }
    public bool     IsRevoked    { get; set; }
    public bool     IsCurrent    { get; set; }
}
