USE [CYHLockerDB];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 1. Extend VaultItems_CYH with Card and Subtitle Metadata
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('VaultItems_CYH') AND name = 'CardBrand')
    ALTER TABLE [dbo].[VaultItems_CYH] ADD [CardBrand] NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('VaultItems_CYH') AND name = 'CardLast4')
    ALTER TABLE [dbo].[VaultItems_CYH] ADD [CardLast4] NVARCHAR(10) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('VaultItems_CYH') AND name = 'SecondaryInfo')
    ALTER TABLE [dbo].[VaultItems_CYH] ADD [SecondaryInfo] NVARCHAR(255) NULL;
GO

-- Create index on ItemType for fast sidebar filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VaultItems_ItemType' AND object_id = OBJECT_ID('VaultItems_CYH'))
    CREATE NONCLUSTERED INDEX [IX_VaultItems_ItemType] ON [dbo].[VaultItems_CYH]([UserId], [ItemType], [IsDeleted]);
GO

-- ============================================================
-- 2. Update Stored Procedure: ManageVault_CYH
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.ManageVault_CYH
    @Flag                 NVARCHAR(50),
    @UserId               NVARCHAR(450),
    @ItemId               NVARCHAR(450)  = NULL,
    @CategoryId           NVARCHAR(450)  = NULL,
    @ItemType             NVARCHAR(50)   = 'Login',
    @Title                NVARCHAR(200)  = NULL,
    @EncryptedUsername    NVARCHAR(MAX)  = NULL,
    @EncryptedPassword    NVARCHAR(MAX)  = NULL,
    @EncryptedUrl         NVARCHAR(MAX)  = NULL,
    @EncryptedNotes       NVARCHAR(MAX)  = NULL,
    @EncryptedCustomFields NVARCHAR(MAX) = NULL,
    @EncryptionIV         NVARCHAR(200)  = NULL,
    @UrlDomain            NVARCHAR(255)  = NULL,
    @ItemIcon             NVARCHAR(255)  = NULL,
    @IsFavorite           BIT            = 0,
    @PasswordStrength     NVARCHAR(30)   = 'Strong',
    @SearchQuery          NVARCHAR(100)  = NULL,
    @CategoryName         NVARCHAR(150)  = NULL,
    @CategoryColor        NVARCHAR(30)   = '#06b6d4',
    @CategoryIcon         NVARCHAR(50)   = '📁',
    @CardBrand            NVARCHAR(50)   = NULL,
    @CardLast4            NVARCHAR(10)   = NULL,
    @SecondaryInfo        NVARCHAR(255)  = NULL,
    @OnlyFavorites        BIT            = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. GET ALL ITEMS FOR USER (WITH MULTI-FILTER) ──────
    IF @Flag = 'GET_ALL'
    BEGIN
        SELECT 
            v.Id,
            v.UserId,
            v.CategoryId,
            c.Name AS CategoryName,
            c.Color AS CategoryColor,
            v.ItemType,
            v.Title,
            v.EncryptedUsername,
            v.EncryptedPassword,
            v.EncryptedUrl,
            v.EncryptedNotes,
            v.EncryptedCustomFields,
            v.EncryptionIV,
            v.UrlDomain,
            v.ItemIcon,
            v.IsFavorite,
            v.PasswordStrength,
            v.CardBrand,
            v.CardLast4,
            v.SecondaryInfo,
            v.CreatedAt,
            v.UpdatedAt,
            v.LastUsedAt
        FROM [dbo].[VaultItems_CYH] v
        LEFT JOIN [dbo].[VaultCategories_CYH] c ON v.CategoryId = c.Id
        WHERE v.UserId = @UserId 
          AND v.IsDeleted = 0
          AND (@CategoryId IS NULL OR @CategoryId = '' OR v.CategoryId = @CategoryId)
          AND (@ItemType IS NULL OR @ItemType = '' OR @ItemType = 'All' 
               OR (@ItemType = 'Login' AND v.ItemType IN ('Login', 'Social'))
               OR v.ItemType = @ItemType)
          AND (@OnlyFavorites = 0 OR v.IsFavorite = 1)
          AND (@SearchQuery IS NULL OR @SearchQuery = '' OR 
               v.Title LIKE '%' + @SearchQuery + '%' OR 
               v.UrlDomain LIKE '%' + @SearchQuery + '%' OR
               v.SecondaryInfo LIKE '%' + @SearchQuery + '%')
        ORDER BY v.IsFavorite DESC, v.UpdatedAt DESC;
        RETURN;
    END

    -- ── 2. GET ITEM BY ID ──────────────────────────────────
    IF @Flag = 'GET_BY_ID'
    BEGIN
        SELECT TOP 1
            v.Id,
            v.UserId,
            v.CategoryId,
            c.Name AS CategoryName,
            c.Color AS CategoryColor,
            v.ItemType,
            v.Title,
            v.EncryptedUsername,
            v.EncryptedPassword,
            v.EncryptedUrl,
            v.EncryptedNotes,
            v.EncryptedCustomFields,
            v.EncryptionIV,
            v.UrlDomain,
            v.ItemIcon,
            v.IsFavorite,
            v.PasswordStrength,
            v.CardBrand,
            v.CardLast4,
            v.SecondaryInfo,
            v.CreatedAt,
            v.UpdatedAt,
            v.LastUsedAt
        FROM [dbo].[VaultItems_CYH] v
        LEFT JOIN [dbo].[VaultCategories_CYH] c ON v.CategoryId = c.Id
        WHERE v.UserId = @UserId AND v.Id = @ItemId;
        RETURN;
    END

    -- ── 3. INSERT ITEM ─────────────────────────────────────
    IF @Flag = 'INSERT'
    BEGIN
        DECLARE @NewId NVARCHAR(450) = ISNULL(@ItemId, NEWID());
        
        INSERT INTO [dbo].[VaultItems_CYH] (
            Id, UserId, CategoryId, ItemType, Title,
            EncryptedUsername, EncryptedPassword, EncryptedUrl, EncryptedNotes, EncryptedCustomFields,
            EncryptionIV, UrlDomain, ItemIcon, IsFavorite, PasswordStrength,
            CardBrand, CardLast4, SecondaryInfo,
            CreatedAt, UpdatedAt
        )
        VALUES (
            @NewId, @UserId, @CategoryId, @ItemType, @Title,
            @EncryptedUsername, @EncryptedPassword, @EncryptedUrl, @EncryptedNotes, @EncryptedCustomFields,
            @EncryptionIV, @UrlDomain, @ItemIcon, @IsFavorite, @PasswordStrength,
            @CardBrand, @CardLast4, @SecondaryInfo,
            SYSUTCDATETIME(), SYSUTCDATETIME()
        );

        SELECT @NewId AS ItemId;
        RETURN;
    END

    -- ── 4. UPDATE ITEM ─────────────────────────────────────
    IF @Flag = 'UPDATE'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH]
        SET CategoryId            = @CategoryId,
            ItemType              = @ItemType,
            Title                 = @Title,
            EncryptedUsername     = @EncryptedUsername,
            EncryptedPassword     = @EncryptedPassword,
            EncryptedUrl          = @EncryptedUrl,
            EncryptedNotes        = @EncryptedNotes,
            EncryptedCustomFields = @EncryptedCustomFields,
            EncryptionIV          = @EncryptionIV,
            UrlDomain             = @UrlDomain,
            ItemIcon              = @ItemIcon,
            IsFavorite            = @IsFavorite,
            PasswordStrength      = @PasswordStrength,
            CardBrand             = @CardBrand,
            CardLast4             = @CardLast4,
            SecondaryInfo         = @SecondaryInfo,
            UpdatedAt             = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END

    -- ── 5. SOFT DELETE ITEM (Move to Trash) ────────────────
    IF @Flag = 'DELETE'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH]
        SET IsDeleted = 1,
            DeletedAt = SYSUTCDATETIME(),
            UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END

    -- ── 6. GET TRASH ITEMS ─────────────────────────────────
    IF @Flag = 'GET_TRASH'
    BEGIN
        SELECT 
            v.Id,
            v.UserId,
            v.CategoryId,
            c.Name AS CategoryName,
            c.Color AS CategoryColor,
            v.ItemType,
            v.Title,
            v.EncryptedUsername,
            v.EncryptedPassword,
            v.EncryptedUrl,
            v.EncryptedNotes,
            v.EncryptedCustomFields,
            v.EncryptionIV,
            v.UrlDomain,
            v.ItemIcon,
            v.IsFavorite,
            v.PasswordStrength,
            v.CardBrand,
            v.CardLast4,
            v.SecondaryInfo,
            v.CreatedAt,
            v.UpdatedAt,
            v.LastUsedAt
        FROM [dbo].[VaultItems_CYH] v
        LEFT JOIN [dbo].[VaultCategories_CYH] c ON v.CategoryId = c.Id
        WHERE v.UserId = @UserId 
          AND v.IsDeleted = 1
        ORDER BY v.DeletedAt DESC;
        RETURN;
    END

    -- ── 7. RESTORE FROM TRASH ──────────────────────────────
    IF @Flag = 'RESTORE'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH]
        SET IsDeleted = 0,
            DeletedAt = NULL,
            UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END

    -- ── 8. PERMANENT DELETE (Single item) ──────────────────
    IF @Flag = 'PERMANENT_DELETE'
    BEGIN
        DELETE FROM [dbo].[VaultItems_CYH]
        WHERE Id = @ItemId AND UserId = @UserId;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END

    -- ── 9. EMPTY TRASH (All deleted items for user) ────────
    IF @Flag = 'EMPTY_TRASH'
    BEGIN
        DELETE FROM [dbo].[VaultItems_CYH]
        WHERE UserId = @UserId AND IsDeleted = 1;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END

    -- ── 10. TOGGLE FAVORITE ────────────────────────────────
    IF @Flag = 'TOGGLE_FAVORITE'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH]
        SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END,
            UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;

        SELECT IsFavorite FROM [dbo].[VaultItems_CYH] WHERE Id = @ItemId;
        RETURN;
    END

    -- ── 11. GET VAULT COUNTS FOR SIDEBAR ───────────────────
    IF @Flag = 'GET_COUNTS'
    BEGIN
        SELECT 
            COUNT(CASE WHEN IsDeleted = 0 THEN 1 END) AS TotalAll,
            COUNT(CASE WHEN IsDeleted = 0 AND ItemType = 'Login' THEN 1 END) AS TotalLogins,
            COUNT(CASE WHEN IsDeleted = 0 AND ItemType = 'Social' THEN 1 END) AS TotalSocial,
            COUNT(CASE WHEN IsDeleted = 0 AND ItemType = 'Card' THEN 1 END) AS TotalCards,
            COUNT(CASE WHEN IsDeleted = 0 AND ItemType = 'Document' THEN 1 END) AS TotalDocuments,
            COUNT(CASE WHEN IsDeleted = 0 AND ItemType = 'ApiKey' THEN 1 END) AS TotalApiKeys,
            COUNT(CASE WHEN IsDeleted = 0 AND IsFavorite = 1 THEN 1 END) AS TotalFavorites,
            COUNT(CASE WHEN IsDeleted = 1 THEN 1 END) AS TotalTrash
        FROM [dbo].[VaultItems_CYH]
        WHERE UserId = @UserId;
        RETURN;
    END

    -- ── 12. AUTOFILL MATCH ─────────────────────────────────
    IF @Flag = 'AUTOFILL_MATCH'
    BEGIN
        SELECT 
            v.Id,
            v.UserId,
            v.Title,
            v.EncryptedUsername,
            v.EncryptedPassword,
            v.EncryptedUrl,
            v.EncryptionIV,
            v.UrlDomain,
            v.ItemIcon
        FROM [dbo].[VaultItems_CYH] v
        WHERE v.UserId = @UserId 
          AND v.IsDeleted = 0
          AND (v.UrlDomain = @UrlDomain OR @UrlDomain LIKE '%' + v.UrlDomain + '%')
        ORDER BY v.IsFavorite DESC, v.LastUsedAt DESC;
        RETURN;
    END

    -- ── 13. GET CATEGORIES ─────────────────────────────────
    IF @Flag = 'GET_CATEGORIES'
    BEGIN
        SELECT 
            c.Id,
            c.UserId,
            c.Name,
            c.Icon,
            c.Color,
            c.SortOrder,
            c.CreatedAt,
            COUNT(v.Id) AS TotalItems
        FROM [dbo].[VaultCategories_CYH] c
        LEFT JOIN [dbo].[VaultItems_CYH] v ON c.Id = v.CategoryId AND v.IsDeleted = 0
        WHERE c.UserId = @UserId
        GROUP BY c.Id, c.UserId, c.Name, c.Icon, c.Color, c.SortOrder, c.CreatedAt
        ORDER BY c.SortOrder ASC, c.Name ASC;
        RETURN;
    END

    -- ── 14. INSERT CATEGORY ────────────────────────────────
    IF @Flag = 'INSERT_CATEGORY'
    BEGIN
        DECLARE @NewCatId NVARCHAR(450) = ISNULL(@CategoryId, NEWID());
        INSERT INTO [dbo].[VaultCategories_CYH] (Id, UserId, Name, Icon, Color, SortOrder, CreatedAt)
        VALUES (@NewCatId, @UserId, @CategoryName, @CategoryIcon, @CategoryColor, 0, SYSUTCDATETIME());

        SELECT @NewCatId AS CategoryId;
        RETURN;
    END

    -- ── 15. DELETE CATEGORY ────────────────────────────────
    IF @Flag = 'DELETE_CATEGORY'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH] 
        SET CategoryId = NULL 
        WHERE CategoryId = @CategoryId AND UserId = @UserId;

        DELETE FROM [dbo].[VaultCategories_CYH]
        WHERE Id = @CategoryId AND UserId = @UserId;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END

    -- ── 16. RECORD USAGE TIMESTAMP ────────────────────────
    IF @Flag = 'TOUCH_USED'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH]
        SET LastUsedAt = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;
        RETURN;
    END
END
GO
