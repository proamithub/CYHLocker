using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var clientIp  = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        var result    = await _authService.LoginAsync(request, clientIp, userAgent);

        if (result.RequiresMfa)
            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "MFA_REQUIRED"));

        if (!result.Success) return Unauthorized(ApiResponse<AuthResponseDto>.Fail(result.Message));
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, result.Message));
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Success) return BadRequest(ApiResponse<AuthResponseDto>.Fail(result.Message));
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, result.Message));
    }

    // POST /api/auth/forgot-password
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        await _authService.ForgotPasswordAsync(dto.Email);
        return Ok(ApiResponse<string>.Ok("ok", "If email is registered, a reset link will be sent."));
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var result = await _authService.ResetPasswordAsync(dto);
        if (!result.Success) return BadRequest(ApiResponse<AuthResponseDto>.Fail(result.Message));
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, result.Message));
    }

    // POST /api/auth/change-password  [Authorize]
    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var result = await _authService.ChangePasswordAsync(UserId, dto);
        if (!result.Success) return BadRequest(ApiResponse<AuthResponseDto>.Fail(result.Message));
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, result.Message));
    }

    // GET /api/auth/mfa/setup  [Authorize]
    [HttpGet("mfa/setup"), Authorize]
    public async Task<IActionResult> SetupMfa()
    {
        var email  = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var result = await _authService.SetupMfaAsync(UserId, email);
        return Ok(ApiResponse<MfaSetupDto>.Ok(result, "Scan QR code with your authenticator app."));
    }

    // POST /api/auth/mfa/enable  [Authorize]
    [HttpPost("mfa/enable"), Authorize]
    public async Task<IActionResult> EnableMfa([FromBody] MfaEnableDto dto)
    {
        var ok = await _authService.VerifyAndEnableMfaWithSecretAsync(UserId, dto.Secret, dto.Code);
        if (!ok) return BadRequest(ApiResponse<string>.Fail("Invalid code."));
        return Ok(ApiResponse<string>.Ok("ok", "MFA enabled."));
    }

    // POST /api/auth/mfa/disable  [Authorize]
    [HttpPost("mfa/disable"), Authorize]
    public async Task<IActionResult> DisableMfa([FromBody] MfaDisableDto dto)
    {
        var ok = await _authService.DisableMfaAsync(UserId, dto.Password);
        if (!ok) return BadRequest(ApiResponse<string>.Fail("Invalid password."));
        return Ok(ApiResponse<string>.Ok("ok", "MFA disabled."));
    }

    // GET /api/auth/sessions  [Authorize]
    [HttpGet("sessions"), Authorize]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _authService.GetSessionsAsync(UserId);
        return Ok(ApiResponse<List<SessionDto>>.Ok(sessions, "Sessions retrieved."));
    }

    // DELETE /api/auth/sessions/{sessionId}  [Authorize]
    [HttpDelete("sessions/{sessionId}"), Authorize]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        var sid = User.FindFirst("sid")?.Value ?? "";
        if (sessionId == sid) return BadRequest(ApiResponse<string>.Fail("Cannot revoke current session."));
        var ok = await _authService.RevokeSessionAsync(UserId, sessionId);
        if (!ok) return BadRequest(ApiResponse<string>.Fail("Failed to revoke session."));
        return Ok(ApiResponse<string>.Ok("ok", "Session revoked."));
    }

    // DELETE /api/auth/sessions  [Authorize]  — revoke all except current
    [HttpDelete("sessions"), Authorize]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var sid = User.FindFirst("sid")?.Value ?? "";
        await _authService.RevokeAllSessionsAsync(UserId, sid);
        return Ok(ApiResponse<string>.Ok("ok", "All other sessions revoked."));
    }
}

// Helper DTOs only needed in API layer
public class MfaEnableDto
{
    public string Secret { get; set; } = string.Empty;
    public string Code   { get; set; } = string.Empty;
}

public class MfaDisableDto
{
    public string Password { get; set; } = string.Empty;
}
