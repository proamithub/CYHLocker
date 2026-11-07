namespace CYHLocker.Data.DTOs;

public class AdminSystemStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int TotalVaultItems { get; set; }
    public int ActiveSessions { get; set; }
    public int FailedLogins24h { get; set; }
    public int TotalAuditEvents { get; set; }
}
