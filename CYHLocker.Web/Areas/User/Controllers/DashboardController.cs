using System.Security.Claims;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "User,Admin")]
public class DashboardController : Controller
{
    private readonly IVaultService _vaultService;

    public DashboardController(IVaultService vaultService)
    {
        _vaultService = vaultService;
    }

    private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private string UserName  => User.Identity?.Name ?? "User";
    private string UserEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private bool   IsMfa     => User.Claims.Any(c => c.Type == "mfa_enabled" && c.Value == "true");

    // GET: /User/Dashboard/Index
    public async Task<IActionResult> Index()
    {
        ViewBag.UserName   = UserName;
        ViewBag.UserEmail  = UserEmail;
        ViewBag.IsMfa      = IsMfa;
        ViewBag.ActivePage = "Dashboard";

        var items = await _vaultService.GetVaultItemsAsync(CurrentUserId);
        ViewBag.TotalVaultItems = items.Count;
        ViewBag.RecentItems = items.Take(4).ToList();

        return View();
    }
}
