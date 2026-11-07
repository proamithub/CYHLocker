USE [CYHLockerDB];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 1. Ensure Indexes on AuditLogs_CYH
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Timestamp' AND object_id = OBJECT_ID('AuditLogs_CYH'))
    CREATE NONCLUSTERED INDEX [IX_AuditLogs_Timestamp] ON [dbo].[AuditLogs_CYH]([Timestamp] DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_UserId' AND object_id = OBJECT_ID('AuditLogs_CYH'))
    CREATE NONCLUSTERED INDEX [IX_AuditLogs_UserId] ON [dbo].[AuditLogs_CYH]([UserId]);
GO

-- ============================================================
-- 2. Stored Procedure: ManageAuditLogs_CYH
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.ManageAuditLogs_CYH
    @Flag          NVARCHAR(50),
    @UserId        NVARCHAR(450)  = NULL,
    @EventType     NVARCHAR(100)  = NULL,
    @IpAddress     NVARCHAR(50)   = NULL,
    @Details       NVARCHAR(MAX)  = NULL,
    @TopCount      INT            = 200
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. LOG AN AUDIT EVENT ──────────────────────────────
    IF @Flag = 'LOG_EVENT'
    BEGIN
        INSERT INTO [dbo].[AuditLogs_CYH] (UserId, EventType, IpAddress, Details, Timestamp)
        VALUES (ISNULL(@UserId, 'SYSTEM'), @EventType, @IpAddress, @Details, SYSUTCDATETIME());

        SELECT SCOPE_IDENTITY() AS LogId;
        RETURN;
    END

    -- ── 2. GET RECENT AUDIT LOGS ───────────────────────────
    IF @Flag = 'GET_ALL'
    BEGIN
        SELECT TOP (@TopCount)
            a.Id,
            a.UserId,
            ISNULL(u.Email, 'system@cyhlocker.com') AS UserEmail,
            ISNULL(u.FullName, 'System') AS UserFullName,
            a.EventType,
            a.IpAddress,
            a.Details,
            a.Timestamp
        FROM [dbo].[AuditLogs_CYH] a
        LEFT JOIN [dbo].[Users_CYH] u ON a.UserId = u.Id
        WHERE (@EventType IS NULL OR @EventType = '' OR @EventType = 'ALL' OR a.EventType = @EventType)
          AND (@UserId IS NULL OR @UserId = '' OR a.UserId = @UserId)
        ORDER BY a.Timestamp DESC;
        RETURN;
    END

    -- ── 3. GET SYSTEM SECURITY STATS ───────────────────────
    IF @Flag = 'GET_STATS'
    BEGIN
        SELECT
            (SELECT COUNT(1) FROM [dbo].[Users_CYH]) AS TotalUsers,
            (SELECT COUNT(1) FROM [dbo].[Users_CYH] WHERE IsActive = 1) AS ActiveUsers,
            (SELECT COUNT(1) FROM [dbo].[Users_CYH] WHERE LockoutEnd IS NOT NULL AND LockoutEnd > SYSUTCDATETIME()) AS LockedUsers,
            (SELECT COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE IsDeleted = 0) AS TotalVaultItems,
            (SELECT COUNT(1) FROM [dbo].[UserSessions_CYH] WHERE IsRevoked = 0 AND ExpiresAt > SYSUTCDATETIME()) AS ActiveSessions,
            (SELECT COUNT(1) FROM [dbo].[AuditLogs_CYH] WHERE EventType LIKE '%FAIL%' AND Timestamp >= DATEADD(hour, -24, SYSUTCDATETIME())) AS FailedLogins24h,
            (SELECT COUNT(1) FROM [dbo].[AuditLogs_CYH]) AS TotalAuditEvents;
        RETURN;
    END
END
GO

-- ============================================================
-- 3. Stored Procedure: ManageAdminUsers_CYH
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.ManageAdminUsers_CYH
    @Flag          NVARCHAR(50),
    @AdminUserId   NVARCHAR(450)  = NULL,
    @TargetUserId  NVARCHAR(450)  = NULL,
    @NewRole       NVARCHAR(50)   = NULL,
    @IsActive      BIT            = NULL,
    @SearchQuery   NVARCHAR(150)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. GET ALL USERS WITH ITEM COUNTS ──────────────────
    IF @Flag = 'GET_ALL_USERS'
    BEGIN
        SELECT 
            u.Id,
            u.Email,
            u.FullName,
            u.Role,
            u.IsActive,
            CASE WHEN u.LockoutEnd IS NOT NULL AND u.LockoutEnd > SYSUTCDATETIME() THEN 1 ELSE 0 END AS IsLocked,
            u.LockoutEnd,
            u.AccessFailedCount,
            u.CreatedAt,
            u.LastLoginAt,
            u.LastLoginIp,
            COUNT(v.Id) AS TotalVaultItems
        FROM [dbo].[Users_CYH] u
        LEFT JOIN [dbo].[VaultItems_CYH] v ON u.Id = v.UserId AND v.IsDeleted = 0
        WHERE (@SearchQuery IS NULL OR @SearchQuery = '' OR 
               u.Email LIKE '%' + @SearchQuery + '%' OR 
               u.FullName LIKE '%' + @SearchQuery + '%')
        GROUP BY 
            u.Id, u.Email, u.FullName, u.Role, u.IsActive, 
            u.LockoutEnd, u.AccessFailedCount, u.CreatedAt, u.LastLoginAt, u.LastLoginIp
        ORDER BY u.CreatedAt DESC;
        RETURN;
    END

    -- ── 2. TOGGLE USER ACTIVE STATUS ───────────────────────
    IF @Flag = 'TOGGLE_STATUS'
    BEGIN
        UPDATE [dbo].[Users_CYH]
        SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END,
            UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @TargetUserId;

        SELECT IsActive FROM [dbo].[Users_CYH] WHERE Id = @TargetUserId;
        RETURN;
    END

    -- ── 3. TOGGLE / RESET USER LOCKOUT ─────────────────────
    IF @Flag = 'TOGGLE_LOCK'
    BEGIN
        DECLARE @CurrentLock DATETIME2(7);
        SELECT @CurrentLock = LockoutEnd FROM [dbo].[Users_CYH] WHERE Id = @TargetUserId;

        IF @CurrentLock IS NOT NULL AND @CurrentLock > SYSUTCDATETIME()
        BEGIN
            -- Currently locked -> Unlock it!
            UPDATE [dbo].[Users_CYH]
            SET LockoutEnd = NULL,
                AccessFailedCount = 0,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @TargetUserId;
            SELECT 0 AS IsLocked;
            RETURN;
        END
        ELSE
        BEGIN
            -- Currently unlocked -> Lock for 30 days
            UPDATE [dbo].[Users_CYH]
            SET LockoutEnd = DATEADD(day, 30, SYSUTCDATETIME()),
                AccessFailedCount = 5,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @TargetUserId;
            SELECT 1 AS IsLocked;
            RETURN;
        END
    END

    -- ── 4. CHANGE USER ROLE (Admin <-> User) ───────────────
    IF @Flag = 'CHANGE_ROLE'
    BEGIN
        IF @NewRole IN ('User', 'Admin')
        BEGIN
            UPDATE [dbo].[Users_CYH]
            SET Role = @NewRole,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @TargetUserId;

            SELECT 1 AS Success;
            RETURN;
        END
        SELECT 0 AS Success;
        RETURN;
    END

    -- ── 5. DELETE USER (Cascade cleanup) ───────────────────
    IF @Flag = 'DELETE_USER'
    BEGIN
        -- Safety: Prevent deleting self
        IF @AdminUserId = @TargetUserId
        BEGIN
            SELECT 0 AS Success;
            RETURN;
        END

        -- Delete user (FK CASCADE removes Sessions, Categories, VaultItems)
        DELETE FROM [dbo].[Users_CYH]
        WHERE Id = @TargetUserId;

        SELECT @@ROWCOUNT AS Success;
        RETURN;
    END
END
GO
