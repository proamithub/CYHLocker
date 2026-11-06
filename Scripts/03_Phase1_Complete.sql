USE [CYHLockerDB];
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Add missing columns to Users_CYH if not exist
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users_CYH') AND name = 'PhoneNumber')
    ALTER TABLE Users_CYH ADD PhoneNumber NVARCHAR(50) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users_CYH') AND name = 'AvatarUrl')
    ALTER TABLE Users_CYH ADD AvatarUrl NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users_CYH') AND name = 'PasswordResetToken')
    ALTER TABLE Users_CYH ADD PasswordResetToken NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users_CYH') AND name = 'PasswordResetExpiry')
    ALTER TABLE Users_CYH ADD PasswordResetExpiry DATETIME2(7) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users_CYH') AND name = 'TotpSecret')
    ALTER TABLE Users_CYH ADD TotpSecret NVARCHAR(MAX) NULL;
GO

-- ============================================================
-- UserSessions_CYH Table (Device Management)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserSessions_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[UserSessions_CYH] (
        [Id]           NVARCHAR(450)  NOT NULL,
        [UserId]       NVARCHAR(450)  NOT NULL,
        [DeviceName]   NVARCHAR(200)  NOT NULL DEFAULT ('Web Browser'),
        [ClientIp]     NVARCHAR(50)   NULL,
        [UserAgent]    NVARCHAR(500)  NULL,
        [IsRevoked]    BIT            NOT NULL DEFAULT (0),
        [CreatedAt]    DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        [LastActiveAt] DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        [ExpiresAt]    DATETIME2(7)   NOT NULL DEFAULT (DATEADD(day, 30, SYSUTCDATETIME())),
        CONSTRAINT [PK_UserSessions_CYH] PRIMARY KEY ([Id] ASC),
        CONSTRAINT [FK_UserSessions_Users_CYH] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users_CYH]([Id]) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX [IX_UserSessions_UserId] ON [dbo].[UserSessions_CYH]([UserId], [IsRevoked]);
END
GO

