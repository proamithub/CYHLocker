using System.Security.Claims;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Controllers;

[Authorize(Roles = "User")]
public class UserController : Controller
{
    private readonly IAuthService _auth;
    private string UserId    => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private string UserName  => User.Identity?.Name ?? "User";
    private string UserEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private bool   IsMfa     => User.Claims.Any(c => c.Type == "mfa_enabled" && c.Value == "true");
    private string? SessionId => User.FindFirst("sid")?.Value;

    public UserController(IAuthService auth) => _auth = auth;

    // ── DASHBOARD ────────────────────────────────────────────────────────────
    public IActionResult Dashboard()
    {
        ViewBag.UserName  = UserName;
        ViewBag.UserEmail = UserEmail;
        ViewBag.IsMfa     = IsMfa;
        ViewBag.ActivePage = "Dashboard";
        return View();
    }

    // ── VAULT (placeholder, Phase 2) ─────────────────────────────────────────
    public IActionResult Vault()
    {
        ViewBag.UserName  = UserName;
        ViewBag.UserEmail = UserEmail;
        ViewBag.ActivePage = "Vault";
        return View();
    }

    // ── PROFILE (delegates to AccountController for full profile logic) ──────
    public IActionResult Profile()
    {
        return RedirectToAction("Profile", "Account");
    }

    // ── SECURITY (placeholder, Phase 2) ──────────────────────────────────────
    public IActionResult Security()
    {
        ViewBag.UserName  = UserName;
        ViewBag.UserEmail = UserEmail;
        ViewBag.IsMfa     = IsMfa;
        ViewBag.ActivePage = "Security";
        return View();
    }
}
