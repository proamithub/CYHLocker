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
    public void FaviconService_ResolvesBrands_And_DetectsCards()
    {
        var favicon = new FaviconService();

        // 1. Social & Web brand icon check
        var googleIcon = favicon.ResolveIcon("https://accounts.google.com/login", "Google Work");
        Assert.Contains("google", googleIcon);

        var githubIcon = favicon.ResolveIcon("https://github.com/login", "GitHub");
        Assert.Contains("github", githubIcon);

        // 2. Card Brand detection
        var visaBrand = favicon.DetectCardBrand("4111 2222 3333 4444");
        Assert.Equal("Visa", visaBrand);

        var mcBrand = favicon.DetectCardBrand("5211 0000 1111 2222");
        Assert.Equal("MasterCard", mcBrand);

        var rupayBrand = favicon.DetectCardBrand("6071 0000 1111 2222");
        Assert.Equal("RuPay", rupayBrand);

        var amexBrand = favicon.DetectCardBrand("3782 8224 6310 005");
        Assert.Equal("Amex", amexBrand);
    }

    [Fact]
    public async Task RealDatabase_VaultCrud_And_AutoFill_Success()
    {
        var crypto = new AesGcmCryptoEngine(_config);
        var passGen = new PasswordGeneratorService();
        var favicon = new FaviconService();
        var repo = new VaultRepository(_config);
        var vaultService = new VaultService(repo, crypto, passGen, favicon);

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

    [Fact]
    public async Task RealDatabase_Card_Document_ApiKey_And_Trash_Success()
    {
        var crypto = new AesGcmCryptoEngine(_config);
        var passGen = new PasswordGeneratorService();
        var favicon = new FaviconService();
        var repo = new VaultRepository(_config);
        var vaultService = new VaultService(repo, crypto, passGen, favicon);

        string userId;
        await using (var con = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await con.OpenAsync();
            await using var cmd = new SqlCommand("SELECT TOP 1 Id FROM Users_CYH WHERE NormalizedEmail = 'USER@CYHLOCKER.COM'", con);
            userId = (await cmd.ExecuteScalarAsync())?.ToString() ?? throw new Exception("User not found");
        }

        // 1. Create Payment Card Item
        var cardId = await vaultService.CreateVaultItemAsync(userId, new CreateVaultItemRequestDto
        {
            Title = "HDFC Regalia Visa Test",
            ItemType = "Card",
            CardholderName = "AMIT KUMAR",
            CardNumber = "4111 2222 3333 4021",
            CardExpiryMonth = "08",
            CardExpiryYear = "29",
            CardCvv = "789",
            CardPin = "9876",
            Notes = "Billing address: Mumbai"
        });
        Assert.NotNull(cardId);

        // Verify Card Decryption
        var cardItem = await vaultService.GetVaultItemByIdAsync(userId, cardId);
        Assert.NotNull(cardItem);
        Assert.Equal("AMIT KUMAR", cardItem.CardholderName);
        Assert.Equal("4111 2222 3333 4021", cardItem.CardNumber);
        Assert.Equal("Visa", cardItem.CardBrand);
        Assert.Equal("4021", cardItem.CardLast4);
        Assert.Equal("789", cardItem.CardCvv);
        Assert.Equal("9876", cardItem.CardPin);

        // 2. Create Secure Document Item
        var docId = await vaultService.CreateVaultItemAsync(userId, new CreateVaultItemRequestDto
        {
            Title = "House Lease Agreement",
            ItemType = "Document",
            DocFileName = "lease_agreement.pdf",
            DocFileSize = "2.4 MB (Encrypted)",
            DocContent = "# Confidential Lease Agreement\nLandlord: John Doe\nTenant: Amit Kumar\nRent: 25000 INR"
        });
        Assert.NotNull(docId);

        var docItem = await vaultService.GetVaultItemByIdAsync(userId, docId);
        Assert.NotNull(docItem);
        Assert.Equal("lease_agreement.pdf", docItem.DocFileName);
        Assert.Contains("Confidential Lease Agreement", docItem.DocContent);

        // 3. Create API Key Item
        var apiKeyId = await vaultService.CreateVaultItemAsync(userId, new CreateVaultItemRequestDto
        {
            Title = "OpenAI Production Secret",
            ItemType = "ApiKey",
            ApiKey = "sk-proj-xyz1234567890",
            ApiSecret = "secret-token-abcdef123456",
            ApiEnvironment = "Production",
            Url = "https://api.openai.com/v1"
        });
        Assert.NotNull(apiKeyId);

        var apiItem = await vaultService.GetVaultItemByIdAsync(userId, apiKeyId);
        Assert.NotNull(apiItem);
        Assert.Equal("sk-proj-xyz1234567890", apiItem.ApiKey);
        Assert.Equal("secret-token-abcdef123456", apiItem.ApiSecret);
        Assert.Equal("Production", apiItem.ApiEnvironment);

        // 4. Test Sidebar Counts
        var counts = await vaultService.GetVaultCountsAsync(userId);
        Assert.True(counts.TotalAll >= 3);
        Assert.True(counts.TotalCards >= 1);
        Assert.True(counts.TotalDocuments >= 1);
        Assert.True(counts.TotalApiKeys >= 1);

        // 5. Test Soft Delete (Move to Trash)
        await vaultService.DeleteVaultItemAsync(userId, cardId);
        var trashItems = await vaultService.GetTrashItemsAsync(userId);
        Assert.Contains(trashItems, t => t.Id == cardId);

        // 6. Test Restore from Trash
        await vaultService.RestoreFromTrashAsync(userId, cardId);
        var restoredItem = await vaultService.GetVaultItemByIdAsync(userId, cardId);
        Assert.NotNull(restoredItem);

        // 7. Cleanup
        await vaultService.PermanentlyDeleteItemAsync(userId, cardId);
        await vaultService.PermanentlyDeleteItemAsync(userId, docId);
        await vaultService.PermanentlyDeleteItemAsync(userId, apiKeyId);
    }
}