-- ============================================================
-- SP: ManageUser_CYH (Updated with all Phase 1 flags)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.ManageUser_CYH
    @Flag            NVARCHAR(50),
    @UserId          NVARCHAR(450) = NULL,
    @Email           NVARCHAR(256) = NULL,
    @FullName        NVARCHAR(200) = NULL,
    @Password        NVARCHAR(MAX) = NULL,
    @NewPassword     NVARCHAR(MAX) = NULL,
    @ResetToken      NVARCHAR(MAX) = NULL,
    @TotpSecret      NVARCHAR(MAX) = NULL,
    @PhoneNumber     NVARCHAR(50)  = NULL,
    @AvatarUrl       NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- REGISTER
    IF @Flag = 'REGISTER'
    BEGIN
        IF EXISTS (SELECT 1 FROM Users_CYH WHERE NormalizedEmail = UPPER(@Email))
        BEGIN SELECT 0 AS Result; RETURN; END

        INSERT INTO Users_CYH (Id, Email, NormalizedEmail, FullName, PasswordHash, Role, EmailConfirmed, IsActive, CreatedAt, UpdatedAt)
        VALUES (@UserId, @Email, UPPER(@Email), @FullName, @Password, 'User', 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
        SELECT 1 AS Result; RETURN;
    END

    -- GET_BY_ID
    IF @Flag = 'GET_BY_ID'
    BEGIN
        SELECT TOP 1 Id AS UserId, Email, FullName, Role, IsActive, EmailConfirmed,
                     TwoFactorEnabled, CreatedAt, LastLoginAt, PhoneNumber, AvatarUrl
        FROM Users_CYH WHERE Id = @UserId;
        RETURN;
    END

    -- GET_BY_EMAIL
    IF @Flag = 'GET_BY_EMAIL'
    BEGIN
        SELECT TOP 1 Id AS UserId, Email, FullName, Role, IsActive, EmailConfirmed,
                     TwoFactorEnabled, CreatedAt, LastLoginAt, PhoneNumber, AvatarUrl
        FROM Users_CYH WHERE NormalizedEmail = UPPER(@Email);
        RETURN;
    END

    -- CHANGE_PASSWORD
    IF @Flag = 'CHANGE_PASSWORD'
    BEGIN
        -- Return current hash for BCrypt.Verify in C#
        SELECT PasswordHash FROM Users_CYH WHERE Id = @UserId AND IsActive = 1;
        RETURN;
    END

    -- DO_CHANGE_PASSWORD (after BCrypt verified in C#)
    IF @Flag = 'DO_CHANGE_PASSWORD'
    BEGIN
        UPDATE Users_CYH SET PasswordHash = @NewPassword, UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @UserId;
        SELECT @@ROWCOUNT AS Result; RETURN;
    END

    -- FORGOT_PASSWORD_REQUEST
    IF @Flag = 'FORGOT_PASSWORD_REQUEST'
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Users_CYH WHERE NormalizedEmail = UPPER(@Email) AND IsActive = 1)
        BEGIN SELECT 0 AS Result; RETURN; END

        UPDATE Users_CYH
        SET PasswordResetToken = @ResetToken,
            PasswordResetExpiry = DATEADD(minute, 30, SYSUTCDATETIME()),
            UpdatedAt = SYSUTCDATETIME()
        WHERE NormalizedEmail = UPPER(@Email);
        SELECT 1 AS Result; RETURN;
    END

    -- FORGOT_PASSWORD_RESET
    IF @Flag = 'FORGOT_PASSWORD_RESET'
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Users_CYH
                       WHERE PasswordResetToken = @ResetToken
                         AND PasswordResetExpiry > SYSUTCDATETIME()
                         AND IsActive = 1)
        BEGIN SELECT 0 AS Result; RETURN; END

        UPDATE Users_CYH
        SET PasswordHash = @NewPassword,
            PasswordResetToken = NULL,
            PasswordResetExpiry = NULL,
            UpdatedAt = SYSUTCDATETIME()
        WHERE PasswordResetToken = @ResetToken;
        SELECT 1 AS Result; RETURN;
    END

    -- ENABLE_MFA
    IF @Flag = 'ENABLE_MFA'
    BEGIN
        UPDATE Users_CYH
        SET TotpSecret = @TotpSecret, TwoFactorEnabled = 1, UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @UserId;
        SELECT @@ROWCOUNT AS Result; RETURN;
    END

    -- DISABLE_MFA
    IF @Flag = 'DISABLE_MFA'
    BEGIN
        UPDATE Users_CYH
        SET TotpSecret = NULL, TwoFactorEnabled = 0, UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @UserId;
        SELECT @@ROWCOUNT AS Result; RETURN;
    END

    -- GET_TOTP_SECRET
    IF @Flag = 'GET_TOTP_SECRET'
    BEGIN
        SELECT TotpSecret, TwoFactorEnabled FROM Users_CYH WHERE Id = @UserId;
        RETURN;
    END

    -- UPDATE_PROFILE
    IF @Flag = 'UPDATE_PROFILE'
    BEGIN
        UPDATE Users_CYH
        SET FullName = ISNULL(@FullName, FullName),
            PhoneNumber = ISNULL(@PhoneNumber, PhoneNumber),
            AvatarUrl = ISNULL(@AvatarUrl, AvatarUrl),
            UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @UserId;
        SELECT @@ROWCOUNT AS Result; RETURN;
    END
END
GO

