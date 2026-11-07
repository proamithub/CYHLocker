using System.Text;
using ClosedXML.Excel;
using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;

namespace CYHLocker.Logic.Services;

public class ReportingService : IReportingService
{
    public byte[] GenerateUsersExcelReport(List<AdminUserListItemDto> users)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("CYHLocker Users");

        // Title Header
        ws.Cell(1, 1).Value = "CYHLocker - Master User Directory & Account Audit Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#06b6d4");

        ws.Cell(2, 1).Value = $"Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Total Accounts: {users.Count}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontSize = 9;

        // Table Column Headers
        string[] headers = 
        { 
            "User ID", "Email Address", "Full Name", "Role", "Active Status", 
            "Lockout Status", "Failed Attempts", "Vault Items", "Registered Date (UTC)", "Last Login (UTC)", "Last Login IP" 
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0f172a");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data Rows
        int row = 5;
        foreach (var u in users)
        {
            ws.Cell(row, 1).Value = u.Id;
            ws.Cell(row, 2).Value = u.Email;
            ws.Cell(row, 3).Value = u.FullName;
            ws.Cell(row, 4).Value = u.Role;
            ws.Cell(row, 5).Value = u.IsActive ? "ACTIVE" : "DISABLED";
            ws.Cell(row, 6).Value = u.IsLocked ? "LOCKED" : "NORMAL";
            ws.Cell(row, 7).Value = u.AccessFailedCount;
            ws.Cell(row, 8).Value = u.TotalVaultItems;
            ws.Cell(row, 9).Value = u.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 10).Value = u.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never";
            ws.Cell(row, 11).Value = u.LastLoginIp ?? "N/A";

            // Conditional format for status
            if (!u.IsActive)
                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
            else
                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Green;

            if (u.IsLocked)
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;

            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateAuditLogsExcelReport(List<AuditLogEntryDto> logs)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Security Audit Logs");

        // Title Header
        ws.Cell(1, 1).Value = "CYHLocker - Enterprise Security Audit Stream";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#dc2626");

        ws.Cell(2, 1).Value = $"Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Total Events: {logs.Count}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontSize = 9;

        // Table Column Headers
        string[] headers = { "Event ID", "Timestamp (UTC)", "Event Type", "User Email", "Full Name", "Client IP", "Details / Audit Message" };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e1e2d");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data Rows
        int row = 5;
        foreach (var l in logs)
        {
            ws.Cell(row, 1).Value = l.Id;
            ws.Cell(row, 2).Value = l.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 3).Value = l.EventType;
            ws.Cell(row, 4).Value = l.UserEmail;
            ws.Cell(row, 5).Value = l.UserFullName;
            ws.Cell(row, 6).Value = l.IpAddress ?? "N/A";
            ws.Cell(row, 7).Value = l.Details ?? "";

            // Highlight failed auth in red
            if (l.EventType.Contains("FAIL", StringComparison.OrdinalIgnoreCase) || l.EventType.Contains("LOCK", StringComparison.OrdinalIgnoreCase))
            {
                ws.Cell(row, 3).Style.Font.FontColor = XLColor.Red;
                ws.Cell(row, 3).Style.Font.Bold = true;
            }
            else if (l.EventType.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                ws.Cell(row, 3).Style.Font.FontColor = XLColor.Green;
            }

            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public string GenerateUsersCsvReport(List<AdminUserListItemDto> users)
    {
        var sb = new StringBuilder();
        sb.AppendLine("UserId,Email,FullName,Role,IsActive,IsLocked,AccessFailedCount,TotalVaultItems,CreatedAtUtc,LastLoginAtUtc,LastLoginIp");

        foreach (var u in users)
        {
            sb.AppendLine($"\"{u.Id}\",\"{EscapeCsv(u.Email)}\",\"{EscapeCsv(u.FullName)}\",\"{u.Role}\",{u.IsActive},{u.IsLocked},{u.AccessFailedCount},{u.TotalVaultItems},\"{u.CreatedAt:O}\",\"{u.LastLoginAt:O}\",\"{u.LastLoginIp}\"");
        }

        return sb.ToString();
    }

    public string GenerateAuditLogsCsvReport(List<AuditLogEntryDto> logs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LogId,TimestampUtc,EventType,UserEmail,UserFullName,IpAddress,Details");

        foreach (var l in logs)
        {
            sb.AppendLine($"{l.Id},\"{l.Timestamp:O}\",\"{EscapeCsv(l.EventType)}\",\"{EscapeCsv(l.UserEmail)}\",\"{EscapeCsv(l.UserFullName)}\",\"{l.IpAddress}\",\"{EscapeCsv(l.Details)}\"");
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }
}
