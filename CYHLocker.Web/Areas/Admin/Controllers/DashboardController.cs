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
    private readonly ISubscriptionService _subService;

    public DashboardController(
        IAdminService adminService, 
        IReportingService reporting,
        ISubscriptionService subService)
    {
        _adminService = adminService;
        _reporting = reporting;
        _subService = subService;
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
    public async Task<IActionResult> Settings()
    {
        SetCommonViewBag("Settings");
        var plans = await _subService.GetPlansAsync();
        ViewBag.Plans = plans;
        return View();
    }

    // POST: /Admin/Dashboard/UpdatePlan
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePlan(string planId, decimal price, int maxPass, int maxCards, int maxDocs, bool canSync)
    {
        var ok = await _subService.UpdatePlanConfigAsync(planId, price, maxPass, maxCards, maxDocs, canSync);
        TempData[ok ? "Success" : "Error"] = ok ? "Subscription tier limits updated successfully." : "Failed to update plan.";
        return RedirectToAction(nameof(Settings));
    }

    // POST: /Admin/Dashboard/CreateDatabaseBackup
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDatabaseBackup()
    {
        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var connStr = config.GetConnectionString("DefaultConnection");

            var backupsDir = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
            if (!Directory.Exists(backupsDir))
            {
                Directory.CreateDirectory(backupsDir);
            }

            var fileName = $"CYHLockerDB_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
            var backupPath = Path.Combine(backupsDir, fileName);

            await using (var con = new Microsoft.Data.SqlClient.SqlConnection(connStr))
            {
                await con.OpenAsync();
                var sql = $"BACKUP DATABASE [CYHLockerDB] TO DISK = '{backupPath.Replace("'", "''")}' WITH INIT";
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, con);
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
            }

            var fi = new FileInfo(backupPath);
            var sizeMb = (fi.Length / (1024.0 * 1024.0)).ToString("0.00");

            TempData["Success"] = $"Full database backup created successfully: {fileName} ({sizeMb} MB) in Backups folder.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Backup failed: " + ex.Message;
        }

        return RedirectToAction(nameof(Reports));
    }

    private void SetCommonViewBag(string activePage)
    {
        ViewBag.AdminName  = AdminName;
        ViewBag.AdminEmail = AdminEmail;
        ViewBag.ActivePage = activePage;
    }
}
