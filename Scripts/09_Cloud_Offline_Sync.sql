USE [CYHLockerDB];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 1. DeviceSyncRegistrations_CYH Table
-- Tracks client devices (Browser Extensions, Mobile Apps, Desktop, Web)
-- ============================================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeviceSyncRegistrations_CYH]') AND type = 'U')
BEGIN
    DROP TABLE [dbo].[DeviceSyncRegistrations_CYH];
END
GO

CREATE TABLE [dbo].[DeviceSyncRegistrations_CYH] (
    [Id]             NVARCHAR(100)  NOT NULL,
    [DeviceId]       NVARCHAR(100)  NOT NULL,
    [UserId]         NVARCHAR(450)  NOT NULL,
    [DeviceName]     NVARCHAR(150)  NOT NULL,
    [DeviceType]     NVARCHAR(50)   NOT NULL DEFAULT ('BrowserExtension'), -- BrowserExtension, Mobile, Web, Desktop
    [ClientVersion]  NVARCHAR(50)   NULL,
    [LastSyncUtc]    DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
    [RegisteredAt]   DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
    [IsActive]       BIT            NOT NULL DEFAULT (1),
    [LastIpAddress]  NVARCHAR(50)   NULL,
    CONSTRAINT [PK_DeviceSyncRegistrations_CYH] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_DeviceSync_Device_User] UNIQUE NONCLUSTERED ([DeviceId] ASC, [UserId] ASC),
    CONSTRAINT [FK_DeviceSync_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users_CYH]([Id]) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX [IX_DeviceSync_UserId] ON [dbo].[DeviceSyncRegistrations_CYH]([UserId] ASC);
GO

