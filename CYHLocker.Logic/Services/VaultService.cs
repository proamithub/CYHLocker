using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly IFaviconService _favicon;
    private readonly ISubscriptionRepository? _subRepo;
    private readonly ILogger<VaultService>? _logger;

    public VaultService(
        IVaultRepository vaultRepo, 
        ICryptoEngine crypto, 
        IPasswordGeneratorService passGen,
        IFaviconService favicon,
        ISubscriptionRepository? subRepo = null,
        ILogger<VaultService>? logger = null)
    {
        _vaultRepo = vaultRepo;
        _crypto    = crypto;
        _passGen   = passGen;
        _favicon   = favicon;
        _subRepo   = subRepo;
        _logger    = logger;
    }

    public async Task<List<VaultItemDto>> GetVaultItemsAsync(
        string userId, 
        string? categoryId = null, 
        string? searchQuery = null, 
        string? itemType = null, 
        bool onlyFavorites = false)
    {
        var records = await _vaultRepo.GetAllItemsAsync(userId, categoryId, searchQuery, itemType, onlyFavorites);
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
        // Check plan quotas if subscription repository is configured
        if (_subRepo != null)
        {
            var quota = await _subRepo.CheckQuotaAsync(userId, request.ItemType ?? "Login");
            if (!quota.IsAllowed)
            {
                throw new InvalidOperationException(quota.Message ?? $"Plan quota exceeded for {request.ItemType}s. Please upgrade to Pro.");
            }
        }

        var record = PrepareDbRecord(Guid.NewGuid().ToString(), userId, request);
        return await _vaultRepo.InsertItemAsync(record);
    }

    public async Task<bool> UpdateVaultItemAsync(string userId, UpdateVaultItemRequestDto request)
    {
        var record = PrepareDbRecord(request.Id, userId, request);
        return await _vaultRepo.UpdateItemAsync(record);
    }

    public Task<bool> DeleteVaultItemAsync(string userId, string itemId) =>
        _vaultRepo.DeleteItemAsync(userId, itemId);

    public Task<bool> ToggleFavoriteAsync(string userId, string itemId) =>
        _vaultRepo.ToggleFavoriteAsync(userId, itemId);

    public async Task<List<AutoFillMatchDto>> GetAutoFillMatchesAsync(string userId, string urlOrDomain)
    {
        var domain = _favicon.ExtractDomain(urlOrDomain) ?? urlOrDomain.Trim().ToLowerInvariant();
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

    // ── TRASH MANAGEMENT ───────────────────────────────────────────────────────
    public async Task<List<VaultItemDto>> GetTrashItemsAsync(string userId)
    {
        var records = await _vaultRepo.GetTrashItemsAsync(userId);
        var result = new List<VaultItemDto>(records.Count);
        foreach (var r in records)
        {
            result.Add(DecryptItem(r));
        }
        return result;
    }

    public Task<bool> RestoreFromTrashAsync(string userId, string itemId) =>
        _vaultRepo.RestoreFromTrashAsync(userId, itemId);

    public Task<bool> PermanentlyDeleteItemAsync(string userId, string itemId) =>
        _vaultRepo.PermanentlyDeleteItemAsync(userId, itemId);

    public Task<bool> EmptyTrashAsync(string userId) =>
        _vaultRepo.EmptyTrashAsync(userId);

    // ── SIDEBAR COUNTS ────────────────────────────────────────────────────────
    public Task<VaultSidebarCountsDto> GetVaultCountsAsync(string userId) =>
        _vaultRepo.GetCountsAsync(userId);

    // ── CATEGORIES ─────────────────────────────────────────────────────────────
    public Task<List<VaultCategoryDto>> GetCategoriesAsync(string userId) =>
        _vaultRepo.GetCategoriesAsync(userId);

    public Task<string> CreateCategoryAsync(string userId, CreateCategoryRequestDto request) =>
        _vaultRepo.InsertCategoryAsync(userId, request.Name, request.Icon, request.Color);

    public Task<bool> UpdateCategoryAsync(string userId, string categoryId, string name, string? icon, string? color) =>
        _vaultRepo.UpdateCategoryAsync(userId, categoryId, name, icon, color);

    public Task<bool> DeleteCategoryAsync(string userId, string categoryId) =>
        _vaultRepo.DeleteCategoryAsync(userId, categoryId);

    // ── PRIVATE HELPERS ────────────────────────────────────────────────────────
    private VaultItemDbRecord PrepareDbRecord(string id, string userId, CreateVaultItemRequestDto request)
    {
        var type = string.IsNullOrWhiteSpace(request.ItemType) ? "Login" : request.ItemType.Trim();
        var domain = _favicon.ExtractDomain(request.Url);
        var strength = string.IsNullOrEmpty(request.Password) ? "Strong" : _passGen.EvaluateStrength(request.Password);

        string? encryptedUser = null;
        string? encryptedPass = null;
        string? encryptedUrl = null;
        string? encryptedNotes = null;
        string? encryptedCustom = null;
        string? cardBrand = null;
        string? cardLast4 = null;
        string? secondary = null;
        string? icon = request.ItemIcon;

        if (string.Equals(type, "Card", StringComparison.OrdinalIgnoreCase))
        {
            // Payment Card Handling
            var cleanNumber = Regex.Replace(request.CardNumber ?? "", @"\D", "");
            cardLast4 = cleanNumber.Length >= 4 ? cleanNumber[^4..] : cleanNumber;
            cardBrand = string.IsNullOrEmpty(request.CardBrand) 
                ? _favicon.DetectCardBrand(cleanNumber) 
                : request.CardBrand.Trim();

            secondary = $"{cardBrand} •••• {cardLast4}".Trim();

            // Encrypt card sensitive payload
            encryptedUser  = string.IsNullOrEmpty(request.CardholderName) ? null : _crypto.Encrypt(request.CardholderName);
            encryptedPass  = string.IsNullOrEmpty(request.CardCvv) ? null : _crypto.Encrypt(request.CardCvv);
            encryptedNotes = string.IsNullOrEmpty(request.Notes) ? null : _crypto.Encrypt(request.Notes);

            var cardMeta = new Dictionary<string, string?>
            {
                { "CardNumber", request.CardNumber },
                { "ExpiryMonth", request.CardExpiryMonth },
                { "ExpiryYear", request.CardExpiryYear },
                { "CardPin", request.CardPin },
                { "CardBrand", cardBrand },
                { "CardLast4", cardLast4 }
            };
            encryptedCustom = _crypto.Encrypt(JsonSerializer.Serialize(cardMeta));

            if (string.IsNullOrEmpty(icon))
            {
                icon = _favicon.ResolveIcon(null, request.Title, "Card", cardBrand);
            }
        }
        else if (string.Equals(type, "Document", StringComparison.OrdinalIgnoreCase))
        {
            // Encrypted Document Handling
            secondary = string.IsNullOrEmpty(request.DocFileSize) ? "Encrypted Doc" : request.DocFileSize;

            encryptedNotes = string.IsNullOrEmpty(request.DocContent) ? null : _crypto.Encrypt(request.DocContent);

            var docMeta = new Dictionary<string, string?>
            {
                { "DocFileName", request.DocFileName },
                { "DocFileSize", request.DocFileSize },
                { "DocMimeType", request.DocMimeType }
            };
            encryptedCustom = _crypto.Encrypt(JsonSerializer.Serialize(docMeta));

            if (string.IsNullOrEmpty(icon))
            {
                icon = _favicon.ResolveIcon(null, request.Title, "Document");
            }
        }
        else if (string.Equals(type, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            // Developer API Key Handling
            secondary = string.IsNullOrEmpty(request.ApiEnvironment) ? "API Key" : request.ApiEnvironment;

            encryptedUser  = string.IsNullOrEmpty(request.ApiKey) ? null : _crypto.Encrypt(request.ApiKey);
            encryptedPass  = string.IsNullOrEmpty(request.ApiSecret) ? null : _crypto.Encrypt(request.ApiSecret);
            encryptedUrl   = string.IsNullOrEmpty(request.Url) ? null : _crypto.Encrypt(request.Url);
            encryptedNotes = string.IsNullOrEmpty(request.Notes) ? null : _crypto.Encrypt(request.Notes);

            var apiMeta = new Dictionary<string, string?>
            {
                { "ApiEnvironment", request.ApiEnvironment }
            };
            encryptedCustom = _crypto.Encrypt(JsonSerializer.Serialize(apiMeta));

            if (string.IsNullOrEmpty(icon))
            {
                icon = _favicon.ResolveIcon(null, request.Title, "ApiKey");
            }
        }
        else
        {
            // Login / Social Media Handling
            secondary = request.Username;

            encryptedUser   = string.IsNullOrEmpty(request.Username) ? null : _crypto.Encrypt(request.Username);
            encryptedPass   = string.IsNullOrEmpty(request.Password) ? null : _crypto.Encrypt(request.Password);
            encryptedUrl    = string.IsNullOrEmpty(request.Url)      ? null : _crypto.Encrypt(request.Url);
            encryptedNotes  = string.IsNullOrEmpty(request.Notes)    ? null : _crypto.Encrypt(request.Notes);
            encryptedCustom = string.IsNullOrEmpty(request.CustomFieldsJson) ? null : _crypto.Encrypt(request.CustomFieldsJson);

            if (string.IsNullOrEmpty(icon))
            {
                icon = _favicon.ResolveIcon(request.Url, request.Title, type);
            }
        }

        return new VaultItemDbRecord
        {
            Id                    = id,
            UserId                = userId,
            CategoryId            = request.CategoryId,
            ItemType              = type,
            Title                 = request.Title.Trim(),
            EncryptedUsername     = encryptedUser,
            EncryptedPassword     = encryptedPass,
            EncryptedUrl          = encryptedUrl,
            EncryptedNotes        = encryptedNotes,
            EncryptedCustomFields = encryptedCustom,
            EncryptionIV          = "AES-256-GCM",
            UrlDomain             = domain,
            ItemIcon              = icon,
            IsFavorite            = request.IsFavorite,
            PasswordStrength      = strength,
            CardBrand             = cardBrand,
            CardLast4             = cardLast4,
            SecondaryInfo         = secondary
        };
    }

    private VaultItemDto DecryptItem(VaultItemDbRecord r)
    {
        string? username = null;
        string? password = null;
        string? url = null;
        string? notes = null;
        string? customJson = null;

        try { if (!string.IsNullOrEmpty(r.EncryptedUsername)) username = _crypto.Decrypt(r.EncryptedUsername); } catch { username = "[Decryption Failed]"; }
        try { if (!string.IsNullOrEmpty(r.EncryptedPassword)) password = _crypto.Decrypt(r.EncryptedPassword); } catch { password = "[Decryption Failed]"; }
        try { if (!string.IsNullOrEmpty(r.EncryptedUrl))      url      = _crypto.Decrypt(r.EncryptedUrl); } catch { url = "[Decryption Failed]"; }
        try { if (!string.IsNullOrEmpty(r.EncryptedNotes))    notes    = _crypto.Decrypt(r.EncryptedNotes); } catch { notes = null; }
        try { if (!string.IsNullOrEmpty(r.EncryptedCustomFields)) customJson = _crypto.Decrypt(r.EncryptedCustomFields); } catch { customJson = null; }

        var item = new VaultItemDto
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
            CustomFieldsJson = customJson,
            UrlDomain        = r.UrlDomain,
            ItemIcon         = r.ItemIcon,
            IsFavorite       = r.IsFavorite,
            PasswordStrength = r.PasswordStrength,
            CardBrand        = r.CardBrand,
            CardLast4        = r.CardLast4,
            SecondaryInfo    = r.SecondaryInfo,
            CreatedAt        = r.CreatedAt,
            UpdatedAt        = r.UpdatedAt,
            LastUsedAt       = r.LastUsedAt
        };

        // Unpack Card metadata
        if (string.Equals(r.ItemType, "Card", StringComparison.OrdinalIgnoreCase))
        {
            item.CardholderName = username;
            item.CardCvv = password;
            if (!string.IsNullOrEmpty(customJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(customJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("CardNumber", out var cn)) item.CardNumber = cn.GetString();
                    if (root.TryGetProperty("ExpiryMonth", out var em)) item.CardExpiryMonth = em.GetString();
                    if (root.TryGetProperty("ExpiryYear", out var ey)) item.CardExpiryYear = ey.GetString();
                    if (root.TryGetProperty("CardPin", out var cp)) item.CardPin = cp.GetString();
                    if (root.TryGetProperty("CardBrand", out var cb) && string.IsNullOrEmpty(item.CardBrand)) item.CardBrand = cb.GetString();
                }
                catch { /* Ignore corrupt meta */ }
            }
        }
        // Unpack Document metadata
        else if (string.Equals(r.ItemType, "Document", StringComparison.OrdinalIgnoreCase))
        {
            item.DocContent = notes;
            if (!string.IsNullOrEmpty(customJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(customJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("DocFileName", out var fn)) item.DocFileName = fn.GetString();
                    if (root.TryGetProperty("DocFileSize", out var fs)) item.DocFileSize = fs.GetString();
                    if (root.TryGetProperty("DocMimeType", out var mt)) item.DocMimeType = mt.GetString();
                }
                catch { /* Ignore corrupt meta */ }
            }
        }
        // Unpack ApiKey metadata
        else if (string.Equals(r.ItemType, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            item.ApiKey = username;
            item.ApiSecret = password;
            if (!string.IsNullOrEmpty(customJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(customJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("ApiEnvironment", out var env)) item.ApiEnvironment = env.GetString();
                }
                catch { /* Ignore corrupt meta */ }
            }
        }

        return item;
    }
}
