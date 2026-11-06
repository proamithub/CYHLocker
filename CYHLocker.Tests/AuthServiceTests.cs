using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Logic.Services;
using CYHLocker.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CYHLocker.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repo;
    private readonly IAuthService          _auth;
    private readonly ITestOutputHelper     _output;

    public AuthServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _repo = new Mock<IUserRepository>();
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key",      "CYHLocker_SuperSecretKey_2026_AmitLab!" },
                { "Jwt:Issuer",   "CYHLocker.API" },
                { "Jwt:Audience", "CYHLocker.Clients" }
            }).Build();
        _auth = new AuthService(_repo.Object, cfg);
    }

    [Fact]
    public void GenerateAndPrintAdminHash()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123", 11);
        _output.WriteLine($"GENERATED_HASH:{hash}");
        var verify = BCrypt.Net.BCrypt.Verify("Admin@123", hash);
        Assert.True(verify);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsSuccess()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("password123", 4);
        var user = new UserDto { UserId = Guid.NewGuid().ToString(), Email = "test@test.com", FullName = "Test", Role = "User", IsActive = true };
        _repo.Setup(r => r.GetForLoginAsync("test@test.com")).ReturnsAsync((user, "OK", hash));
        _repo.Setup(r => r.GetTotpSecretAsync(user.UserId)).ReturnsAsync((null, false));
        _repo.Setup(r => r.LoginSuccessAsync(It.IsAny<string>(), null, null)).ReturnsAsync(Guid.NewGuid().ToString());

        var result = await _auth.LoginAsync(new LoginRequestDto { Email = "test@test.com", Password = "password123" });

        Assert.True(result.Success);
        Assert.NotNull(result.Token);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsFail()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("correctPassword", 4);
        var user = new UserDto { UserId = Guid.NewGuid().ToString(), Email = "x@x.com", IsActive = true };
        _repo.Setup(r => r.GetForLoginAsync("x@x.com")).ReturnsAsync((user, "OK", hash));
        _repo.Setup(r => r.LoginFailedAsync("x@x.com")).ReturnsAsync(1);

        var result = await _auth.LoginAsync(new LoginRequestDto { Email = "x@x.com", Password = "wrongPassword" });

        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Message);
    }

    [Fact]
    public async Task Login_LockedAccount_ReturnsFail()
    {
        _repo.Setup(r => r.GetForLoginAsync("locked@test.com")).ReturnsAsync(((UserDto?)null, "LOCKED", (string?)null));

        var result = await _auth.LoginAsync(new LoginRequestDto { Email = "locked@test.com", Password = "any" });

        Assert.False(result.Success);
        Assert.Contains("locked", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_EmptyEmail_ReturnsFail()
    {
        var result = await _auth.LoginAsync(new LoginRequestDto { Email = "", Password = "password" });
        Assert.False(result.Success);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
