using CYHLocker.Data.DTOs;
using CYHLocker.Logic.Encryption;
using CYHLocker.Logic.Interfaces;
using CYHLocker.Repository.Interfaces;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Logic.Services;

public class VaultService : IVaultService
{
    private readonly IVaultRepository _vaultRepo;
    private readonly ICryptoEngine _crypto;
    private readonly IPasswordGeneratorService _passGen;
    private readonly ILogger<VaultService>? _logger;

    public VaultService(
        IVaultRepository vaultRepo, 
        ICryptoEngine crypto, 
        IPasswordGeneratorService passGen,
        ILogger<VaultService>? logger = null)
    {
        _vaultRepo = vaultRepo;
        _crypto = crypto;
        _passGen = passGen;
        _logger = logger;
    }

    public async Task<List<VaultItemDto>> GetVaultItemsAsync(string userId, string? categoryId = null, string? searchQuery = null)
    {
        var records = await _vaultRepo.GetAllItemsAsync(userId, categoryId, searchQuery);
        var result = new List<VaultItemDto>(records.Count);

        foreach (var r in records)
        {
            result.Add(DecryptItem(r));
        }

        return result;
    }

    public async Task<VaultItemDto?> GetVaultItemByIdAsync(string userId, string itemId)
    {
        var record = await _vaultRepo.GetItemByIdAsync(userId, itemId);
        return record == null ? null : DecryptItem(record);
    }

    public async Task<string> CreateVaultItemAsync(string userId, CreateVaultItemRequestDto request)
    {
        var domain = ExtractDomain(request.Url);
        var strength = _passGen.EvaluateStrength(request.Password);

        // Encrypt sensitive fields with AES-256-GCM
        var encryptedUser   = string.IsNullOrEmpty(request.Username) ? null : _crypto.Encrypt(request.Username);
        var encryptedPass   = string.IsNullOrEmpty(request.Password) ? null : _crypto.Encrypt(request.Password);
        var encryptedUrl    = string.IsNullOrEmpty(request.Url)      ? null : _crypto.Encrypt(request.Url);
        var encryptedNotes  = string.IsNullOrEmpty(request.Notes)    ? null : _crypto.Encrypt(request.Notes);
        var encryptedCustom = string.IsNullOrEmpty(request.CustomFieldsJson) ? null : _crypto.Encrypt(request.CustomFieldsJson);

        var record = new VaultItemDbRecord
        {
            Id                    = Guid.NewGuid().ToString(),
            UserId                = userId,
            CategoryId            = request.CategoryId,
            ItemType              = request.ItemType ?? "Login",
            Title                 = request.Title.Trim(),
            EncryptedUsername     = encryptedUser,
            EncryptedPassword     = encryptedPass,
            EncryptedUrl          = encryptedUrl,
            EncryptedNotes        = encryptedNotes,
            EncryptedCustomFields = encryptedCustom,
            EncryptionIV          = "AES-256-GCM",
            UrlDomain             = domain,
            ItemIcon              = string.IsNullOrEmpty(request.ItemIcon) ? GetDefaultIcon(request.ItemType) : request.ItemIcon,
            IsFavorite            = request.IsFavorite,
            PasswordStrength      = strength
        };

        return await _vaultRepo.InsertItemAsync(record);
    }

    public async Task<bool> UpdateVaultItemAsync(string userId, UpdateVaultItemRequestDto request)
    {
        var domain = ExtractDomain(request.Url);
        var strength = _passGen.EvaluateStrength(request.Password);

        var encryptedUser   = string.IsNullOrEmpty(request.Username) ? null : _crypto.Encrypt(request.Username);
        var encryptedPass   = string.IsNullOrEmpty(request.Password) ? null : _crypto.Encrypt(request.Password);
        var encryptedUrl    = string.IsNullOrEmpty(request.Url)      ? null : _crypto.Encrypt(request.Url);
        var encryptedNotes  = string.IsNullOrEmpty(request.Notes)    ? null : _crypto.Encrypt(request.Notes);
        var encryptedCustom = string.IsNullOrEmpty(request.CustomFieldsJson) ? null : _crypto.Encrypt(request.CustomFieldsJson);

        var record = new VaultItemDbRecord
        {
            Id                    = request.Id,
            UserId                = userId,
            CategoryId            = request.CategoryId,
            ItemType              = request.ItemType ?? "Login",
            Title                 = request.Title.Trim(),
            EncryptedUsername     = encryptedUser,
            EncryptedPassword     = encryptedPass,
            EncryptedUrl          = encryptedUrl,
            EncryptedNotes        = encryptedNotes,
            EncryptedCustomFields = encryptedCustom,
            EncryptionIV          = "AES-256-GCM",
            UrlDomain             = domain,
            ItemIcon              = string.IsNullOrEmpty(request.ItemIcon) ? GetDefaultIcon(request.ItemType) : request.ItemIcon,
            IsFavorite            = request.IsFavorite,
            PasswordStrength      = strength
        };

        return await _vaultRepo.UpdateItemAsync(record);
    }

