using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Services;
using CYHLocker.Repository.Repositories;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CYHLocker.Tests;

public class DatabaseLoginIntegrationTest
{
    [Fact]
    public async Task RealDatabase_AdminLogin_Success()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" },
                { "Jwt:Key", "CYHLocker_SuperSecretKey_2026_AmitLab!" },
                { "Jwt:Issuer", "CYHLocker.API" },
                { "Jwt:Audience", "CYHLocker.Clients" }
            })
            .Build();

        var repo = new UserRepository(config);
        var authService = new AuthService(repo, config);

        var response = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "admin@cyhlocker.com",
            Password = "Admin@123"
        });

        Assert.True(response.Success, $"Login failed with message: {response.Message}");
        Assert.NotNull(response.Token);
        Assert.Equal("admin@cyhlocker.com", response.User?.Email);
    }

    [Fact]
    public async Task Seed_And_Verify_StandardUser_In_Database()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" },
                { "Jwt:Key", "CYHLocker_SuperSecretKey_2026_AmitLab!" },
                { "Jwt:Issuer", "CYHLocker.API" },
                { "Jwt:Audience", "CYHLocker.Clients" }
            })
            .Build();

        var repo = new UserRepository(config);
        var authService = new AuthService(repo, config);

        // Check if user@cyhlocker.com exists, if not register/insert
        await using (var con = new Microsoft.Data.SqlClient.SqlConnection(config.GetConnectionString("DefaultConnection")))
        {
            await con.OpenAsync();
            var checkCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT COUNT(1) FROM Users_CYH WHERE NormalizedEmail = 'USER@CYHLOCKER.COM'", con);
            var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0) > 0;
            if (!exists)
            {
                var hash = BCrypt.Net.BCrypt.HashPassword("User@123", 11);
                var insertCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    INSERT INTO Users_CYH (Id, Email, NormalizedEmail, FullName, PasswordHash, Role, IsActive, EmailConfirmed, AccessFailedCount, LockoutEnabled, CreatedAt, UpdatedAt)
                    VALUES (NEWID(), 'user@cyhlocker.com', 'USER@CYHLOCKER.COM', 'CYH User', @Hash, 'User', 1, 1, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME())", con);
                insertCmd.Parameters.AddWithValue("@Hash", hash);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        // Now test login with user@cyhlocker.com
        var response = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "user@cyhlocker.com",
            Password = "User@123"
        });

        Assert.True(response.Success, $"User Login failed: {response.Message}");
        Assert.NotNull(response.Token);
        Assert.Equal("user@cyhlocker.com", response.User?.Email);
        Assert.Equal("User", response.User?.Role);
    }

    [Fact]
    public async Task Deploy_Phase2_VaultSchema_ToDatabase()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" }
            })
            .Build();

        var scriptPath = @"D:\MyAppAi\CYHLocker\Scripts\05_Vault_Phase2_Schema.sql";
        var sqlText = await File.ReadAllTextAsync(scriptPath);

        await using var con = new Microsoft.Data.SqlClient.SqlConnection(config.GetConnectionString("DefaultConnection"));
        await con.OpenAsync();

        // Split by GO
        var batches = System.Text.RegularExpressions.Regex.Split(sqlText, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(trimmed, con);
            await cmd.ExecuteNonQueryAsync();
        }

        // Verify tables exist
        await using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('VaultItems_CYH', 'VaultCategories_CYH', 'SecureNotes_CYH', 'VaultAttachments_CYH')", con))
        {
            var count = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);
            Assert.Equal(4, count);
        }
    }

    [Fact]
    public async Task Deploy_Phase3_ExtendedVaultSchema_ToDatabase()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" }
            })
            .Build();

        var scriptPath = @"D:\MyAppAi\CYHLocker\Scripts\06_Extended_Vault_Phase3.sql";
        var sqlText = await File.ReadAllTextAsync(scriptPath);

        await using var con = new Microsoft.Data.SqlClient.SqlConnection(config.GetConnectionString("DefaultConnection"));
        await con.OpenAsync();

        var batches = System.Text.RegularExpressions.Regex.Split(sqlText, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(trimmed, con);
            await cmd.ExecuteNonQueryAsync();
        }

        // Verify columns CardBrand, CardLast4, SecondaryInfo exist
        await using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('VaultItems_CYH') AND name IN ('CardBrand', 'CardLast4', 'SecondaryInfo')", con))
        {
            var count = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);
            Assert.Equal(3, count);
        }
    }
}
