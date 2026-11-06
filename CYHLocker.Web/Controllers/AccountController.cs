using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly IAuthService _auth;

    public AccountController(IAuthService auth) => _auth = auth;

    private string  UserId           => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private string  UserEmail        => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private string? CurrentSessionId => User.FindFirst("sid")?.Value;

    // ── PROFILE ───────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        // Get sessions for the account page
        var sessions = await _auth.GetSessionsAsync(UserId);
        if (CurrentSessionId != null)
        {
            var current = sessions.FirstOrDefault(s => s.SessionId == CurrentSessionId);
            if (current != null) current.IsCurrent = true;
        }
        ViewBag.Sessions = sessions;
        ViewBag.CurrentSessionId = CurrentSessionId;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string? phoneNumber, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["Error"] = "Full name is required.";
            return RedirectToAction("Profile");
        }

        var ok = await _auth.UpdateProfileAsync(UserId, fullName.Trim(), phoneNumber?.Trim(), avatarUrl?.Trim());
        TempData[ok ? "Success" : "Error"] = ok ? "Profile updated successfully." : "Failed to update profile.";
        return RedirectToAction("Profile");
    }

    // ── CHANGE PASSWORD ───────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.ChangePasswordAsync(UserId, model);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("Profile");
    }

    // ── MFA SETUP ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> SetupMfa()
    {
        var setup = await _auth.SetupMfaAsync(UserId, UserEmail);
        TempData["MfaSecret"] = setup.Secret;
        return View(setup);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableMfa(string code)
    {
        var secret = TempData["MfaSecret"]?.ToString();
        if (string.IsNullOrEmpty(secret))
        {
            TempData["Error"] = "MFA session expired. Please start again.";
            return RedirectToAction("SetupMfa");
        }

        var ok = await _auth.VerifyAndEnableMfaWithSecretAsync(UserId, secret, code);
        if (!ok)
        {
            TempData["MfaSecret"] = secret;
            TempData["Error"] = "Invalid code. Please try again.";
            return RedirectToAction("SetupMfa");
        }

        TempData["Success"] = "Two-Factor Authentication enabled successfully!";
        return RedirectToAction("Profile");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableMfa(string password)
    {
        var ok = await _auth.DisableMfaAsync(UserId, password);
        TempData[ok ? "Success" : "Error"] = ok ? "MFA disabled successfully." : "Invalid password.";
        return RedirectToAction("Profile");
    }

    // ── SESSIONS ──────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        if (sessionId == CurrentSessionId)
        {
            TempData["Error"] = "Cannot revoke your current session. Use Logout instead.";
            return RedirectToAction("Profile");
        }

        var ok = await _auth.RevokeSessionAsync(UserId, sessionId);
        TempData[ok ? "Success" : "Error"] = ok ? "Session revoked." : "Failed to revoke session.";
        return RedirectToAction("Profile");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var sid = CurrentSessionId ?? "";
        var ok  = await _auth.RevokeAllSessionsAsync(UserId, sid);
        TempData[ok ? "Success" : "Error"] = ok ? "All other sessions revoked." : "Failed to revoke sessions.";
        return RedirectToAction("Profile");
    }
}
