USE [CYHLockerDB];
GO

-- ============================================================
-- Seed Standard Test User ('user@cyhlocker.com' / 'User@123')
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users_CYH] WHERE [NormalizedEmail] = 'USER@CYHLOCKER.COM')
BEGIN
    INSERT INTO [dbo].[Users_CYH] (
        [Id],
        [Email],
        [NormalizedEmail],
        [FullName],
        [PasswordHash],
        [Role],
        [IsActive],
        [EmailConfirmed],
        [TwoFactorEnabled],
        [AccessFailedCount],
        [LockoutEnabled],
        [CreatedAt],
        [UpdatedAt]
    )
    VALUES (
        NEWID(),
        'user@cyhlocker.com',
        'USER@CYHLOCKER.COM',
        'CYH User',
        '$2a$11$ri2uj2iuKX/lKr64POvBWeatPa1Wo2aUivajJBnpBJ.wU5g5pPfaW', -- BCrypt hash of User@123 / Admin@123
        'User',
        1,
        1,
        0,
        0,
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END
ELSE
BEGIN
    UPDATE [dbo].[Users_CYH]
    SET [Role] = 'User',
        [IsActive] = 1,
        [AccessFailedCount] = 0,
        [LockoutEnd] = NULL
    WHERE [NormalizedEmail] = 'USER@CYHLOCKER.COM';
END
GO

SELECT [Email], [FullName], [Role], [IsActive], [CreatedAt]
FROM [dbo].[Users_CYH]
WHERE [NormalizedEmail] IN ('ADMIN@CYHLOCKER.COM', 'USER@CYHLOCKER.COM');
GO
