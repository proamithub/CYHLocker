using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Repository.Interfaces;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Logic.Services;

public class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepo;
    private readonly ILogger<AdminService>? _logger;

    public AdminService(IAdminRepository adminRepo, ILogger<AdminService>? logger = null)
    {
        _adminRepo = adminRepo;
        _logger = logger;
    }

    public Task<List<AdminUserListItemDto>> GetAllUsersAsync(string? searchQuery = null) =>
        _adminRepo.GetAllUsersAsync(searchQuery);

    public async Task<bool> ToggleUserStatusAsync(string targetUserId)
    {
        var ok = await _adminRepo.ToggleUserStatusAsync(targetUserId);
        if (ok)
        {
            await _adminRepo.LogAuditAsync(targetUserId, "ADMIN_TOGGLE_STATUS", null, "Admin toggled user active status");
        }
        return ok;
    }

    public async Task<bool> ToggleUserLockAsync(string targetUserId)
    {
        var ok = await _adminRepo.ToggleUserLockAsync(targetUserId);
        if (ok)
        {
            await _adminRepo.LogAuditAsync(targetUserId, "ADMIN_TOGGLE_LOCK", null, "Admin toggled user lockout");
        }
        return ok;
    }

    public async Task<bool> ChangeUserRoleAsync(string adminUserId, string targetUserId, string newRole)
    {
        if (adminUserId == targetUserId && newRole != "Admin")
        {
            _logger?.LogWarning("[CYH] Admin {AdminId} attempted to demote own account", adminUserId);
            return false; // Prevent self-demotion
        }

        var ok = await _adminRepo.ChangeUserRoleAsync(targetUserId, newRole);
        if (ok)
        {
            await _adminRepo.LogAuditAsync(targetUserId, "ADMIN_ROLE_CHANGE", null, $"Role changed to {newRole} by admin {adminUserId}");
        }
        return ok;
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(string adminUserId, string targetUserId)
    {
        if (adminUserId == targetUserId)
        {
            return (false, "Cannot delete your own administrator account while logged in.");
        }

        var ok = await _adminRepo.DeleteUserAsync(adminUserId, targetUserId);
        if (ok)
        {
            await _adminRepo.LogAuditAsync(adminUserId, "ADMIN_USER_DELETED", null, $"Target user ID {targetUserId} permanently deleted with cascade");
            return (true, "User and all associated data permanently deleted.");
        }

        return (false, "Failed to delete user account.");
    }

    public Task<List<AuditLogEntryDto>> GetAuditLogsAsync(int top = 200, string? eventType = null) =>
        _adminRepo.GetAuditLogsAsync(top, eventType);

    public Task<AdminSystemStatsDto> GetSystemStatsAsync() =>
        _adminRepo.GetSystemStatsAsync();

    public Task LogSecurityEventAsync(string? userId, string eventType, string? ipAddress, string? details) =>
        _adminRepo.LogAuditAsync(userId, eventType, ipAddress, details);
}