    public Task<bool> DeleteVaultItemAsync(string userId, string itemId) =>
        _vaultRepo.DeleteItemAsync(userId, itemId);

    public Task<bool> ToggleFavoriteAsync(string userId, string itemId) =>
        _vaultRepo.ToggleFavoriteAsync(userId, itemId);

    public async Task<List<AutoFillMatchDto>> GetAutoFillMatchesAsync(string userId, string urlOrDomain)
    {
        var domain = ExtractDomain(urlOrDomain) ?? urlOrDomain.Trim().ToLowerInvariant();
        var records = await _vaultRepo.GetAutoFillMatchesAsync(userId, domain);
        var result = new List<AutoFillMatchDto>(records.Count);

        foreach (var r in records)
        {
            result.Add(new AutoFillMatchDto
            {
                Id        = r.Id,
                Title     = r.Title,
                Username  = string.IsNullOrEmpty(r.EncryptedUsername) ? null : _crypto.Decrypt(r.EncryptedUsername),
                Password  = string.IsNullOrEmpty(r.EncryptedPassword) ? null : _crypto.Decrypt(r.EncryptedPassword),
                Url       = string.IsNullOrEmpty(r.EncryptedUrl) ? null : _crypto.Decrypt(r.EncryptedUrl),
                UrlDomain = r.UrlDomain,
                ItemIcon  = r.ItemIcon
            });
        }

        return result;
    }

    public Task<List<VaultCategoryDto>> GetCategoriesAsync(string userId) =>
        _vaultRepo.GetCategoriesAsync(userId);

    public Task<string> CreateCategoryAsync(string userId, CreateCategoryRequestDto request) =>
        _vaultRepo.InsertCategoryAsync(userId, request.Name, request.Icon, request.Color);

    public Task<bool> DeleteCategoryAsync(string userId, string categoryId) =>
        _vaultRepo.DeleteCategoryAsync(userId, categoryId);

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private VaultItemDto DecryptItem(VaultItemDbRecord r)
    {
        string? username = null;
        string? password = null;
        string? url = null;
        string? notes = null;
        string? custom = null;

        try { if (!string.IsNullOrEmpty(r.EncryptedUsername)) username = _crypto.Decrypt(r.EncryptedUsername); } catch { username = "[Decryption Failed]"; }
        try { if (!string.IsNullOrEmpty(r.EncryptedPassword)) password = _crypto.Decrypt(r.EncryptedPassword); } catch { password = "[Decryption Failed]"; }
        try { if (!string.IsNullOrEmpty(r.EncryptedUrl))      url      = _crypto.Decrypt(r.EncryptedUrl); } catch { url = "[Decryption Failed]"; }
        try { if (!string.IsNullOrEmpty(r.EncryptedNotes))    notes    = _crypto.Decrypt(r.EncryptedNotes); } catch { notes = null; }
        try { if (!string.IsNullOrEmpty(r.EncryptedCustomFields)) custom = _crypto.Decrypt(r.EncryptedCustomFields); } catch { custom = null; }

        return new VaultItemDto
        {
            Id               = r.Id,
            UserId           = r.UserId,
            CategoryId       = r.CategoryId,
            CategoryName     = r.CategoryName,
            CategoryColor    = r.CategoryColor,
            ItemType         = r.ItemType,
            Title            = r.Title,
            Username         = username,
            Password         = password,
            Url              = url,
            Notes            = notes,
            CustomFieldsJson = custom,
            UrlDomain        = r.UrlDomain,
            ItemIcon         = r.ItemIcon,
            IsFavorite       = r.IsFavorite,
            PasswordStrength = r.PasswordStrength,
            CreatedAt        = r.CreatedAt,
            UpdatedAt        = r.UpdatedAt,
            LastUsedAt       = r.LastUsedAt
        };
    }

    private static string? ExtractDomain(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return null;
        try
        {
            var url = rawUrl.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            var uri = new Uri(url);
            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host[4..];
            return host;
        }
        catch
        {
            return rawUrl.Trim().ToLowerInvariant();
        }
    }

    private static string GetDefaultIcon(string? itemType) => itemType switch
    {
        "Card"     => "💳",
        "Identity" => "🪪",
        "ApiKey"   => "🔑",
        "Note"     => "📝",
        _          => "🌐"
    };
}
