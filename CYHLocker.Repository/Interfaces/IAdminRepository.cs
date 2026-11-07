using CYHLocker.Data.DTOs;

namespace CYHLocker.Repository.Interfaces;

public interface IAdminRepository
{
    // User Management
    Task<List<AdminUserListItemDto>> GetAllUsersAsync(string? searchQuery = null);
    Task<bool> ToggleUserStatusAsync(string targetUserId);
    Task<bool> ToggleUserLockAsync(string targetUserId);
    Task<bool> ChangeUserRoleAsync(string targetUserId, string newRole);
    Task<bool> DeleteUserAsync(string adminUserId, string targetUserId);

    // Audit Logging
    Task LogAuditAsync(string? userId, string eventType, string? ipAddress, string? details);
    Task<List<AuditLogEntryDto>> GetAuditLogsAsync(int top = 200, string? eventType = null, string? userId = null);

    // System Dashboard Stats
    Task<AdminSystemStatsDto> GetSystemStatsAsync();
}
