using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Controllers;

public class HomeController : Controller
{
    // Public landing page — no [Authorize]
    public IActionResult Index()
    {
        // If already logged in → go to Dashboard
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard");

        return View("Landing");
    }

    // Dashboard requires login
    [Authorize]
    public IActionResult Dashboard()
    {
        ViewBag.UserName = User.Identity?.Name ?? "User";
        ViewBag.Email    = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
        ViewBag.Role     = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value   ?? "User";
        ViewBag.IsMfa    = User.Claims.Any(c => c.Type == "mfa_enabled" && c.Value == "true");
        return View("Index"); // existing Index.cshtml = Dashboard
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
