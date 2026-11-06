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
}
