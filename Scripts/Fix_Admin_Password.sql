USE [CYHLockerDB];
GO

-- Real BCrypt hash of 'Admin@123'
UPDATE [dbo].[Users_CYH]
SET [PasswordHash] = '$2a$11$ri2uj2iuKX/lKr64POvBWeatPa1Wo2aUivajJBnpBJ.wU5g5pPfaW',
    [AccessFailedCount] = 0,
    [LockoutEnd] = NULL,
    [IsActive] = 1,
    [UpdatedAt] = SYSUTCDATETIME()
WHERE [NormalizedEmail] = 'ADMIN@CYHLOCKER.COM';
GO

SELECT [Email], [NormalizedEmail], [PasswordHash], [AccessFailedCount], [LockoutEnd], [IsActive]
FROM [dbo].[Users_CYH]
WHERE [NormalizedEmail] = 'ADMIN@CYHLOCKER.COM';
GO
