using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Encryption;
using CYHLocker.Logic.Services;
using CYHLocker.Repository.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CYHLocker.Tests;

public class VaultPhase2IntegrationTests
{
    private readonly IConfiguration _config;

    public VaultPhase2IntegrationTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Server=localhost;Database=CYHLockerDB;Trusted_Connection=True;TrustServerCertificate=True;" },
                { "Vault:MasterKey", "CYHLocker_SuperVaultMasterKey_AES256_2026_AmitLab!" }
            })
            .Build();
    }

    [Fact]
    public void AesGcm_Encrypt_And_Decrypt_Success()
    {
        var crypto = new AesGcmCryptoEngine(_config);
        var plain = "MySuperSecretBankPassword!@#123";

        var encrypted = crypto.Encrypt(plain);
        Assert.NotNull(encrypted);
        Assert.NotEqual(plain, encrypted);

        var decrypted = crypto.Decrypt(encrypted);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void AesGcm_TamperedPayload_ThrowsException()
    {
        var crypto = new AesGcmCryptoEngine(_config);
        var plain = "ConfidentialPassword";
        var encrypted = crypto.Encrypt(plain);

        // Tamper with the ciphertext
        var bytes = Convert.FromBase64String(encrypted);
        bytes[^1] ^= 0x01; // flip last bit
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
        {
            crypto.Decrypt(tampered);
        });
    }

    [Fact]
    public void PasswordGenerator_GeneratesExpectedLengthAndStrength()
    {
        var gen = new PasswordGeneratorService();
        var options = new PasswordGeneratorOptionsDto
        {
            Length = 20,
            IncludeUppercase = true,
            IncludeLowercase = true,
            IncludeNumbers = true,
            IncludeSymbols = true
        };

        var result = gen.GeneratePassword(options);

        Assert.Equal(20, result.Password.Length);
        Assert.Contains(result.Strength, new[] { "Strong", "Very Strong" });
        Assert.True(result.EntropyBits > 80);
    }

    [Fact]
    public async Task RealDatabase_VaultCrud_And_AutoFill_Success()
    {
        var crypto = new AesGcmCryptoEngine(_config);
        var passGen = new PasswordGeneratorService();
        var repo = new VaultRepository(_config);
        var vaultService = new VaultService(repo, crypto, passGen);

        // 1. Get user id for user@cyhlocker.com
        string userId;
        await using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await con.OpenAsync();
            await using var cmd = new SqlCommand("SELECT TOP 1 Id FROM Users_CYH WHERE NormalizedEmail = 'USER@CYHLOCKER.COM'", con);
            userId = (await cmd.ExecuteScalarAsync())?.ToString() ?? throw new Exception("User not found");
        }

        // 2. Create category
        var catId = await vaultService.CreateCategoryAsync(userId, new CreateCategoryRequestDto
        {
            Name = "Social Media",
            Icon = "🌐",
            Color = "#3b82f6"
        });
        Assert.NotNull(catId);

        // 3. Create Vault Item
        var plainPassword = "MyGooglePassword#9988!";
        var plainUsername = "amit.test@gmail.com";
        var siteUrl = "https://accounts.google.com/signin";

        var itemId = await vaultService.CreateVaultItemAsync(userId, new CreateVaultItemRequestDto
        {
            Title = "Google Account Test",
            CategoryId = catId,
            ItemType = "Login",
            Username = plainUsername,
            Password = plainPassword,
            Url = siteUrl,
            Notes = "Test recovery codes: 1234-5678",
            IsFavorite = true
        });
        Assert.NotNull(itemId);

        // 4. Verify in database that password in table is NOT plain text!
        await using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await con.OpenAsync();
            await using var cmd = new SqlCommand("SELECT EncryptedPassword, EncryptedUsername, UrlDomain FROM VaultItems_CYH WHERE Id = @Id", con);
            cmd.Parameters.AddWithValue("@Id", itemId);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            var rawEncryptedPass = reader["EncryptedPassword"].ToString();
            var rawEncryptedUser = reader["EncryptedUsername"].ToString();
            var urlDomain = reader["UrlDomain"].ToString();

            Assert.NotEqual(plainPassword, rawEncryptedPass);
            Assert.NotEqual(plainUsername, rawEncryptedUser);
            Assert.Equal("accounts.google.com", urlDomain);
        }

        // 5. Retrieve via VaultService (should decrypt cleanly)
        var item = await vaultService.GetVaultItemByIdAsync(userId, itemId);
        Assert.NotNull(item);
        Assert.Equal(plainPassword, item.Password);
        Assert.Equal(plainUsername, item.Username);
        Assert.Equal(siteUrl, item.Url);
        Assert.Equal("Social Media", item.CategoryName);

        // 6. Test AutoFill Match with domain
        var autoFillMatches = await vaultService.GetAutoFillMatchesAsync(userId, "accounts.google.com");
        Assert.NotEmpty(autoFillMatches);
        Assert.Contains(autoFillMatches, m => m.Id == itemId && m.Password == plainPassword);

        // 7. Cleanup test item
        await vaultService.DeleteVaultItemAsync(userId, itemId);
        await vaultService.DeleteCategoryAsync(userId, catId);
    }
}
