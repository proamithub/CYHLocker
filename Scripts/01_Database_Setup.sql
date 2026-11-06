USE [master];
GO
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'CYHLockerDB')
    CREATE DATABASE [CYHLockerDB];
GO
USE [CYHLockerDB];
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
-- Users Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Users_CYH] (
        [Id]               NVARCHAR(450)  NOT NULL,
        [Email]            NVARCHAR(256)  NOT NULL,
        [NormalizedEmail]  NVARCHAR(256)  NOT NULL,
        [FullName]         NVARCHAR(200)  NOT NULL,
        [PasswordHash]     NVARCHAR(MAX)  NOT NULL,
        [Role]             NVARCHAR(50)   NOT NULL DEFAULT ('User'),
        [IsActive]         BIT            NOT NULL DEFAULT (1),
        [EmailConfirmed]   BIT            NOT NULL DEFAULT (1),
        [TwoFactorEnabled] BIT            NOT NULL DEFAULT (0),
        [AccessFailedCount] INT           NOT NULL DEFAULT (0),
        [LockoutEnabled]   BIT            NOT NULL DEFAULT (1),
        [LockoutEnd]       DATETIME2(7)   NULL,
        [LastLoginAt]      DATETIME2(7)   NULL,
        [LastLoginIp]      NVARCHAR(50)   NULL,
        [CreatedAt]        DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt]        DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users_CYH] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_CYH_Email] ON [dbo].[Users_CYH]([NormalizedEmail] ASC);
END
GO
-- AuditLogs Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[AuditLogs_CYH] (
        [Id]        BIGINT IDENTITY(1,1) NOT NULL,
        [UserId]    NVARCHAR(450)        NOT NULL,
        [EventType] NVARCHAR(100)        NOT NULL,
        [IpAddress] NVARCHAR(50)         NULL,
        [Details]   NVARCHAR(MAX)        NULL,
        [Timestamp] DATETIME2(7)         NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AuditLogs_CYH] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO
