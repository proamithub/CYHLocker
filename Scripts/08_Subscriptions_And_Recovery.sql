USE [CYHLockerDB];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 1. SubscriptionPlans_CYH Table
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SubscriptionPlans_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[SubscriptionPlans_CYH] (
        [Id]              NVARCHAR(450)   NOT NULL,
        [PlanCode]        NVARCHAR(50)    NOT NULL,
        [Name]            NVARCHAR(100)   NOT NULL,
        [Description]     NVARCHAR(255)   NULL,
        [PriceINR]        DECIMAL(18, 2)  NOT NULL DEFAULT (0.00),
        [BillingCycle]    NVARCHAR(20)    NOT NULL DEFAULT ('MONTHLY'), -- FREE, MONTHLY, ANNUAL
        [MaxPasswords]    INT             NOT NULL DEFAULT (15),
        [MaxCards]        INT             NOT NULL DEFAULT (2),
        [MaxDocuments]    INT             NOT NULL DEFAULT (1),
        [CanSync]         BIT             NOT NULL DEFAULT (0),
        [RazorpayPlanId]  NVARCHAR(100)   NULL,
        [IsActive]        BIT             NOT NULL DEFAULT (1),
        [CreatedAt]       DATETIME2(7)    NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_SubscriptionPlans_CYH] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_SubscriptionPlans_Code] UNIQUE ([PlanCode] ASC)
    );
END
GO

-- Seed Default Plans if empty
IF NOT EXISTS (SELECT 1 FROM [dbo].[SubscriptionPlans_CYH] WHERE [PlanCode] = 'FREE')
BEGIN
    INSERT INTO [dbo].[SubscriptionPlans_CYH] 
    (Id, PlanCode, Name, Description, PriceINR, BillingCycle, MaxPasswords, MaxCards, MaxDocuments, CanSync, IsActive)
    VALUES 
    ('PLAN_FREE', 'FREE', 'Free Starter Vault', 'Essential personal password manager for individual use', 0.00, 'FREE', 15, 2, 1, 0, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SubscriptionPlans_CYH] WHERE [PlanCode] = 'PRO_MONTHLY')
BEGIN
    INSERT INTO [dbo].[SubscriptionPlans_CYH] 
    (Id, PlanCode, Name, Description, PriceINR, BillingCycle, MaxPasswords, MaxCards, MaxDocuments, CanSync, IsActive)
    VALUES 
    ('PLAN_PRO_M', 'PRO_MONTHLY', 'Pro Vault (Monthly)', 'Unlimited items, encrypted cards & documents, priority cloud sync', 199.00, 'MONTHLY', 999999, 999999, 999999, 1, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SubscriptionPlans_CYH] WHERE [PlanCode] = 'PRO_ANNUAL')
BEGIN
    INSERT INTO [dbo].[SubscriptionPlans_CYH] 
    (Id, PlanCode, Name, Description, PriceINR, BillingCycle, MaxPasswords, MaxCards, MaxDocuments, CanSync, IsActive)
    VALUES 
    ('PLAN_PRO_Y', 'PRO_ANNUAL', 'Pro Vault (Annual)', 'Full Pro suite at 37% discount + annual priority backup kit', 1499.00, 'ANNUAL', 999999, 999999, 999999, 1, 1);
END
GO

-- ============================================================
-- 2. UserSubscriptions_CYH Table
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserSubscriptions_CYH]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[UserSubscriptions_CYH] (
        [Id]                    NVARCHAR(450)  NOT NULL,
        [UserId]                NVARCHAR(450)  NOT NULL,
        [PlanId]                NVARCHAR(450)  NOT NULL,
        [RazorpayOrderId]       NVARCHAR(150)  NULL,
        [RazorpayPaymentId]     NVARCHAR(150)  NULL,
        [RazorpaySignature]     NVARCHAR(255)  NULL,
        [Status]                NVARCHAR(50)   NOT NULL DEFAULT ('Active'), -- Active, Expired, Cancelled
        [StartDate]             DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        [EndDate]               DATETIME2(7)   NULL,
        [CreatedAt]             DATETIME2(7)   NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_UserSubscriptions_CYH] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_UserSubscriptions_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users_CYH]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserSubscriptions_Plans] FOREIGN KEY ([PlanId]) REFERENCES [dbo].[SubscriptionPlans_CYH]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_UserSubscriptions_User_Status] ON [dbo].[UserSubscriptions_CYH]([UserId], [Status]);
