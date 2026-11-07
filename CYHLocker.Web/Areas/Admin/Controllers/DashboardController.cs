using System.Security.Claims;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CYHLocker.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly IAdminService _adminService;
    private readonly IReportingService _reporting;

    public DashboardController(IAdminService adminService, IReportingService reporting)
    {
        _adminService = adminService;
        _reporting = reporting;
    }

    private string CurrentAdminId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private string AdminName  => User.Identity?.Name ?? "Admin";
    private string AdminEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "";

    // GET: /Admin/Dashboard/Index
    public async Task<IActionResult> Index()
    {
        SetCommonViewBag("Dashboard");
        var stats = await _adminService.GetSystemStatsAsync();
        var recentLogs = await _adminService.GetAuditLogsAsync(6);

        ViewBag.Stats = stats;
        ViewBag.RecentLogs = recentLogs;
        return View();
    }

    // GET: /Admin/Dashboard/Users?q=...
    public async Task<IActionResult> Users(string? q)
    {
        SetCommonViewBag("Users");
        ViewBag.SearchQuery = q;

        var users = await _adminService.GetAllUsersAsync(q);
        return View(users);
    }

    // POST: /Admin/Dashboard/ToggleStatus
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var ok = await _adminService.ToggleUserStatusAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "User active status updated." : "Failed to update status.";
        return RedirectToAction(nameof(Users));
    }

    // POST: /Admin/Dashboard/ToggleLock
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var ok = await _adminService.ToggleUserLockAsync(id);
        TempData[ok ? "Success" : "Error"] = ok ? "User lockout state toggled." : "Failed to toggle lock.";
        return RedirectToAction(nameof(Users));
    }

    // POST: /Admin/Dashboard/ChangeRole
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role)
    {
        var ok = await _adminService.ChangeUserRoleAsync(CurrentAdminId, id, role);
        TempData[ok ? "Success" : "Error"] = ok ? $"User role updated to '{role}'." : "Action prevented (cannot demote self).";
        return RedirectToAction(nameof(Users));
    }

    // POST: /Admin/Dashboard/DeleteUser
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var (ok, msg) = await _adminService.DeleteUserAsync(CurrentAdminId, id);
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction(nameof(Users));
    }

    // GET: /Admin/Dashboard/Reports?eventType=...
    public async Task<IActionResult> Reports(string? eventType)
    {
        SetCommonViewBag("Reports");
        ViewBag.SelectedEventType = string.IsNullOrEmpty(eventType) ? "ALL" : eventType;

        var logs = await _adminService.GetAuditLogsAsync(200, eventType);
        return View(logs);
    }

    // GET: /Admin/Dashboard/ExportUsers?format=xlsx|csv
    [HttpGet]
    public async Task<IActionResult> ExportUsers(string format = "xlsx")
    {
        var users = await _adminService.GetAllUsersAsync();
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmm");

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = _reporting.GenerateUsersCsvReport(users);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"CYHLocker_Users_{dateStr}.csv");
        }

        var bytes = _reporting.GenerateUsersExcelReport(users);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"CYHLocker_Users_{dateStr}.xlsx");
    }

    // GET: /Admin/Dashboard/ExportAuditLogs?format=xlsx|csv
    [HttpGet]
    public async Task<IActionResult> ExportAuditLogs(string format = "xlsx", string? eventType = null)
    {
        var logs = await _adminService.GetAuditLogsAsync(1000, eventType);
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmm");

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = _reporting.GenerateAuditLogsCsvReport(logs);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"CYHLocker_AuditLogs_{dateStr}.csv");
        }

        var bytes = _reporting.GenerateAuditLogsExcelReport(logs);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"CYHLocker_AuditLogs_{dateStr}.xlsx");
    }

    // GET: /Admin/Dashboard/Settings
    public IActionResult Settings()
    {
        SetCommonViewBag("Settings");
        return View();
    }

    private void SetCommonViewBag(string activePage)
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = activePage;
    }
}
