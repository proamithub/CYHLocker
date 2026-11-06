USE [CYHLockerDB];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 1. VaultCategories_CYH Table (Folders / Categories)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VaultCategories_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[VaultCategories_CYH] (
        [Id]         NVARCHAR(450)  NOT NULL,
        [UserId]     NVARCHAR(450)  NOT NULL,
        [Name]       NVARCHAR(150)  NOT NULL,
        [Icon]       NVARCHAR(50)   NULL DEFAULT ('📁'),
        [Color]      NVARCHAR(30)   NULL DEFAULT ('#06b6d4'),
        [SortOrder]  INT            NOT NULL DEFAULT (0),
        [CreatedAt]  DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_VaultCategories_CYH] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_VaultCategories_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users_CYH]([Id]) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX [IX_VaultCategories_UserId] ON [dbo].[VaultCategories_CYH]([UserId] ASC);
END
GO

-- ============================================================
-- 2. VaultItems_CYH Table (Core Encrypted Credentials)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VaultItems_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[VaultItems_CYH] (
        [Id]                     NVARCHAR(450)  NOT NULL,
        [UserId]                 NVARCHAR(450)  NOT NULL,
        [CategoryId]             NVARCHAR(450)  NULL,
        [ItemType]               NVARCHAR(50)   NOT NULL DEFAULT ('Login'), -- Login, Card, Identity, ApiKey
        [Title]                  NVARCHAR(200)  NOT NULL,                  -- Display title (e.g. 'Google Work Account')
        
        -- Encrypted Fields (AES-256-GCM cipher payload: Base64)
        [EncryptedUsername]      NVARCHAR(MAX)  NULL,
        [EncryptedPassword]      NVARCHAR(MAX)  NULL,
        [EncryptedUrl]           NVARCHAR(MAX)  NULL,
        [EncryptedNotes]         NVARCHAR(MAX)  NULL,
        [EncryptedCustomFields]  NVARCHAR(MAX)  NULL,                      -- JSON blob for extra encrypted properties
        [EncryptionIV]           NVARCHAR(200)  NOT NULL,                  -- Nonce/IV (Base64)
        
        -- Plain metadata for search, filtering, and auto-fill matching
        [UrlDomain]              NVARCHAR(255)  NULL,                      -- e.g. 'github.com', 'accounts.google.com'
        [ItemIcon]               NVARCHAR(100)  NULL,                      -- Favicon or emoji
        [IsFavorite]             BIT            NOT NULL DEFAULT (0),
        [PasswordStrength]       NVARCHAR(30)   NULL DEFAULT ('Strong'),   -- Weak, Fair, Strong, VeryStrong
        [IsDeleted]              BIT            NOT NULL DEFAULT (0),      -- Soft delete
        [DeletedAt]              DATETIME2(7)   NULL,
        [CreatedAt]              DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt]              DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        [LastUsedAt]             DATETIME2(7)   NULL,
        
        CONSTRAINT [PK_VaultItems_CYH] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_VaultItems_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users_CYH]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_VaultItems_Categories] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[VaultCategories_CYH]([Id]) ON DELETE NO ACTION
    );
    CREATE NONCLUSTERED INDEX [IX_VaultItems_UserId_Deleted] ON [dbo].[VaultItems_CYH]([UserId] ASC, [IsDeleted] ASC);
    CREATE NONCLUSTERED INDEX [IX_VaultItems_Domain] ON [dbo].[VaultItems_CYH]([UserId] ASC, [UrlDomain] ASC) WHERE [IsDeleted] = 0;
END
GO

-- ============================================================
-- 3. SecureNotes_CYH Table (Encrypted Notes)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SecureNotes_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[SecureNotes_CYH] (
        [Id]             NVARCHAR(450)  NOT NULL,
        [UserId]         NVARCHAR(450)  NOT NULL,
        [CategoryId]     NVARCHAR(450)  NULL,
        [Title]          NVARCHAR(200)  NOT NULL,
        [EncryptedBody]  NVARCHAR(MAX)  NOT NULL,
        [EncryptionIV]   NVARCHAR(200)  NOT NULL,
        [IsPinned]       BIT            NOT NULL DEFAULT (0),
        [Tags]           NVARCHAR(500)  NULL,
        [CreatedAt]      DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt]      DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_SecureNotes_CYH] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_SecureNotes_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users_CYH]([Id]) ON DELETE CASCADE
    );
END
GO

