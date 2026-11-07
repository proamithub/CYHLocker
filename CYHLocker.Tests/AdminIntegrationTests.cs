using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Logic.Services;
using CYHLocker.Repository.Repositories;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CYHLocker.Tests;

public class AdminIntegrationTests
{
    private readonly IConfiguration _config;

    public AdminIntegrationTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" }
            })
            .Build();
    }

    [Fact]
    public async Task Admin_GetAllUsers_ReturnsUsersWithVaultCounts()
    {
        var repo = new AdminRepository(_config);
        var adminService = new AdminService(repo);

        var users = await adminService.GetAllUsersAsync();

        Assert.NotNull(users);
        Assert.NotEmpty(users);
        Assert.Contains(users, u => u.Email == "admin@cyhlocker.com");
    }

    [Fact]
    public async Task Admin_AuditLogging_WritesAndRetrieves()
    {
        var repo = new AdminRepository(_config);
        var adminService = new AdminService(repo);

        var testEvent = "TEST_SECURITY_PING";
        var testDetails = $"Automated test log generated at {DateTime.UtcNow:O}";

        await adminService.LogSecurityEventAsync(null, testEvent, "127.0.0.1", testDetails);

        var logs = await adminService.GetAuditLogsAsync(20, testEvent);

        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.EventType == testEvent && l.Details == testDetails);
    }

    [Fact]
    public async Task Admin_SystemStats_ReturnsValidCounts()
    {
        var repo = new AdminRepository(_config);
        var adminService = new AdminService(repo);

        await adminService.LogSecurityEventAsync(null, "STATS_CHECK", "127.0.0.1", "Initial stat check event");
        var stats = await adminService.GetSystemStatsAsync();

        Assert.NotNull(stats);
        Assert.True(stats.TotalUsers >= 1);
        Assert.True(stats.TotalAuditEvents >= 1);
    }

    [Fact]
    public async Task Admin_ExcelAndCsv_Reports_GenerateValidBytes()
    {
        var repo = new AdminRepository(_config);
        var adminService = new AdminService(repo);
        var reporting = new ReportingService();

        var users = await adminService.GetAllUsersAsync();
        var logs = await adminService.GetAuditLogsAsync(50);

        // 1. Test Excel generation
        var userExcelBytes = reporting.GenerateUsersExcelReport(users);
        Assert.NotNull(userExcelBytes);
        Assert.True(userExcelBytes.Length > 1000); // Valid xlsx zip archive is > 1KB

        var auditExcelBytes = reporting.GenerateAuditLogsExcelReport(logs);
        Assert.NotNull(auditExcelBytes);
        Assert.True(auditExcelBytes.Length > 1000);

        // 2. Test CSV generation
        var userCsv = reporting.GenerateUsersCsvReport(users);
        Assert.NotNull(userCsv);
        Assert.Contains("UserId,Email,FullName", userCsv);
        Assert.Contains("admin@cyhlocker.com", userCsv);

        var auditCsv = reporting.GenerateAuditLogsCsvReport(logs);
        Assert.NotNull(auditCsv);
        Assert.Contains("LogId,TimestampUtc,EventType", auditCsv);
    }

    [Fact]
    public async Task Admin_SelfDelete_IsPrevented()
    {
        var repo = new AdminRepository(_config);
        var adminService = new AdminService(repo);

        var users = await adminService.GetAllUsersAsync();
        var admin = users.First(u => u.Email == "admin@cyhlocker.com");

        // Attempting to delete own account
        var (success, msg) = await adminService.DeleteUserAsync(admin.Id, admin.Id);

        Assert.False(success);
        Assert.Contains("Cannot delete your own administrator account", msg);
    }
}