END
GO

-- ============================================================
-- 3. Stored Procedure: ManageSubscriptions_CYH
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.ManageSubscriptions_CYH
    @Flag                   NVARCHAR(50),
    @UserId                 NVARCHAR(450)  = NULL,
    @PlanId                 NVARCHAR(450)  = NULL,
    @PlanCode               NVARCHAR(50)   = NULL,
    @RazorpayOrderId        NVARCHAR(150)  = NULL,
    @RazorpayPaymentId      NVARCHAR(150)  = NULL,
    @RazorpaySignature      NVARCHAR(255)  = NULL,
    @ItemType               NVARCHAR(50)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 1. GET ALL ACTIVE PLANS ────────────────────────────
    IF @Flag = 'GET_PLANS'
    BEGIN
        SELECT Id, PlanCode, Name, Description, PriceINR, BillingCycle, MaxPasswords, MaxCards, MaxDocuments, CanSync, IsActive
        FROM [dbo].[SubscriptionPlans_CYH]
        WHERE IsActive = 1
        ORDER BY PriceINR ASC;
        RETURN;
    END

    -- ── 2. GET CURRENT USER PLAN & QUOTA USAGE ─────────────
    IF @Flag = 'GET_USER_PLAN'
    BEGIN
        -- Get active plan (fallback to FREE if none exists)
        DECLARE @ActivePlanId NVARCHAR(450);
        DECLARE @EndDate DATETIME2(7);
        DECLARE @SubStatus NVARCHAR(50);

        SELECT TOP 1 
            @ActivePlanId = s.PlanId, 
            @EndDate = s.EndDate,
            @SubStatus = s.Status
        FROM [dbo].[UserSubscriptions_CYH] s
        WHERE s.UserId = @UserId AND s.Status = 'Active' AND (s.EndDate IS NULL OR s.EndDate > SYSUTCDATETIME())
        ORDER BY s.StartDate DESC;

        IF @ActivePlanId IS NULL
        BEGIN
            SET @ActivePlanId = 'PLAN_FREE';
            SET @SubStatus = 'Active';
        END

        SELECT 
            p.Id AS PlanId,
            p.PlanCode,
            p.Name AS PlanName,
            p.PriceINR,
            p.BillingCycle,
            p.MaxPasswords,
            p.MaxCards,
            p.MaxDocuments,
            p.CanSync,
            @SubStatus AS SubscriptionStatus,
            @EndDate AS ExpirationDate,
            (SELECT COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE UserId = @UserId AND IsDeleted = 0 AND ItemType IN ('Login', 'Social')) AS CurrentPasswords,
            (SELECT COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE UserId = @UserId AND IsDeleted = 0 AND ItemType = 'Card') AS CurrentCards,
            (SELECT COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE UserId = @UserId AND IsDeleted = 0 AND ItemType = 'Document') AS CurrentDocuments,
            (SELECT COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE UserId = @UserId AND IsDeleted = 0 AND ItemType = 'ApiKey') AS CurrentApiKeys
        FROM [dbo].[SubscriptionPlans_CYH] p
        WHERE p.Id = @ActivePlanId;
        RETURN;
    END

    -- ── 3. ASSIGN / ACTIVATE PLAN AFTER PAYMENT ─────────────
    IF @Flag = 'ASSIGN_PLAN'
    BEGIN
        -- Find target plan
        DECLARE @TargetPlanId NVARCHAR(450);
        DECLARE @Cycle NVARCHAR(20);
        
        SELECT TOP 1 @TargetPlanId = Id, @Cycle = BillingCycle 
        FROM [dbo].[SubscriptionPlans_CYH] 
        WHERE Id = @PlanId OR PlanCode = @PlanCode;

        IF @TargetPlanId IS NULL
        BEGIN
            SELECT 0 AS Success, 'Invalid Plan' AS Message;
            RETURN;
        END

        -- Determine expiration
        DECLARE @NewEnd DATETIME2(7) = NULL;
        IF @Cycle = 'MONTHLY' SET @NewEnd = DATEADD(month, 1, SYSUTCDATETIME());
        IF @Cycle = 'ANNUAL'  SET @NewEnd = DATEADD(year, 1, SYSUTCDATETIME());

        -- Expire any existing subscriptions
        UPDATE [dbo].[UserSubscriptions_CYH]
        SET Status = 'Replaced'
        WHERE UserId = @UserId AND Status = 'Active';

        -- Insert new active subscription
        DECLARE @NewSubId NVARCHAR(450) = NEWID();
        INSERT INTO [dbo].[UserSubscriptions_CYH] (
            Id, UserId, PlanId, RazorpayOrderId, RazorpayPaymentId, RazorpaySignature, Status, StartDate, EndDate, CreatedAt
        )
        VALUES (
            @NewSubId, @UserId, @TargetPlanId, @RazorpayOrderId, @RazorpayPaymentId, @RazorpaySignature, 'Active', SYSUTCDATETIME(), @NewEnd, SYSUTCDATETIME()
        );

        SELECT 1 AS Success, 'Subscription Activated' AS Message, @NewSubId AS SubscriptionId;
        RETURN;
    END

    -- ── 4. CHECK QUOTA BEFORE ITEM CREATION ────────────────
    IF @Flag = 'CHECK_QUOTA'
    BEGIN
        DECLARE @UserPlanId NVARCHAR(450);
        SELECT TOP 1 @UserPlanId = PlanId 
        FROM [dbo].[UserSubscriptions_CYH] 
        WHERE UserId = @UserId AND Status = 'Active' AND (EndDate IS NULL OR EndDate > SYSUTCDATETIME())
        ORDER BY StartDate DESC;

        IF @UserPlanId IS NULL SET @UserPlanId = 'PLAN_FREE';

        DECLARE @MaxPass INT, @MaxCard INT, @MaxDoc INT;
        SELECT @MaxPass = MaxPasswords, @MaxCard = MaxCards, @MaxDoc = MaxDocuments
        FROM [dbo].[SubscriptionPlans_CYH] WHERE Id = @UserPlanId;

        DECLARE @Allowed BIT = 1;
        DECLARE @CurrentCount INT = 0;
        DECLARE @MaxAllowed INT = 999999;

        IF @ItemType IN ('Login', 'Social')
        BEGIN
            SELECT @CurrentCount = COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE UserId = @UserId AND IsDeleted = 0 AND ItemType IN ('Login', 'Social');
            SET @MaxAllowed = @MaxPass;
            IF @CurrentCount >= @MaxPass SET @Allowed = 0;
        END
        ELSE IF @ItemType = 'Card'
        BEGIN
            SELECT @CurrentCount = COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE UserId = @UserId AND IsDeleted = 0 AND ItemType = 'Card';
            SET @MaxAllowed = @MaxCard;
            IF @CurrentCount >= @MaxCard SET @Allowed = 0;
        END
        ELSE IF @ItemType = 'Document'
        BEGIN
            SELECT @CurrentCount = COUNT(1) FROM [dbo].[VaultItems_CYH] WHERE UserId = @UserId AND IsDeleted = 0 AND ItemType = 'Document';
            SET @MaxAllowed = @MaxDoc;
            IF @CurrentCount >= @MaxDoc SET @Allowed = 0;
        END

        SELECT @Allowed AS IsAllowed, @CurrentCount AS CurrentCount, @MaxAllowed AS MaxLimit;
        RETURN;
    END
END
GO

-- ============================================================
-- 4. Stored Procedure: ManageAdminPlans_CYH (Admin Config)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.ManageAdminPlans_CYH
    @Flag            NVARCHAR(50),
    @PlanId          NVARCHAR(450),
    @PriceINR        DECIMAL(18,2)  = NULL,
    @MaxPasswords    INT            = NULL,
    @MaxCards        INT            = NULL,
    @MaxDocuments    INT            = NULL,
    @CanSync         BIT            = NULL,
    @IsActive        BIT            = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Flag = 'UPDATE_PLAN'
    BEGIN
        UPDATE [dbo].[SubscriptionPlans_CYH]
        SET PriceINR     = ISNULL(@PriceINR, PriceINR),
            MaxPasswords = ISNULL(@MaxPasswords, MaxPasswords),
            MaxCards     = ISNULL(@MaxCards, MaxCards),
            MaxDocuments = ISNULL(@MaxDocuments, MaxDocuments),
            CanSync      = ISNULL(@CanSync, CanSync),
            IsActive     = ISNULL(@IsActive, IsActive)
        WHERE Id = @PlanId;

        SELECT @@ROWCOUNT AS RowsAffected;
        RETURN;
    END
END
GO
