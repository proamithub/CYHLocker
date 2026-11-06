namespace CYHLocker.Data.DTOs;

public class AuthResponseDto
{
    public bool     Success     { get; set; }
    public string   Message     { get; set; } = string.Empty;
    public string?  Token       { get; set; }
    public string?  SessionId   { get; set; }
    public bool     RequiresMfa { get; set; }
    public UserDto? User        { get; set; }
}
