using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OtpNet;

namespace CYHLocker.Logic.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository       _repo;
    private readonly IConfiguration        _config;
    private readonly IAdminRepository?     _adminRepo;
    private readonly ILogger<AuthService>? _logger;

    public AuthService(
        IUserRepository repo, 
        IConfiguration config, 
        IAdminRepository? adminRepo = null, 
        ILogger<AuthService>? logger = null)
    {
        _repo      = repo;
        _config    = config;
        _adminRepo = adminRepo;
        _logger    = logger;
    }

    // ── LOGIN ────────────────────────────────────────────────────────────────
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto req, string? clientIp = null, string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Fail("Email and password are required.");

        var (user, status, hash) = await _repo.GetForLoginAsync(req.Email);

        if (status == "LOCKED")
            return Fail("Account is temporarily locked due to multiple failed attempts. Try again in 15 minutes.");

        if (status == "NOT_FOUND" || user == null || hash == null)
        {
            await _repo.LoginFailedAsync(req.Email);
            return Fail("Invalid email or password.");
        }

        // BCrypt verify
        if (!BCrypt.Net.BCrypt.Verify(req.Password, hash))
        {
            var fails     = await _repo.LoginFailedAsync(req.Email);
            var remaining = 5 - fails;
            if (remaining <= 0) return Fail("Account locked for 15 minutes due to too many failed attempts.");
            return Fail($"Invalid email or password. {remaining} attempt(s) remaining.");
        }

        if (!user.IsActive)
            return Fail("Account is disabled. Contact support.");

        // MFA check
        if (user.IsMfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(req.MfaCode))
                return new AuthResponseDto { Success = false, Message = "MFA_REQUIRED", RequiresMfa = true, User = user };

            var (secret, _) = await _repo.GetTotpSecretAsync(user.UserId);
            if (secret == null || !VerifyTotp(secret, req.MfaCode))
                return Fail("Invalid MFA code.");
        }

        // Create session
        var sessionId = await _repo.LoginSuccessAsync(req.Email, clientIp, userAgent);
        var token     = GenerateJwt(user, sessionId);
        _logger?.LogInformation("[CYH] Login success: {Email}", req.Email);

        if (_adminRepo != null)
        {
            await _adminRepo.LogAuditAsync(user.UserId, "AUTH_SUCCESS", clientIp, $"User {req.Email} authenticated via BCrypt");
        }

        return new AuthResponseDto { Success = true, Message = "Login successful.", Token = token, User = user, SessionId = sessionId };
    }

    // ── REGISTER ─────────────────────────────────────────────────────────────
    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.FullName))
            return Fail("All fields are required.");

        if (req.Password.Length < 8)
            return Fail("Password must be at least 8 characters.");

        // BCrypt hash before storing
        req.Password = BCrypt.Net.BCrypt.HashPassword(req.Password, 11);

        var (success, msg, userId) = await _repo.RegisterAsync(req);
        if (!success) return Fail(msg);

        var user = await _repo.GetByIdAsync(userId!);
        return new AuthResponseDto { Success = true, Message = "Registration successful. Please login.", User = user };
    }

    // ── CHANGE PASSWORD ───────────────────────────────────────────────────────
    public async Task<AuthResponseDto> ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmPassword) return Fail("Passwords do not match.");
        if (dto.NewPassword.Length < 8) return Fail("New password must be at least 8 characters.");

        var currentHash = await _repo.GetCurrentPasswordHashAsync(userId);
        if (currentHash == null || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, currentHash))
            return Fail("Current password is incorrect.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, 11);
        var ok = await _repo.UpdatePasswordAsync(userId, newHash);
        return ok ? new AuthResponseDto { Success = true, Message = "Password changed successfully." } : Fail("Failed to update password.");
    }

    // ── FORGOT PASSWORD ───────────────────────────────────────────────────────
    public async Task<bool> ForgotPasswordAsync(string email)
    {
        // Generate a secure token
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var ok    = await _repo.SetResetTokenAsync(email, token);
        if (ok)
        {
            // In production: send email with token link
            // For now: log it (token stored in DB, user can query DB for testing)
            _logger?.LogInformation("[CYH] Password reset token for {Email}: {Token}", email, token);
        }
        return true; // Always return true to prevent email enumeration
    }

    public async Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmPassword) return Fail("Passwords do not match.");
        if (dto.NewPassword.Length < 8) return Fail("Password must be at least 8 characters.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, 11);
        var ok      = await _repo.ResetPasswordAsync(dto.Token, newHash);
        return ok ? new AuthResponseDto { Success = true, Message = "Password reset successful. Please login." } : Fail("Invalid or expired reset link.");
    }

    // ── MFA ───────────────────────────────────────────────────────────────────
    public Task<MfaSetupDto> SetupMfaAsync(string userId, string email)
    {
        // Generate TOTP secret
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret      = Base32Encoding.ToString(secretBytes);
        var appName     = "CYHLocker";
        var qrUrl       = $"otpauth://totp/{Uri.EscapeDataString(appName)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(appName)}&algorithm=SHA1&digits=6&period=30";

        return Task.FromResult(new MfaSetupDto
        {
            Secret    = secret,
            ManualKey = secret,
            QrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={Uri.EscapeDataString(qrUrl)}"
        });
    }

    public Task<bool> VerifyAndEnableMfaAsync(string userId, string code)
    {
        // Not directly usable without the secret — use VerifyAndEnableMfaWithSecretAsync
        return Task.FromResult(false);
    }

    public async Task<bool> VerifyAndEnableMfaWithSecretAsync(string userId, string secret, string code)
    {
        if (!VerifyTotp(secret, code)) return false;
        return await _repo.EnableMfaAsync(userId, secret);
    }

    public async Task<bool> DisableMfaAsync(string userId, string password)
    {
        var currentHash = await _repo.GetCurrentPasswordHashAsync(userId);
        if (currentHash == null || !BCrypt.Net.BCrypt.Verify(password, currentHash)) return false;
        return await _repo.DisableMfaAsync(userId);
    }

    // ── SESSIONS ──────────────────────────────────────────────────────────────
    public Task<List<SessionDto>> GetSessionsAsync(string userId)         => _repo.GetSessionsAsync(userId);
    public Task<bool> RevokeSessionAsync(string userId, string sessionId)  => _repo.RevokeSessionAsync(userId, sessionId);
    public Task<bool> RevokeAllSessionsAsync(string userId, string sid)    => _repo.RevokeAllSessionsAsync(userId, sid);

    // ── PROFILE ───────────────────────────────────────────────────────────────
    public Task<bool> UpdateProfileAsync(string userId, string? fullName, string? phone, string? avatarUrl)
        => _repo.UpdateProfileAsync(userId, fullName, phone, avatarUrl);

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private bool VerifyTotp(string secret, string code)
    {
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(2, 2));
        }
        catch { return false; }
    }

    private string GenerateJwt(UserDto user, string? sessionId = null)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "CYHLocker_SuperSecretKey_2026_AmitLab!"));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.UserId),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name,               user.FullName),
            new(ClaimTypes.Role,               user.Role),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };
        if (sessionId != null) claims.Add(new Claim("sid", sessionId));

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"]   ?? "CYHLocker.API",
            audience:           _config["Jwt:Audience"] ?? "CYHLocker.Clients",
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthResponseDto Fail(string msg) => new() { Success = false, Message = msg };
}
