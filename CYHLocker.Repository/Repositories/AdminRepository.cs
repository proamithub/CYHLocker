using System.Data;
using CYHLocker.Data.DTOs;
using CYHLocker.Repository.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Repository.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly string _conn;
    private readonly ILogger<AdminRepository>? _logger;

    public AdminRepository(IConfiguration config, ILogger<AdminRepository>? logger = null)
    {
        _conn = config.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("'DefaultConnection' not found.");
        _logger = logger;
    }

    public async Task<List<AdminUserListItemDto>> GetAllUsersAsync(string? searchQuery = null)
    {
        var users = new List<AdminUserListItemDto>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAdminUsers_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_ALL_USERS");
            cmd.Parameters.AddWithValue("@SearchQuery", (object?)searchQuery ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                users.Add(new AdminUserListItemDto
                {
                    Id                = r["Id"].ToString()!,
                    Email             = r["Email"].ToString()!,
                    FullName          = r["FullName"].ToString()!,
                    Role              = r["Role"].ToString()!,
                    IsActive          = Convert.ToBoolean(r["IsActive"]),
                    IsLocked          = Convert.ToBoolean(r["IsLocked"]),
                    LockoutEnd        = r["LockoutEnd"] != DBNull.Value ? Convert.ToDateTime(r["LockoutEnd"]) : null,
                    AccessFailedCount = Convert.ToInt32(r["AccessFailedCount"]),
                    CreatedAt         = Convert.ToDateTime(r["CreatedAt"]),
                    LastLoginAt       = r["LastLoginAt"] != DBNull.Value ? Convert.ToDateTime(r["LastLoginAt"]) : null,
                    LastLoginIp       = r["LastLoginIp"] != DBNull.Value ? r["LastLoginIp"].ToString() : null,
                    TotalVaultItems   = Convert.ToInt32(r["TotalVaultItems"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetAllUsersAsync failed");
        }
        return users;
    }

    public async Task<bool> ToggleUserStatusAsync(string targetUserId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAdminUsers_CYH");
            cmd.Parameters.AddWithValue("@Flag", "TOGGLE_STATUS");
            cmd.Parameters.AddWithValue("@TargetUserId", targetUserId);

            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] ToggleUserStatusAsync failed for {UserId}", targetUserId);
            return false;
        }
    }

    public async Task<bool> ToggleUserLockAsync(string targetUserId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAdminUsers_CYH");
            cmd.Parameters.AddWithValue("@Flag", "TOGGLE_LOCK");
            cmd.Parameters.AddWithValue("@TargetUserId", targetUserId);

            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] ToggleUserLockAsync failed for {UserId}", targetUserId);
            return false;
        }
    }

    public async Task<bool> ChangeUserRoleAsync(string targetUserId, string newRole)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAdminUsers_CYH");
            cmd.Parameters.AddWithValue("@Flag", "CHANGE_ROLE");
            cmd.Parameters.AddWithValue("@TargetUserId", targetUserId);
            cmd.Parameters.AddWithValue("@NewRole", newRole);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0) > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] ChangeUserRoleAsync failed for {UserId}", targetUserId);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string adminUserId, string targetUserId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAdminUsers_CYH");
            cmd.Parameters.AddWithValue("@Flag", "DELETE_USER");
            cmd.Parameters.AddWithValue("@AdminUserId", adminUserId);
            cmd.Parameters.AddWithValue("@TargetUserId", targetUserId);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result ?? 0) > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] DeleteUserAsync failed for {UserId}", targetUserId);
            return false;
        }
    }

    // ── AUDIT LOGGING ──────────────────────────────────────────────────────────
    public async Task LogAuditAsync(string? userId, string eventType, string? ipAddress, string? details)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAuditLogs_CYH");
            cmd.Parameters.AddWithValue("@Flag", "LOG_EVENT");
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EventType", eventType);
            cmd.Parameters.AddWithValue("@IpAddress", (object?)ipAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Details", (object?)details ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] LogAuditAsync failed for {EventType}", eventType);
        }
    }

    public async Task<List<AuditLogEntryDto>> GetAuditLogsAsync(int top = 200, string? eventType = null, string? userId = null)
    {
        var logs = new List<AuditLogEntryDto>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAuditLogs_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_ALL");
            cmd.Parameters.AddWithValue("@TopCount", top);
            cmd.Parameters.AddWithValue("@EventType", (object?)eventType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                logs.Add(new AuditLogEntryDto
                {
                    Id           = Convert.ToInt64(r["Id"]),
                    UserId       = r["UserId"].ToString()!,
                    UserEmail    = r["UserEmail"].ToString()!,
                    UserFullName = r["UserFullName"].ToString()!,
                    EventType    = r["EventType"].ToString()!,
                    IpAddress    = r["IpAddress"] != DBNull.Value ? r["IpAddress"].ToString() : null,
                    Details      = r["Details"] != DBNull.Value ? r["Details"].ToString() : null,
                    Timestamp    = Convert.ToDateTime(r["Timestamp"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetAuditLogsAsync failed");
        }
        return logs;
    }

    public async Task<AdminSystemStatsDto> GetSystemStatsAsync()
    {
        var stats = new AdminSystemStatsDto();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageAuditLogs_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_STATS");

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                stats.TotalUsers        = Convert.ToInt32(r["TotalUsers"]);
                stats.ActiveUsers       = Convert.ToInt32(r["ActiveUsers"]);
                stats.LockedUsers       = Convert.ToInt32(r["LockedUsers"]);
                stats.TotalVaultItems   = Convert.ToInt32(r["TotalVaultItems"]);
                stats.ActiveSessions    = Convert.ToInt32(r["ActiveSessions"]);
                stats.FailedLogins24h   = Convert.ToInt32(r["FailedLogins24h"]);
                stats.TotalAuditEvents  = Convert.ToInt32(r["TotalAuditEvents"]);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetSystemStatsAsync failed");
        }
        return stats;
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };
}
