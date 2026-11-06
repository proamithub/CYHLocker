using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private string AdminName  => User.Identity?.Name ?? "Admin";
    private string AdminEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";

    // ── DASHBOARD ────────────────────────────────────────────────────────────
    public IActionResult Dashboard()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Dashboard";
        return View();
    }

    // ── USER MANAGEMENT (placeholder, Phase 2) ───────────────────────────────
    public IActionResult Users()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Users";
        return View();
    }

    // ── SETTINGS (placeholder, Phase 2) ─────────────────────────────────────
    public IActionResult Settings()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Settings";
        return View();
    }

    // ── REPORTS (placeholder, Phase 2) ──────────────────────────────────────
    public IActionResult Reports()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Reports";
        return View();
    }
}
