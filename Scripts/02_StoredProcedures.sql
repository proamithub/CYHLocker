USE [CYHLockerDB];
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO

-- SP 1: ManageUser_CYH  (REGISTER, GET_BY_ID, GET_BY_EMAIL, CHANGE_PASSWORD)
CREATE OR ALTER PROCEDURE dbo.ManageUser_CYH
    @Flag          NVARCHAR(50),
    @UserId        NVARCHAR(450) = NULL,
    @Email         NVARCHAR(256) = NULL,
    @FullName      NVARCHAR(200) = NULL,
    @Password      NVARCHAR(MAX) = NULL,
    @NewPassword   NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Flag = 'REGISTER'
    BEGIN
        IF EXISTS (SELECT 1 FROM Users_CYH WHERE NormalizedEmail = UPPER(@Email))
        BEGIN SELECT 0 AS Result; RETURN; END

        INSERT INTO Users_CYH (Id, Email, NormalizedEmail, FullName, PasswordHash, CreatedAt, UpdatedAt)
        VALUES (@UserId, @Email, UPPER(@Email), @FullName, @Password, SYSUTCDATETIME(), SYSUTCDATETIME());
        SELECT 1 AS Result; RETURN;
    END

    IF @Flag = 'GET_BY_ID'
    BEGIN
        SELECT TOP 1 Id, Email, FullName, Role, IsActive, EmailConfirmed, TwoFactorEnabled, CreatedAt, LastLoginAt
        FROM Users_CYH WHERE Id = @UserId;
        RETURN;
    END

    IF @Flag = 'GET_BY_EMAIL'
    BEGIN
        SELECT TOP 1 Id, Email, FullName, Role, IsActive, EmailConfirmed, TwoFactorEnabled, CreatedAt, LastLoginAt
        FROM Users_CYH WHERE NormalizedEmail = UPPER(@Email);
        RETURN;
    END

    IF @Flag = 'CHANGE_PASSWORD'
    BEGIN
        IF EXISTS (SELECT 1 FROM Users_CYH WHERE Id = @UserId AND PasswordHash = @Password AND IsActive = 1)
        BEGIN
            UPDATE Users_CYH SET PasswordHash = @NewPassword, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @UserId;
            SELECT 1 AS Result; RETURN;
        END
        SELECT 0 AS Result; RETURN;
    END
END
GO

-- SP 2: AuthenticateUser_CYH (LOGIN)
CREATE OR ALTER PROCEDURE dbo.AuthenticateUser_CYH
    @Flag      NVARCHAR(50),
    @Email     NVARCHAR(256) = NULL,
    @Password  NVARCHAR(MAX) = NULL,
    @ClientIp  NVARCHAR(50)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Flag = 'LOGIN'
    BEGIN
        DECLARE @UserId NVARCHAR(450);

        SELECT @UserId = Id
        FROM Users_CYH
        WHERE NormalizedEmail = UPPER(@Email)
          AND PasswordHash = @Password
          AND IsActive = 1
          AND (LockoutEnd IS NULL OR LockoutEnd < SYSUTCDATETIME());

        IF @UserId IS NOT NULL
        BEGIN
            UPDATE Users_CYH
            SET LastLoginAt = SYSUTCDATETIME(), LastLoginIp = @ClientIp, AccessFailedCount = 0
            WHERE Id = @UserId;

            SELECT
                u.Id         AS UserId,
                u.Email      AS Email,
                u.FullName   AS FullName,
                u.Role       AS Role,
                u.IsActive   AS IsActive,
                u.EmailConfirmed,
                u.TwoFactorEnabled,
                u.CreatedAt,
                u.LastLoginAt
            FROM Users_CYH u WHERE u.Id = @UserId;
        END
    END
END
GO

-- SP 3: Seed Admin User
IF NOT EXISTS (SELECT 1 FROM Users_CYH WHERE NormalizedEmail = 'ADMIN@CYHLOCKER.COM')
BEGIN
    INSERT INTO Users_CYH (Id, Email, NormalizedEmail, FullName, PasswordHash, Role, EmailConfirmed, IsActive, CreatedAt, UpdatedAt)
    VALUES (NEWID(), 'admin@cyhlocker.com', 'ADMIN@CYHLOCKER.COM', 'CYH Admin', 'Admin@123', 'Admin', 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
END
GO