-- ============================================================
-- 4. VaultAttachments_CYH Table (Encrypted Documents/Files)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VaultAttachments_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[VaultAttachments_CYH] (
        [Id]                       NVARCHAR(450)  NOT NULL,
        [UserId]                   NVARCHAR(450)  NOT NULL,
        [VaultItemId]              NVARCHAR(450)  NULL,
        [EncryptedOriginalName]    NVARCHAR(MAX)  NOT NULL,
        [StoredFileName]           NVARCHAR(500)  NOT NULL,
        [FileSizeBytes]            BIGINT         NOT NULL,
        [MimeType]                 NVARCHAR(150)  NOT NULL,
        [StoragePath]              NVARCHAR(1000) NOT NULL,
        [EncryptionIV]             NVARCHAR(200)  NOT NULL,
        [FileHash]                 NVARCHAR(200)  NOT NULL,
        [CreatedAt]                DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_VaultAttachments_CYH] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_VaultAttachments_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users_CYH]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_VaultAttachments_Items] FOREIGN KEY ([VaultItemId]) REFERENCES [dbo].[VaultItems_CYH]([Id]) ON DELETE NO ACTION
    );
END
GO

-- ============================================================
-- 5. Stored Procedure: ManageVault_CYH
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
    @ItemIcon             NVARCHAR(100)  = NULL,
    @IsFavorite           BIT            = 0,
    @PasswordStrength     NVARCHAR(30)   = 'Strong',
    @SearchQuery          NVARCHAR(100)  = NULL,
    @CategoryName         NVARCHAR(150)  = NULL,
    @CategoryColor        NVARCHAR(30)   = '#06b6d4',
    @CategoryIcon         NVARCHAR(50)   = '📁'
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. GET ALL ITEMS FOR USER ──────────────────────────
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
            v.CreatedAt,
            v.UpdatedAt,
            v.LastUsedAt
        FROM [dbo].[VaultItems_CYH] v
        LEFT JOIN [dbo].[VaultCategories_CYH] c ON v.CategoryId = c.Id
        WHERE v.UserId = @UserId 
          AND v.IsDeleted = 0
          AND (@CategoryId IS NULL OR v.CategoryId = @CategoryId)
          AND (@SearchQuery IS NULL OR v.Title LIKE '%' + @SearchQuery + '%' OR v.UrlDomain LIKE '%' + @SearchQuery + '%')
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
            v.CreatedAt,
            v.UpdatedAt,
            v.LastUsedAt
        FROM [dbo].[VaultItems_CYH] v
        LEFT JOIN [dbo].[VaultCategories_CYH] c ON v.CategoryId = c.Id
        WHERE v.UserId = @UserId AND v.Id = @ItemId AND v.IsDeleted = 0;
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
            CreatedAt, UpdatedAt
        )
        VALUES (
            @NewId, @UserId, @CategoryId, @ItemType, @Title,
            @EncryptedUsername, @EncryptedPassword, @EncryptedUrl, @EncryptedNotes, @EncryptedCustomFields,
            @EncryptionIV, @UrlDomain, @ItemIcon, @IsFavorite, @PasswordStrength,
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
            UpdatedAt             = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END

    -- ── 5. DELETE ITEM (Soft Delete) ───────────────────────
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

    -- ── 6. TOGGLE FAVORITE ─────────────────────────────────
    IF @Flag = 'TOGGLE_FAVORITE'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH]
        SET IsFavorite = CASE WHEN IsFavorite = 1 THEN 0 ELSE 1 END,
            UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;

        SELECT IsFavorite FROM [dbo].[VaultItems_CYH] WHERE Id = @ItemId;
        RETURN;
    END

    -- ── 7. AUTOFILL MATCH ──────────────────────────────────
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

    -- ── 8. GET CATEGORIES ──────────────────────────────────
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

    -- ── 9. INSERT CATEGORY ─────────────────────────────────
    IF @Flag = 'INSERT_CATEGORY'
    BEGIN
        DECLARE @NewCatId NVARCHAR(450) = ISNULL(@CategoryId, NEWID());
        INSERT INTO [dbo].[VaultCategories_CYH] (Id, UserId, Name, Icon, Color, SortOrder, CreatedAt)
        VALUES (@NewCatId, @UserId, @CategoryName, @CategoryIcon, @CategoryColor, 0, SYSUTCDATETIME());

        SELECT @NewCatId AS CategoryId;
        RETURN;
    END

    -- ── 10. DELETE CATEGORY ────────────────────────────────
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

    -- ── 11. RECORD USAGE TIMESTAMP ─────────────────────────
    IF @Flag = 'TOUCH_USED'
    BEGIN
        UPDATE [dbo].[VaultItems_CYH]
        SET LastUsedAt = SYSUTCDATETIME()
        WHERE Id = @ItemId AND UserId = @UserId;
        RETURN;
    END
END
GO
