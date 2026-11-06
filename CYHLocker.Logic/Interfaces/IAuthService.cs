using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto>  LoginAsync(LoginRequestDto request, string? clientIp = null, string? userAgent = null);
    Task<AuthResponseDto>  RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto>  ChangePasswordAsync(string userId, ChangePasswordDto dto);
    Task<bool>             ForgotPasswordAsync(string email);
    Task<AuthResponseDto>  ResetPasswordAsync(ResetPasswordDto dto);
    Task<MfaSetupDto>      SetupMfaAsync(string userId, string email);
    Task<bool>             VerifyAndEnableMfaAsync(string userId, string code);
    Task<bool>             VerifyAndEnableMfaWithSecretAsync(string userId, string secret, string code);
    Task<bool>             DisableMfaAsync(string userId, string password);
    Task<List<SessionDto>> GetSessionsAsync(string userId);
    Task<bool>             RevokeSessionAsync(string userId, string sessionId);
    Task<bool>             RevokeAllSessionsAsync(string userId, string currentSessionId);
    Task<bool>             UpdateProfileAsync(string userId, string? fullName, string? phone, string? avatarUrl);
}