-- ============================================================
-- SP: AuthenticateUser_CYH (Updated with Lockout)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.AuthenticateUser_CYH
    @Flag     NVARCHAR(50),
    @Email    NVARCHAR(256) = NULL,
    @Password NVARCHAR(MAX) = NULL,
    @ClientIp NVARCHAR(50)  = NULL,
    @UserAgent NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Flag = 'LOGIN'
    BEGIN
        -- Check lockout first
        IF EXISTS (SELECT 1 FROM Users_CYH
                   WHERE NormalizedEmail = UPPER(@Email)
                     AND LockoutEnabled = 1
                     AND LockoutEnd IS NOT NULL
                     AND LockoutEnd > SYSUTCDATETIME())
        BEGIN
            SELECT 'LOCKED' AS Status, NULL AS UserId, NULL AS Email, NULL AS FullName,
                   NULL AS Role, NULL AS IsActive, NULL AS EmailConfirmed, NULL AS TwoFactorEnabled,
                   NULL AS CreatedAt, NULL AS LastLoginAt, NULL AS PasswordHash;
            RETURN;
        END

        -- Return password hash + user info for BCrypt.Verify in C#
        SELECT TOP 1
            u.Id         AS UserId,
            u.Email      AS Email,
            u.FullName   AS FullName,
            u.Role       AS Role,
            u.IsActive   AS IsActive,
            u.EmailConfirmed,
            u.TwoFactorEnabled,
            u.CreatedAt,
            u.LastLoginAt,
            u.PasswordHash,
            u.AccessFailedCount,
            'OK' AS Status
        FROM Users_CYH u
        WHERE NormalizedEmail = UPPER(@Email)
          AND IsActive = 1;
        RETURN;
    END

    IF @Flag = 'LOGIN_SUCCESS'
    BEGIN
        DECLARE @SessionId NVARCHAR(450) = NEWID();

        UPDATE Users_CYH
        SET LastLoginAt = SYSUTCDATETIME(),
            LastLoginIp = @ClientIp,
            AccessFailedCount = 0,
            LockoutEnd = NULL,
            UpdatedAt = SYSUTCDATETIME()
        WHERE NormalizedEmail = UPPER(@Email);

        -- Create session
        DECLARE @Uid NVARCHAR(450);
        SELECT @Uid = Id FROM Users_CYH WHERE NormalizedEmail = UPPER(@Email);

        INSERT INTO UserSessions_CYH (Id, UserId, ClientIp, UserAgent, CreatedAt, LastActiveAt, ExpiresAt)
        VALUES (@SessionId, @Uid, @ClientIp, @UserAgent, SYSUTCDATETIME(), SYSUTCDATETIME(), DATEADD(day, 30, SYSUTCDATETIME()));

        SELECT @SessionId AS SessionId;
        RETURN;
    END

    IF @Flag = 'LOGIN_FAILED'
    BEGIN
        DECLARE @FailCount INT;
        SELECT @FailCount = ISNULL(AccessFailedCount, 0) + 1 FROM Users_CYH WHERE NormalizedEmail = UPPER(@Email);

        IF @FailCount >= 5
            UPDATE Users_CYH
            SET AccessFailedCount = @FailCount,
                LockoutEnd = DATEADD(minute, 15, SYSUTCDATETIME()),
                UpdatedAt = SYSUTCDATETIME()
            WHERE NormalizedEmail = UPPER(@Email);
        ELSE
            UPDATE Users_CYH
            SET AccessFailedCount = @FailCount,
                UpdatedAt = SYSUTCDATETIME()
            WHERE NormalizedEmail = UPPER(@Email);

        SELECT @FailCount AS FailCount;
        RETURN;
    END
END
GO

-- ============================================================
-- SP: ManageSession_CYH (Device Management)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.ManageSession_CYH
    @Flag      NVARCHAR(50),
    @UserId    NVARCHAR(450) = NULL,
    @SessionId NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- LIST active sessions
    IF @Flag = 'LIST'
    BEGIN
        SELECT Id AS SessionId, DeviceName, ClientIp, UserAgent,
               CreatedAt, LastActiveAt, ExpiresAt, IsRevoked
        FROM UserSessions_CYH
        WHERE UserId = @UserId
          AND IsRevoked = 0
          AND ExpiresAt > SYSUTCDATETIME()
        ORDER BY LastActiveAt DESC;
        RETURN;
    END

    -- REVOKE single session
    IF @Flag = 'REVOKE'
    BEGIN
        UPDATE UserSessions_CYH SET IsRevoked = 1
        WHERE Id = @SessionId AND UserId = @UserId;
        SELECT @@ROWCOUNT AS Result; RETURN;
    END

    -- REVOKE_ALL except current
    IF @Flag = 'REVOKE_ALL'
    BEGIN
        UPDATE UserSessions_CYH SET IsRevoked = 1
        WHERE UserId = @UserId AND Id != @SessionId;
        SELECT @@ROWCOUNT AS Result; RETURN;
    END

    -- UPDATE last active
    IF @Flag = 'PING'
    BEGIN
        UPDATE UserSessions_CYH SET LastActiveAt = SYSUTCDATETIME()
        WHERE Id = @SessionId;
        RETURN;
    END
END
GO

-- Update existing admin user to BCrypt hash (Admin@123)
-- BCrypt hash of 'Admin@123' with work factor 11
UPDATE Users_CYH
SET PasswordHash = '$2a$11$rGZjKZkYFzV8K9kXxsQyaOjPQCPTCNVyJbQMX4PbJQRdXm4QgXOvW'
WHERE NormalizedEmail = 'ADMIN@CYHLOCKER.COM'
  AND PasswordHash = 'Admin@123';
GO
