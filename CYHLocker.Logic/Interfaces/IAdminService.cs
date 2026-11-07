using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface IAdminService
{
    Task<List<AdminUserListItemDto>> GetAllUsersAsync(string? searchQuery = null);
    Task<bool> ToggleUserStatusAsync(string targetUserId);
    Task<bool> ToggleUserLockAsync(string targetUserId);
    Task<bool> ChangeUserRoleAsync(string adminUserId, string targetUserId, string newRole);
    Task<(bool Success, string Message)> DeleteUserAsync(string adminUserId, string targetUserId);
    Task<List<AuditLogEntryDto>> GetAuditLogsAsync(int top = 200, string? eventType = null);
    Task<AdminSystemStatsDto> GetSystemStatsAsync();
    Task LogSecurityEventAsync(string? userId, string eventType, string? ipAddress, string? details);
}
