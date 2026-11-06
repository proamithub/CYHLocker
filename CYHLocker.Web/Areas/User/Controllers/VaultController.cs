using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "User,Admin")]
public class VaultController : Controller
{
    private string UserName  => User.Identity?.Name ?? "User";
    private string UserEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private bool   IsMfa     => User.Claims.Any(c => c.Type == "mfa_enabled" && c.Value == "true");

    // GET: /User/Vault/Index
    public IActionResult Index()
    {
        ViewBag.UserName   = UserName;
        ViewBag.UserEmail  = UserEmail;
        ViewBag.IsMfa      = IsMfa;
        ViewBag.ActivePage = "Vault";
        return View();
    }
}
