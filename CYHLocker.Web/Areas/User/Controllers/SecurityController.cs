using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "User,Admin")]
public class SecurityController : Controller
{
    private string UserName  => User.Identity?.Name ?? "User";
    private string UserEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";
    private bool   IsMfa     => User.Claims.Any(c => c.Type == "mfa_enabled" && c.Value == "true");

    // GET: /User/Security/Index
    public IActionResult Index()
    {
        ViewBag.UserName   = UserName;
        ViewBag.UserEmail  = UserEmail;
        ViewBag.IsMfa      = IsMfa;
        ViewBag.ActivePage = "Security";
        return View();
    }
}
