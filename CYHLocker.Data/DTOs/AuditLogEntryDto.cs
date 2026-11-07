namespace CYHLocker.Data.DTOs;

public class AuditLogEntryDto
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}
