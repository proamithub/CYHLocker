using CYHLocker.Data.DTOs;

namespace CYHLocker.Repository.Interfaces;

public interface IUserRepository
{
    // Auth
    Task<(UserDto? User, string Status, string? PasswordHash)> GetForLoginAsync(string email);
    Task<string?> LoginSuccessAsync(string email, string? clientIp, string? userAgent);
    Task<int>     LoginFailedAsync(string email);
    Task<(bool Success, string Message, string? UserId)> RegisterAsync(RegisterRequestDto dto);

    // User
    Task<UserDto?> GetByIdAsync(string userId);
    Task<UserDto?> GetByEmailAsync(string email);
    Task<string?>  GetCurrentPasswordHashAsync(string userId);
    Task<bool>     UpdatePasswordAsync(string userId, string newPasswordHash);
    Task<bool>     UpdateProfileAsync(string userId, string? fullName, string? phone, string? avatarUrl);

    // Forgot Password
    Task<bool> SetResetTokenAsync(string email, string tokenHash);
    Task<bool> ResetPasswordAsync(string token, string newPasswordHash);

    // MFA
    Task<(string? Secret, bool Enabled)> GetTotpSecretAsync(string userId);
    Task<bool> EnableMfaAsync(string userId, string secret);
    Task<bool> DisableMfaAsync(string userId);

    // Sessions
    Task<List<SessionDto>> GetSessionsAsync(string userId);
    Task<bool>             RevokeSessionAsync(string userId, string sessionId);
    Task<bool>             RevokeAllSessionsAsync(string userId, string currentSessionId);
}
