using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Controllers;

public class HomeController : Controller
{
    // Landing page — public, no auth required
    // If already logged in → redirect to role-specific dashboard
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            return RedirectToAction("Index", "Dashboard", new { area = "User" });
        }
        return View(); // Views/Home/Index.cshtml = Landing page
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
