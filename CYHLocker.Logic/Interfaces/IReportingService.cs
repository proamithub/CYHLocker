using CYHLocker.Data.DTOs;

namespace CYHLocker.Logic.Interfaces;

public interface IReportingService
{
    byte[] GenerateUsersExcelReport(List<AdminUserListItemDto> users);
    byte[] GenerateAuditLogsExcelReport(List<AuditLogEntryDto> logs);
    string GenerateUsersCsvReport(List<AdminUserListItemDto> users);
    string GenerateAuditLogsCsvReport(List<AuditLogEntryDto> logs);
}
