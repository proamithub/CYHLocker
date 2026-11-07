USE [CYHLockerDB];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- HIGH-PERFORMANCE COVERING INDEXES FOR CYHLOCKER
-- Sub-millisecond reads for Vault, Autofill, and Audit stream
-- ============================================================

-- 1. Vault Query Index (User + Deleted + Type + Favorites)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VaultItems_User_Filter_Perf' AND object_id = OBJECT_ID('dbo.VaultItems_CYH'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_VaultItems_User_Filter_Perf]
    ON [dbo].[VaultItems_CYH] ([UserId] ASC, [IsDeleted] ASC, [ItemType] ASC, [IsFavorite] DESC)
    INCLUDE ([Title], [UrlDomain], [ItemIcon], [CardBrand], [CardLast4], [SecondaryInfo], [UpdatedAt], [CreatedAt]);
END
GO

-- 2. Fast Auto-fill Domain Lookup Index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_VaultItems_Domain_Autofill' AND object_id = OBJECT_ID('dbo.VaultItems_CYH'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_VaultItems_Domain_Autofill]
    ON [dbo].[VaultItems_CYH] ([UserId] ASC, [IsDeleted] ASC, [UrlDomain] ASC)
    INCLUDE ([Title], [EncryptedUsername], [EncryptedPassword], [EncryptionIV], [ItemIcon], [IsFavorite], [LastUsedAt]);
END
GO

-- 3. Live Audit Stream Performance Index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_User_Timestamp_Perf' AND object_id = OBJECT_ID('dbo.AuditLogs_CYH'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLogs_User_Timestamp_Perf]
    ON [dbo].[AuditLogs_CYH] ([UserId] ASC, [Timestamp] DESC)
    INCLUDE ([EventType], [IpAddress], [Details]);
END
GO

-- 4. Device Sync Lookup Index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeviceSync_User_Device_Perf' AND object_id = OBJECT_ID('dbo.DeviceSyncRegistrations_CYH'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeviceSync_User_Device_Perf]
    ON [dbo].[DeviceSyncRegistrations_CYH] ([UserId] ASC, [DeviceId] ASC)
    INCLUDE ([DeviceName], [DeviceType], [LastSyncUtc], [IsActive]);
END
GO