-- ============================================================
-- 2. Stored Procedure: dbo.ManageSync_CYH
-- High-performance Delta Pull & Push Sync Engine
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[ManageSync_CYH]
    @Flag             NVARCHAR(50),
    @UserId           NVARCHAR(450),
    @DeviceId         NVARCHAR(100)  = NULL,
    @DeviceName       NVARCHAR(150)  = NULL,
    @DeviceType       NVARCHAR(50)   = NULL,
    @ClientVersion    NVARCHAR(50)   = NULL,
    @LastIpAddress    NVARCHAR(50)   = NULL,
    @SinceUtc         DATETIME2(7)   = NULL,
    @ItemId           NVARCHAR(450)  = NULL,
    @CategoryId       NVARCHAR(450)  = NULL,
    @ItemType         NVARCHAR(50)   = NULL,
    @Title            NVARCHAR(200)  = NULL,
    @EncryptedUsername NVARCHAR(MAX) = NULL,
    @EncryptedPassword NVARCHAR(MAX) = NULL,
    @EncryptedUrl     NVARCHAR(MAX)  = NULL,
    @EncryptedNotes   NVARCHAR(MAX)  = NULL,
    @EncryptedCustomFields NVARCHAR(MAX) = NULL,
    @EncryptionIV     NVARCHAR(200)  = NULL,
    @UrlDomain        NVARCHAR(255)  = NULL,
    @ItemIcon         NVARCHAR(100)  = NULL,
    @IsFavorite       BIT            = 0,
    @PasswordStrength NVARCHAR(30)   = 'Strong',
    @CardBrand        NVARCHAR(50)   = NULL,
    @CardLast4        NVARCHAR(10)   = NULL,
    @SecondaryInfo    NVARCHAR(200)  = NULL,
    @IsDeleted        BIT            = 0,
    @ClientUpdatedAt  DATETIME2(7)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. REGISTER OR HEARTBEAT DEVICE ────────────────────
    IF @Flag = 'REGISTER_DEVICE'
    BEGIN
        IF EXISTS (SELECT 1 FROM [dbo].[DeviceSyncRegistrations_CYH] WHERE DeviceId = @DeviceId AND UserId = @UserId)
        BEGIN
            UPDATE [dbo].[DeviceSyncRegistrations_CYH]
            SET DeviceName = ISNULL(@DeviceName, DeviceName),
                DeviceType = ISNULL(@DeviceType, DeviceType),
                ClientVersion = ISNULL(@ClientVersion, ClientVersion),
                LastIpAddress = ISNULL(@LastIpAddress, LastIpAddress),
                LastSyncUtc = SYSUTCDATETIME(),
                IsActive = 1
            WHERE DeviceId = @DeviceId AND UserId = @UserId;
        END
        ELSE
        BEGIN
            INSERT INTO [dbo].[DeviceSyncRegistrations_CYH] (
                Id, DeviceId, UserId, DeviceName, DeviceType, ClientVersion, LastIpAddress, LastSyncUtc, RegisteredAt, IsActive
            )
            VALUES (
                NEWID(), @DeviceId, @UserId, 
                ISNULL(@DeviceName, 'CYHLocker Client'), 
                ISNULL(@DeviceType, 'BrowserExtension'),
                @ClientVersion, @LastIpAddress, SYSUTCDATETIME(), SYSUTCDATETIME(), 1
            );
        END

        SELECT TOP 1
            Id, DeviceId, UserId, DeviceName, DeviceType, ClientVersion, LastSyncUtc, RegisteredAt, IsActive
        FROM [dbo].[DeviceSyncRegistrations_CYH]
        WHERE DeviceId = @DeviceId AND UserId = @UserId;
        RETURN;
    END

    -- ── 2. PULL CHANGES (Delta Sync) ───────────────────────
    IF @Flag = 'PULL_CHANGES'
    BEGIN
        DECLARE @CurrentServerUtc DATETIME2(7) = SYSUTCDATETIME();

        -- Update device last sync timestamp if DeviceId provided
        IF @DeviceId IS NOT NULL
        BEGIN
            UPDATE [dbo].[DeviceSyncRegistrations_CYH]
            SET LastSyncUtc = @CurrentServerUtc
            WHERE DeviceId = @DeviceId AND UserId = @UserId;
        END

        -- If @SinceUtc is NULL or min date, return ALL items
        IF @SinceUtc IS NULL OR @SinceUtc < '1970-01-01'
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
                v.IsDeleted,
                v.DeletedAt,
                v.CreatedAt,
                v.UpdatedAt,
                v.LastUsedAt,
                @CurrentServerUtc AS ServerTimestampUtc
            FROM [dbo].[VaultItems_CYH] v
            LEFT JOIN [dbo].[VaultCategories_CYH] c ON v.CategoryId = c.Id
            WHERE v.UserId = @UserId
            ORDER BY v.UpdatedAt ASC;
        END
        ELSE
        BEGIN
            -- Delta: Return items modified or deleted since @SinceUtc
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
                v.IsDeleted,
                v.DeletedAt,
                v.CreatedAt,
                v.UpdatedAt,
                v.LastUsedAt,
                @CurrentServerUtc AS ServerTimestampUtc
            FROM [dbo].[VaultItems_CYH] v
            LEFT JOIN [dbo].[VaultCategories_CYH] c ON v.CategoryId = c.Id
            WHERE v.UserId = @UserId
              AND (v.UpdatedAt >= @SinceUtc OR v.CreatedAt >= @SinceUtc)
            ORDER BY v.UpdatedAt ASC;
        END
        RETURN;
    END

    -- ── 3. PUSH SINGLE ITEM (Upsert with Last-Write-Wins) ───
    IF @Flag = 'PUSH_ITEM'
    BEGIN
        DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
        DECLARE @ExistingUpdatedAt DATETIME2(7);
        DECLARE @ResultStatus NVARCHAR(50) = 'INSERTED';

        IF EXISTS (SELECT 1 FROM [dbo].[VaultItems_CYH] WHERE Id = @ItemId AND UserId = @UserId)
        BEGIN
            SELECT @ExistingUpdatedAt = UpdatedAt 
            FROM [dbo].[VaultItems_CYH] 
            WHERE Id = @ItemId AND UserId = @UserId;

            -- Conflict check: if client timestamp is older than existing DB timestamp by > 2 seconds, conflict!
            -- Otherwise accept client update (Last-Write-Wins)
            IF @ClientUpdatedAt IS NOT NULL AND @ExistingUpdatedAt IS NOT NULL AND @ClientUpdatedAt < DATEADD(second, -2, @ExistingUpdatedAt)
            BEGIN
                -- Server has newer version
                SELECT 'CONFLICT' AS [Status], @ExistingUpdatedAt AS ServerUpdatedAt, @Now AS ServerTimestampUtc;
                RETURN;
            END

            UPDATE [dbo].[VaultItems_CYH]
            SET CategoryId            = @CategoryId,
                ItemType              = ISNULL(@ItemType, ItemType),
                Title                 = ISNULL(@Title, Title),
                EncryptedUsername     = @EncryptedUsername,
                EncryptedPassword     = @EncryptedPassword,
                EncryptedUrl          = @EncryptedUrl,
                EncryptedNotes        = @EncryptedNotes,
                EncryptedCustomFields = @EncryptedCustomFields,
                EncryptionIV          = ISNULL(@EncryptionIV, EncryptionIV),
                UrlDomain             = @UrlDomain,
                ItemIcon              = @ItemIcon,
                IsFavorite            = @IsFavorite,
                PasswordStrength      = ISNULL(@PasswordStrength, PasswordStrength),
                CardBrand             = @CardBrand,
                CardLast4             = @CardLast4,
                SecondaryInfo         = @SecondaryInfo,
                IsDeleted             = @IsDeleted,
                DeletedAt             = CASE WHEN @IsDeleted = 1 THEN ISNULL(DeletedAt, @Now) ELSE NULL END,
                UpdatedAt             = @Now
            WHERE Id = @ItemId AND UserId = @UserId;

            SET @ResultStatus = 'UPDATED';
        END
        ELSE
        BEGIN
            -- Brand new item inserted from client
            INSERT INTO [dbo].[VaultItems_CYH] (
                Id, UserId, CategoryId, ItemType, Title,
                EncryptedUsername, EncryptedPassword, EncryptedUrl, EncryptedNotes, EncryptedCustomFields,
                EncryptionIV, UrlDomain, ItemIcon, IsFavorite, PasswordStrength,
                CardBrand, CardLast4, SecondaryInfo,
                IsDeleted, DeletedAt, CreatedAt, UpdatedAt
            )
            VALUES (
                @ItemId, @UserId, @CategoryId, ISNULL(@ItemType, 'Login'), ISNULL(@Title, 'Untitled'),
                @EncryptedUsername, @EncryptedPassword, @EncryptedUrl, @EncryptedNotes, @EncryptedCustomFields,
                ISNULL(@EncryptionIV, ''), @UrlDomain, @ItemIcon, @IsFavorite, ISNULL(@PasswordStrength, 'Strong'),
                @CardBrand, @CardLast4, @SecondaryInfo,
                @IsDeleted, CASE WHEN @IsDeleted = 1 THEN @Now ELSE NULL END,
                ISNULL(@ClientUpdatedAt, @Now), @Now
            );

            SET @ResultStatus = 'INSERTED';
        END

        -- Also record device heartbeat
        IF @DeviceId IS NOT NULL
        BEGIN
            UPDATE [dbo].[DeviceSyncRegistrations_CYH]
            SET LastSyncUtc = @Now
            WHERE DeviceId = @DeviceId AND UserId = @UserId;
        END

        SELECT @ResultStatus AS [Status], @Now AS ServerUpdatedAt, @Now AS ServerTimestampUtc;
        RETURN;
    END

    -- ── 4. GET REGISTERED DEVICES & SYNC STATUS ─────────────
    IF @Flag = 'GET_STATUS'
    BEGIN
        SELECT 
            Id, DeviceId, UserId, DeviceName, DeviceType, ClientVersion, LastSyncUtc, RegisteredAt, IsActive
        FROM [dbo].[DeviceSyncRegistrations_CYH]
        WHERE UserId = @UserId
        ORDER BY LastSyncUtc DESC;

        SELECT 
            COUNT(CASE WHEN IsDeleted = 0 THEN 1 END) AS ActiveVaultItems,
            COUNT(CASE WHEN IsDeleted = 1 THEN 1 END) AS TrashVaultItems,
            MAX(UpdatedAt) AS LastVaultUpdateUtc,
            SYSUTCDATETIME() AS CurrentServerUtc
        FROM [dbo].[VaultItems_CYH]
        WHERE UserId = @UserId;
        RETURN;
    END
END
GO
