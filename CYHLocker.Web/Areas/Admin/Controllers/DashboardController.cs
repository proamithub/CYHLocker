using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private string AdminName  => User.Identity?.Name ?? "Admin";
    private string AdminEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";

    // GET: /Admin/Dashboard/Index
    public IActionResult Index()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Dashboard";
        return View();
    }

    // GET: /Admin/Dashboard/Users
    public IActionResult Users()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Users";
        return View();
    }

    // GET: /Admin/Dashboard/Reports
    public IActionResult Reports()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Reports";
        return View();
    }

    // GET: /Admin/Dashboard/Settings
    public IActionResult Settings()
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = "Settings";
        return View();
    }
}
