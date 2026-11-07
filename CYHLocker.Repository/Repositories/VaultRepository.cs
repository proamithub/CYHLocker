using System.Data;
using CYHLocker.Data.DTOs;
using CYHLocker.Repository.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CYHLocker.Repository.Repositories;

public class VaultRepository : IVaultRepository
{
    private readonly string _conn;
    private readonly ILogger<VaultRepository>? _logger;

    public VaultRepository(IConfiguration config, ILogger<VaultRepository>? logger = null)
    {
        _conn = config.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("'DefaultConnection' not found.");
        _logger = logger;
    }

    public async Task<List<VaultItemDbRecord>> GetAllItemsAsync(
        string userId, 
        string? categoryId = null, 
        string? searchQuery = null, 
        string? itemType = null, 
        bool onlyFavorites = false)
    {
        var items = new List<VaultItemDbRecord>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageVault_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_ALL");
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SearchQuery", (object?)searchQuery ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ItemType", (object?)itemType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OnlyFavorites", onlyFavorites);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                items.Add(MapRecord(r));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetAllItemsAsync failed for user {UserId}", userId);
        }
        return items;
    }

    public async Task<VaultItemDbRecord?> GetItemByIdAsync(string userId, string itemId)
    {
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageVault_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_BY_ID");
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ItemId", itemId);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return MapRecord(r);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetItemByIdAsync failed for item {ItemId}", itemId);
        }
        return null;
    }

    public async Task<string> InsertItemAsync(VaultItemDbRecord record)
    {
        var id = string.IsNullOrEmpty(record.Id) ? Guid.NewGuid().ToString() : record.Id;
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "INSERT");
        cmd.Parameters.AddWithValue("@UserId", record.UserId);
        cmd.Parameters.AddWithValue("@ItemId", id);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)record.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ItemType", record.ItemType);
        cmd.Parameters.AddWithValue("@Title", record.Title);
        cmd.Parameters.AddWithValue("@EncryptedUsername", (object?)record.EncryptedUsername ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedPassword", (object?)record.EncryptedPassword ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedUrl", (object?)record.EncryptedUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedNotes", (object?)record.EncryptedNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedCustomFields", (object?)record.EncryptedCustomFields ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptionIV", record.EncryptionIV);
        cmd.Parameters.AddWithValue("@UrlDomain", (object?)record.UrlDomain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ItemIcon", (object?)record.ItemIcon ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsFavorite", record.IsFavorite);
        cmd.Parameters.AddWithValue("@PasswordStrength", record.PasswordStrength);
        cmd.Parameters.AddWithValue("@CardBrand", (object?)record.CardBrand ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CardLast4", (object?)record.CardLast4 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SecondaryInfo", (object?)record.SecondaryInfo ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? id;
    }

    public async Task<bool> UpdateItemAsync(VaultItemDbRecord record)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "UPDATE");
        cmd.Parameters.AddWithValue("@UserId", record.UserId);
        cmd.Parameters.AddWithValue("@ItemId", record.Id);
        cmd.Parameters.AddWithValue("@CategoryId", (object?)record.CategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ItemType", record.ItemType);
        cmd.Parameters.AddWithValue("@Title", record.Title);
        cmd.Parameters.AddWithValue("@EncryptedUsername", (object?)record.EncryptedUsername ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedPassword", (object?)record.EncryptedPassword ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedUrl", (object?)record.EncryptedUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedNotes", (object?)record.EncryptedNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptedCustomFields", (object?)record.EncryptedCustomFields ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EncryptionIV", record.EncryptionIV);
        cmd.Parameters.AddWithValue("@UrlDomain", (object?)record.UrlDomain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ItemIcon", (object?)record.ItemIcon ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsFavorite", record.IsFavorite);
        cmd.Parameters.AddWithValue("@PasswordStrength", record.PasswordStrength);
        cmd.Parameters.AddWithValue("@CardBrand", (object?)record.CardBrand ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CardLast4", (object?)record.CardLast4 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SecondaryInfo", (object?)record.SecondaryInfo ?? DBNull.Value);

        var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        return rows > 0;
    }

    public async Task<bool> DeleteItemAsync(string userId, string itemId)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "DELETE");
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);

        var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        return rows > 0;
    }

    public async Task<bool> ToggleFavoriteAsync(string userId, string itemId)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "TOGGLE_FAVORITE");
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToBoolean(result);
    }

    public async Task<List<VaultItemDbRecord>> GetAutoFillMatchesAsync(string userId, string urlDomain)
    {
        var items = new List<VaultItemDbRecord>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageVault_CYH");
            cmd.Parameters.AddWithValue("@Flag", "AUTOFILL_MATCH");
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@UrlDomain", urlDomain);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                items.Add(MapRecord(r));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetAutoFillMatchesAsync failed for domain {Domain}", urlDomain);
        }
        return items;
    }

    // ── TRASH MANAGEMENT ───────────────────────────────────────────────────────
    public async Task<List<VaultItemDbRecord>> GetTrashItemsAsync(string userId)
    {
        var items = new List<VaultItemDbRecord>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageVault_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_TRASH");
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                items.Add(MapRecord(r));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetTrashItemsAsync failed");
        }
        return items;
    }

    public async Task<bool> RestoreFromTrashAsync(string userId, string itemId)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "RESTORE");
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);

        var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        return rows > 0;
    }

    public async Task<bool> PermanentlyDeleteItemAsync(string userId, string itemId)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "PERMANENT_DELETE");
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@ItemId", itemId);

        var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        return rows > 0;
    }

    public async Task<bool> EmptyTrashAsync(string userId)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "EMPTY_TRASH");
        cmd.Parameters.AddWithValue("@UserId", userId);

        var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        return rows >= 0;
    }

    // ── SIDEBAR COUNTS ────────────────────────────────────────────────────────
    public async Task<VaultSidebarCountsDto> GetCountsAsync(string userId)
    {
        var counts = new VaultSidebarCountsDto();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageVault_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_COUNTS");
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                counts.TotalAll       = Convert.ToInt32(r["TotalAll"]);
                counts.TotalLogins    = Convert.ToInt32(r["TotalLogins"]);
                counts.TotalSocial    = Convert.ToInt32(r["TotalSocial"]);
                counts.TotalCards     = Convert.ToInt32(r["TotalCards"]);
                counts.TotalDocuments = Convert.ToInt32(r["TotalDocuments"]);
                counts.TotalApiKeys   = Convert.ToInt32(r["TotalApiKeys"]);
                counts.TotalFavorites = Convert.ToInt32(r["TotalFavorites"]);
                counts.TotalTrash     = Convert.ToInt32(r["TotalTrash"]);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetCountsAsync failed");
        }
        return counts;
    }

    // ── CATEGORIES ─────────────────────────────────────────────────────────────
    public async Task<List<VaultCategoryDto>> GetCategoriesAsync(string userId)
    {
        var categories = new List<VaultCategoryDto>();
        try
        {
            await using var con = new SqlConnection(_conn);
            await con.OpenAsync();
            await using var cmd = Sp(con, "ManageVault_CYH");
            cmd.Parameters.AddWithValue("@Flag", "GET_CATEGORIES");
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                categories.Add(new VaultCategoryDto
                {
                    Id = r["Id"].ToString()!,
                    UserId = r["UserId"].ToString()!,
                    Name = r["Name"].ToString()!,
                    Icon = r["Icon"] != DBNull.Value ? r["Icon"].ToString() : "📁",
                    Color = r["Color"] != DBNull.Value ? r["Color"].ToString() : "#06b6d4",
                    SortOrder = Convert.ToInt32(r["SortOrder"]),
                    TotalItems = Convert.ToInt32(r["TotalItems"]),
                    CreatedAt = Convert.ToDateTime(r["CreatedAt"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] GetCategoriesAsync failed");
        }
        return categories;
    }

    public async Task<string> InsertCategoryAsync(string userId, string name, string? icon, string? color)
    {
        var id = Guid.NewGuid().ToString();
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "INSERT_CATEGORY");
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@CategoryId", id);
        cmd.Parameters.AddWithValue("@CategoryName", name.Trim());
        cmd.Parameters.AddWithValue("@CategoryIcon", (object?)icon ?? "📁");
        cmd.Parameters.AddWithValue("@CategoryColor", (object?)color ?? "#06b6d4");

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? id;
    }

    public async Task<bool> DeleteCategoryAsync(string userId, string categoryId)
    {
        await using var con = new SqlConnection(_conn);
        await con.OpenAsync();
        await using var cmd = Sp(con, "ManageVault_CYH");
        cmd.Parameters.AddWithValue("@Flag", "DELETE_CATEGORY");
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@CategoryId", categoryId);

        var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        return rows > 0;
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };

    private static VaultItemDbRecord MapRecord(SqlDataReader r) => new()
    {
        Id                    = r["Id"].ToString()!,
        UserId                = r["UserId"].ToString()!,
        CategoryId            = HasColumn(r, "CategoryId") && r["CategoryId"] != DBNull.Value ? r["CategoryId"].ToString() : null,
        CategoryName          = HasColumn(r, "CategoryName") && r["CategoryName"] != DBNull.Value ? r["CategoryName"].ToString() : null,
        CategoryColor         = HasColumn(r, "CategoryColor") && r["CategoryColor"] != DBNull.Value ? r["CategoryColor"].ToString() : null,
        ItemType              = HasColumn(r, "ItemType") && r["ItemType"] != DBNull.Value ? r["ItemType"].ToString()! : "Login",
        Title                 = r["Title"].ToString()!,
        EncryptedUsername     = HasColumn(r, "EncryptedUsername") && r["EncryptedUsername"] != DBNull.Value ? r["EncryptedUsername"].ToString() : null,
        EncryptedPassword     = HasColumn(r, "EncryptedPassword") && r["EncryptedPassword"] != DBNull.Value ? r["EncryptedPassword"].ToString() : null,
        EncryptedUrl          = HasColumn(r, "EncryptedUrl") && r["EncryptedUrl"] != DBNull.Value ? r["EncryptedUrl"].ToString() : null,
        EncryptedNotes        = HasColumn(r, "EncryptedNotes") && r["EncryptedNotes"] != DBNull.Value ? r["EncryptedNotes"].ToString() : null,
        EncryptedCustomFields = HasColumn(r, "EncryptedCustomFields") && r["EncryptedCustomFields"] != DBNull.Value ? r["EncryptedCustomFields"].ToString() : null,
        EncryptionIV          = HasColumn(r, "EncryptionIV") ? r["EncryptionIV"].ToString()! : string.Empty,
        UrlDomain             = HasColumn(r, "UrlDomain") && r["UrlDomain"] != DBNull.Value ? r["UrlDomain"].ToString() : null,
        ItemIcon              = HasColumn(r, "ItemIcon") && r["ItemIcon"] != DBNull.Value ? r["ItemIcon"].ToString() : null,
        IsFavorite            = HasColumn(r, "IsFavorite") && r["IsFavorite"] != DBNull.Value && Convert.ToBoolean(r["IsFavorite"]),
        PasswordStrength      = HasColumn(r, "PasswordStrength") && r["PasswordStrength"] != DBNull.Value ? r["PasswordStrength"].ToString()! : "Strong",
        CardBrand             = HasColumn(r, "CardBrand") && r["CardBrand"] != DBNull.Value ? r["CardBrand"].ToString() : null,
        CardLast4             = HasColumn(r, "CardLast4") && r["CardLast4"] != DBNull.Value ? r["CardLast4"].ToString() : null,
        SecondaryInfo         = HasColumn(r, "SecondaryInfo") && r["SecondaryInfo"] != DBNull.Value ? r["SecondaryInfo"].ToString() : null,
        CreatedAt             = HasColumn(r, "CreatedAt") && r["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(r["CreatedAt"]) : DateTime.UtcNow,
        UpdatedAt             = HasColumn(r, "UpdatedAt") && r["UpdatedAt"] != DBNull.Value ? Convert.ToDateTime(r["UpdatedAt"]) : DateTime.UtcNow,
        LastUsedAt            = HasColumn(r, "LastUsedAt") && r["LastUsedAt"] != DBNull.Value ? Convert.ToDateTime(r["LastUsedAt"]) : null
    };

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
