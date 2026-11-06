using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    private string  UserId           => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private string  UserEmail        => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private string? CurrentSessionId => User.FindFirst("sid")?.Value;

    // ── LOGIN ────────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return RedirectByRole(role);
        }
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequestDto model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var clientIp  = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        var result    = await _auth.LoginAsync(model, clientIp, userAgent);

        if (!result.Success)
        {
            // MFA required — redirect to MFA step
            if (result.RequiresMfa)
            {
                TempData["MfaEmail"]    = model.Email;
                TempData["MfaPassword"] = model.Password;
                return RedirectToAction("MfaChallenge");
            }
            ModelState.AddModelError("", result.Message);
            return View(model);
        }

        await SignInUserAsync(result);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Role-based redirect after login
        return RedirectByRole(result.User!.Role);
    }

    // ── MFA CHALLENGE ────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult MfaChallenge()
    {
        if (TempData["MfaEmail"] == null) return RedirectToAction("Login");
        TempData.Keep();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MfaChallenge(string mfaCode, string? returnUrl = null)
    {
        var email    = TempData["MfaEmail"]?.ToString();
        var password = TempData["MfaPassword"]?.ToString();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            return RedirectToAction("Login");

        var req = new LoginRequestDto { Email = email, Password = password, MfaCode = mfaCode };
        var clientIp  = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        var result    = await _auth.LoginAsync(req, clientIp, userAgent);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            TempData["MfaEmail"]    = email;
            TempData["MfaPassword"] = password;
            return View();
        }

        await SignInUserAsync(result);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Role-based redirect after login
        return RedirectByRole(result.User!.Role);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View("~/Views/Shared/AccessDenied.cshtml");

    // ── REGISTER ─────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequestDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.RegisterAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            return View(model);
        }

        TempData["Success"] = "Account created! Please login.";
        return RedirectToAction("Login");
    }

    // ── LOGOUT ───────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("CYHCookie");
        return RedirectToAction("Index", "Home");
    }

    // ── FORGOT PASSWORD ───────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto model)
    {
        if (!ModelState.IsValid) return View(model);

        await _auth.ForgotPasswordAsync(model.Email);
        // Always show success to prevent email enumeration
        TempData["Success"] = "If that email is registered, a reset link will be sent shortly. Check application logs for the token (dev mode).";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ResetPassword(string token)
    {
        if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");
        return View(new ResetPasswordDto { Token = token });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.ResetPasswordAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("Login");
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private IActionResult RedirectByRole(string? role)
    {
        if (role == "Admin")
            return RedirectToAction("Dashboard", "Admin");
        return RedirectToAction("Dashboard", "User");
    }

    private async Task SignInUserAsync(AuthResponseDto result)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User!.UserId),
            new(ClaimTypes.Email,          result.User.Email),
            new(ClaimTypes.Name,           result.User.FullName),
            new(ClaimTypes.Role,           result.User.Role),
            new("Token",                   result.Token ?? "")
        };
        if (result.SessionId != null)
            claims.Add(new Claim("sid", result.SessionId));

        var identity  = new ClaimsIdentity(claims, "CYHCookie");
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync("CYHCookie", principal);
    }
}
