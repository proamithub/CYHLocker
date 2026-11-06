using System.Data;
using CYHLocker.Data.DTOs;
using CYHLocker.Repository.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Repository.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _conn;
    private readonly ILogger<UserRepository>? _logger;

    public UserRepository(IConfiguration cfg, ILogger<UserRepository>? logger = null)
    {
        _conn = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("'DefaultConnection' not found.");
        _logger = logger;
    }

    // ── AUTH ────────────────────────────────────────────────────────────────

    public async Task<(UserDto? User, string Status, string? PasswordHash)> GetForLoginAsync(string email)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "AuthenticateUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",  "LOGIN");
            cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return (null, "NOT_FOUND", null);

            var status = r["Status"]?.ToString() ?? "OK";
            if (status == "LOCKED") return (null, "LOCKED", null);

            var user = MapUser(r);
            var hash = r["PasswordHash"]?.ToString();
            return (user, status, hash);
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] GetForLoginAsync failed"); return (null, "ERROR", null); }
    }

    public async Task<string?> LoginSuccessAsync(string email, string? clientIp, string? userAgent)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "AuthenticateUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",      "LOGIN_SUCCESS");
            cmd.Parameters.AddWithValue("@Email",     email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@ClientIp",  (object?)clientIp  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) return r["SessionId"]?.ToString();
            return null;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] LoginSuccessAsync failed"); return null; }
    }

    public async Task<int> LoginFailedAsync(string email)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "AuthenticateUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",  "LOGIN_FAILED");
            cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) return Convert.ToInt32(r["FailCount"]);
            return 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] LoginFailedAsync failed"); return 0; }
    }

    public async Task<(bool Success, string Message, string? UserId)> RegisterAsync(RegisterRequestDto dto)
    {
        try
        {
            var id = Guid.NewGuid().ToString();
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",     "REGISTER");
            cmd.Parameters.AddWithValue("@UserId",   id);
            cmd.Parameters.AddWithValue("@Email",    dto.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@FullName", dto.FullName.Trim());
            cmd.Parameters.AddWithValue("@Password", dto.Password); // already BCrypt hashed

            var res = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
            return res > 0 ? (true, "Registration successful.", id) : (false, "Email already registered.", null);
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] RegisterAsync failed"); return (false, ex.Message, null); }
    }

    // ── USER ────────────────────────────────────────────────────────────────

    public async Task<UserDto?> GetByIdAsync(string userId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",   "GET_BY_ID");
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) return MapUser(r);
            return null;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] GetByIdAsync failed"); return null; }
    }

    public async Task<UserDto?> GetByEmailAsync(string email)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",  "GET_BY_EMAIL");
            cmd.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) return MapUser(r);
            return null;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] GetByEmailAsync failed"); return null; }
    }

    public async Task<string?> GetCurrentPasswordHashAsync(string userId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",   "CHANGE_PASSWORD");
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) return r["PasswordHash"]?.ToString();
            return null;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] GetCurrentPasswordHashAsync failed"); return null; }
    }

    public async Task<bool> UpdatePasswordAsync(string userId, string newPasswordHash)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",        "DO_CHANGE_PASSWORD");
            cmd.Parameters.AddWithValue("@UserId",      userId);
            cmd.Parameters.AddWithValue("@NewPassword", newPasswordHash);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] UpdatePasswordAsync failed"); return false; }
    }

    public async Task<bool> UpdateProfileAsync(string userId, string? fullName, string? phone, string? avatarUrl)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",        "UPDATE_PROFILE");
            cmd.Parameters.AddWithValue("@UserId",      userId);
            cmd.Parameters.AddWithValue("@FullName",    (object?)fullName  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PhoneNumber", (object?)phone     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AvatarUrl",   (object?)avatarUrl ?? DBNull.Value);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] UpdateProfileAsync failed"); return false; }
    }

    // ── FORGOT PASSWORD ─────────────────────────────────────────────────────

    public async Task<bool> SetResetTokenAsync(string email, string tokenHash)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",       "FORGOT_PASSWORD_REQUEST");
            cmd.Parameters.AddWithValue("@Email",      email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@ResetToken", tokenHash);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] SetResetTokenAsync failed"); return false; }
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPasswordHash)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",        "FORGOT_PASSWORD_RESET");
            cmd.Parameters.AddWithValue("@ResetToken",  token);
            cmd.Parameters.AddWithValue("@NewPassword", newPasswordHash);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] ResetPasswordAsync failed"); return false; }
    }

    // ── MFA ─────────────────────────────────────────────────────────────────

    public async Task<(string? Secret, bool Enabled)> GetTotpSecretAsync(string userId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",   "GET_TOTP_SECRET");
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                return (r["TotpSecret"]?.ToString(), Convert.ToBoolean(r["TwoFactorEnabled"]));
            return (null, false);
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] GetTotpSecretAsync failed"); return (null, false); }
    }

    public async Task<bool> EnableMfaAsync(string userId, string secret)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",       "ENABLE_MFA");
            cmd.Parameters.AddWithValue("@UserId",     userId);
            cmd.Parameters.AddWithValue("@TotpSecret", secret);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] EnableMfaAsync failed"); return false; }
    }

    public async Task<bool> DisableMfaAsync(string userId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageUser_CYH");
            cmd.Parameters.AddWithValue("@Flag",   "DISABLE_MFA");
            cmd.Parameters.AddWithValue("@UserId", userId);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] DisableMfaAsync failed"); return false; }
    }

    // ── SESSIONS ────────────────────────────────────────────────────────────

    public async Task<List<SessionDto>> GetSessionsAsync(string userId)
    {
        var list = new List<SessionDto>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSession_CYH");
            cmd.Parameters.AddWithValue("@Flag",   "LIST");
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new SessionDto
                {
                    SessionId    = r["SessionId"]?.ToString() ?? "",
                    DeviceName   = r["DeviceName"]?.ToString() ?? "Web Browser",
                    ClientIp     = r["ClientIp"]?.ToString(),
                    UserAgent    = r["UserAgent"]?.ToString(),
                    CreatedAt    = r["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(r["CreatedAt"]) : DateTime.UtcNow,
                    LastActiveAt = r["LastActiveAt"] != DBNull.Value ? Convert.ToDateTime(r["LastActiveAt"]) : DateTime.UtcNow,
                    ExpiresAt    = r["ExpiresAt"] != DBNull.Value ? Convert.ToDateTime(r["ExpiresAt"]) : DateTime.UtcNow.AddDays(30)
                });
            }
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] GetSessionsAsync failed"); }
        return list;
    }

    public async Task<bool> RevokeSessionAsync(string userId, string sessionId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSession_CYH");
            cmd.Parameters.AddWithValue("@Flag",      "REVOKE");
            cmd.Parameters.AddWithValue("@UserId",    userId);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] RevokeSessionAsync failed"); return false; }
    }

    public async Task<bool> RevokeAllSessionsAsync(string userId, string currentSessionId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageSession_CYH");
            cmd.Parameters.AddWithValue("@Flag",      "REVOKE_ALL");
            cmd.Parameters.AddWithValue("@UserId",    userId);
            cmd.Parameters.AddWithValue("@SessionId", currentSessionId);
            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res ?? 0) > 0;
        }
        catch (Exception ex) { _logger?.LogError(ex, "[CYH] RevokeAllSessionsAsync failed"); return false; }
    }

    // ── HELPERS ─────────────────────────────────────────────────────────────

    private static SqlCommand Sp(SqlConnection con, string spName) =>
        new(spName, con) { CommandType = CommandType.StoredProcedure };

    private static UserDto MapUser(SqlDataReader r) => new()
    {
        UserId          = SafeStr(r, "UserId") ?? SafeStr(r, "Id") ?? "",
        Email           = SafeStr(r, "Email")   ?? "",
        FullName        = SafeStr(r, "FullName") ?? "",
        Role            = SafeStr(r, "Role")     ?? "User",
        PhoneNumber     = SafeStr(r, "PhoneNumber"),
        AvatarUrl       = SafeStr(r, "AvatarUrl"),
        IsActive        = SafeBool(r, "IsActive"),
        IsEmailVerified = SafeBool(r, "EmailConfirmed"),
        IsMfaEnabled    = SafeBool(r, "TwoFactorEnabled"),
        CreatedAt       = SafeDateTime(r, "CreatedAt") ?? DateTime.UtcNow,
        LastLoginAt     = SafeDateTime(r, "LastLoginAt")
    };

    private static string? SafeStr(SqlDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : r[col]?.ToString(); } catch { return null; }
    }
    private static bool SafeBool(SqlDataReader r, string col)
    {
        try { return r[col] != DBNull.Value && Convert.ToBoolean(r[col]); } catch { return false; }
    }
    private static DateTime? SafeDateTime(SqlDataReader r, string col)
    {
        try { return r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]); } catch { return null; }
    }
}
