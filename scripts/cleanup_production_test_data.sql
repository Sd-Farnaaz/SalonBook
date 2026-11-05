-- ==============================================================================
-- SalonBook Production Database (db77856) Test-Data Cleanup Script
-- Target Database: db77856 on MonsterASP (SQL Server)
-- Mode: DRY-RUN BY DEFAULT (Rolls back automatically unless switch is changed)
-- Safety: Enforces positive test-ID sets, pre-checks, post-checks, and rollback
-- ZERO BLANKET 'NOT IN' DELETIONS: Only explicitly confirmed test IDs are deleted.
-- ==============================================================================

-- !!! SAFETY SWITCH !!!
-- 0 = DRY RUN ONLY (Performs all deletions inside transaction, validates counts, and ROLLS BACK)
-- 1 = ACTUAL EXECUTE (Commits deletions ONLY if all pre-checks, counts, and post-checks pass)
DECLARE @ExecuteActualDelete BIT = 0;

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '==================================================================';
PRINT 'SALONBOOK PRODUCTION TEST-DATA CLEANUP SCRIPT';
PRINT 'Target Database: db77856';
PRINT 'Execution Mode: ' + CASE WHEN @ExecuteActualDelete = 1 THEN '>>> ACTUAL COMMIT <<<' ELSE '>>> DRY RUN (AUTO ROLLBACK) <<<' END;
PRINT 'Timestamp (UTC): ' + CONVERT(VARCHAR(30), GETUTCDATE(), 120);
PRINT '==================================================================';

BEGIN TRANSACTION;

BEGIN TRY
    -- --------------------------------------------------------------------------
    -- STEP 1: POPULATE EXPLICIT AUDITED TEST-RECORD ID SETS
    -- --------------------------------------------------------------------------
    IF OBJECT_ID('tempdb..#ConfirmedTestSalons') IS NOT NULL DROP TABLE #ConfirmedTestSalons;
    CREATE TABLE #ConfirmedTestSalons (SalonId INT PRIMARY KEY);

    IF OBJECT_ID('tempdb..#ConfirmedTestUsers') IS NOT NULL DROP TABLE #ConfirmedTestUsers;
    CREATE TABLE #ConfirmedTestUsers (UserId INT PRIMARY KEY);

    -- Confirmed Audited Test Salons: IDs 2-172 (171 salons) and IDs 174-258 (85 salons) = exactly 256 salons
    INSERT INTO #ConfirmedTestSalons (SalonId)
    SELECT Id FROM [Salons] WHERE (Id BETWEEN 2 AND 172) OR (Id BETWEEN 174 AND 258);

    -- Confirmed Audited Test Users: IDs 3-297 (295 users) and IDs 301-449 (149 users) = exactly 444 users
    INSERT INTO #ConfirmedTestUsers (UserId)
    SELECT Id FROM [Users] WHERE (Id BETWEEN 3 AND 297) OR (Id BETWEEN 301 AND 449);

    -- --------------------------------------------------------------------------
    -- STEP 2: PRE-EXECUTION EXACT IDENTITY & UNKNOWN RECORDS ASSERTIONS
    -- --------------------------------------------------------------------------
    PRINT 'Step 1: Running pre-execution safety assertions...';

    -- 2.1 Assert Exact User Identities
    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Id = 1 AND Email = 'superadmin@salonbook.com' AND Role = 'SuperAdmin' AND SalonId IS NULL)
    BEGIN
        RAISERROR('PRE-CHECK FAILED: User 1 identity mismatch (expected superadmin@salonbook.com, SuperAdmin, SalonId NULL)!', 16, 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Id = 2 AND Email = 'rozi@gmail.com' AND Role = 'Admin' AND SalonId = 1)
    BEGIN
        RAISERROR('PRE-CHECK FAILED: User 2 identity mismatch (expected rozi@gmail.com, Admin, SalonId 1)!', 16, 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Id = 298 AND Email = 'minha@gmail.com' AND Role = 'Admin' AND SalonId = 173)
    BEGIN
        RAISERROR('PRE-CHECK FAILED: User 298 identity mismatch (expected minha@gmail.com, Admin, SalonId 173)!', 16, 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Id = 299 AND Email = 'zavi@gmail.com' AND Role = 'User' AND SalonId IS NULL)
    BEGIN
        RAISERROR('PRE-CHECK FAILED: User 299 identity mismatch (expected zavi@gmail.com, User, SalonId NULL)!', 16, 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM [Users] WHERE Id = 300 AND Email = 'rumanakhan@gmail.com' AND Role = 'User' AND SalonId IS NULL)
    BEGIN
        RAISERROR('PRE-CHECK FAILED: User 300 identity mismatch (expected rumanakhan@gmail.com, User, SalonId NULL)!', 16, 1);
    END;

    -- 2.2 Assert Exact Salon Identities
    IF NOT EXISTS (SELECT 1 FROM [Salons] WHERE Id = 1 AND Name = 'Rozi''s Salon' AND Slug = 'rozis-salon')
    BEGIN
        RAISERROR('PRE-CHECK FAILED: Salon 1 identity mismatch (expected Rozi''s Salon, rozis-salon)!', 16, 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM [Salons] WHERE Id = 173 AND Name = 'Minha''s salon' AND Slug = 'minhas-salon')
    BEGIN
        RAISERROR('PRE-CHECK FAILED: Salon 173 identity mismatch (expected Minha''s salon, minhas-salon)!', 16, 1);
    END;

    -- 2.3 Assert Exact Service Identities & Salon Affiliations
    IF NOT EXISTS (SELECT 1 FROM [Services] WHERE Id = 1 AND SalonId = 1 AND Name = 'Hair cut')
    BEGIN
        RAISERROR('PRE-CHECK FAILED: Service 1 mismatch (expected Hair cut on Salon 1)!', 16, 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM [Services] WHERE Id = 86 AND SalonId = 173 AND Name = 'hair')
    BEGIN
        RAISERROR('PRE-CHECK FAILED: Service 86 mismatch (expected hair on Salon 173)!', 16, 1);
    END;

    -- 2.4 Verify Zero Overlap: Test Sets Must NEVER Contain Genuine IDs
    IF EXISTS (SELECT 1 FROM #ConfirmedTestSalons WHERE SalonId IN (1, 173))
    BEGIN
        RAISERROR('FATAL SAFETY VIOLATION: Test salon set intersects with genuine salons (1, 173)!', 16, 1);
    END;

    IF EXISTS (SELECT 1 FROM #ConfirmedTestUsers WHERE UserId IN (1, 2, 298, 299, 300))
    BEGIN
        RAISERROR('FATAL SAFETY VIOLATION: Test user set intersects with genuine users (1, 2, 298, 299, 300)!', 16, 1);
    END;

    -- 2.5 Verify Unknown Records Protection: Check if any unclassified user or salon exists
    DECLARE @UnknownUsersCount INT, @UnknownSalonsCount INT;
    SELECT @UnknownUsersCount = COUNT(*) FROM [Users] WHERE Id NOT IN (1, 2, 298, 299, 300) AND Id NOT IN (SELECT UserId FROM #ConfirmedTestUsers);
    SELECT @UnknownSalonsCount = COUNT(*) FROM [Salons] WHERE Id NOT IN (1, 173) AND Id NOT IN (SELECT SalonId FROM #ConfirmedTestSalons);

    IF @UnknownUsersCount > 0
    BEGIN
        PRINT '  WARNING: ' + CAST(@UnknownUsersCount AS VARCHAR(10)) + ' unknown user(s) detected. They will be AUTOMATICALLY PRESERVED.';
    END;

    IF @UnknownSalonsCount > 0
    BEGIN
        PRINT '  WARNING: ' + CAST(@UnknownSalonsCount AS VARCHAR(10)) + ' unknown salon(s) detected. They will be AUTOMATICALLY PRESERVED.';
    END;

    -- 2.6 Dynamic Genuine Refresh Token Count Capture
    DECLARE @Before_GenuineTokens INT;
    SELECT @Before_GenuineTokens = COUNT(*) FROM [RefreshTokens] WHERE UserId IN (1, 2, 298, 299, 300);
    IF @Before_GenuineTokens < 24
    BEGIN
        RAISERROR('PRE-CHECK FAILED: Expected at least 24 genuine refresh tokens, found %d!', 16, 1, @Before_GenuineTokens);
    END;

    PRINT '  Pre-checks passed: All genuine identities and dynamic refresh tokens (' + CAST(@Before_GenuineTokens AS VARCHAR(10)) + ') validated.';

    -- --------------------------------------------------------------------------
    -- STEP 3: RECORD BEFORE-COUNTS ACROSS ALL 18 TABLES
    -- --------------------------------------------------------------------------
    DECLARE @Before_Formulas INT, @Before_Reviews INT, @Before_OffPeak INT, @Before_SubTx INT;
    DECLARE @Before_Cards INT, @Before_Reminders INT, @Before_Notifs INT, @Before_Appts INT;
    DECLARE @Before_Breaks INT, @Before_Schedules INT, @Before_Staff INT, @Before_Services INT;
    DECLARE @Before_Payouts INT, @Before_Compliance INT, @Before_ResetTokens INT, @Before_Tokens INT;
    DECLARE @Before_Users INT, @Before_Salons INT;

    SELECT @Before_Formulas = COUNT(*) FROM [ClientFormulaRecords];
    SELECT @Before_Reviews = COUNT(*) FROM [Reviews];
    SELECT @Before_OffPeak = COUNT(*) FROM [SalonOffPeakRules];
    SELECT @Before_SubTx = COUNT(*) FROM [SubscriptionTransactions];
    SELECT @Before_Cards = COUNT(*) FROM [ClientConsultationCards];
    SELECT @Before_Reminders = COUNT(*) FROM [AppointmentReminders];
    SELECT @Before_Notifs = COUNT(*) FROM [Notifications];
    SELECT @Before_Appts = COUNT(*) FROM [Appointments];
    SELECT @Before_Breaks = COUNT(*) FROM [StaffBreaks];
    SELECT @Before_Schedules = COUNT(*) FROM [StaffSchedules];
    SELECT @Before_Staff = COUNT(*) FROM [Staff];
    SELECT @Before_Services = COUNT(*) FROM [Services];
    SELECT @Before_Payouts = COUNT(*) FROM [VendorPayouts];
    SELECT @Before_Compliance = COUNT(*) FROM [VendorComplianceLogs];
    SELECT @Before_ResetTokens = COUNT(*) FROM [PasswordResetTokens];
    SELECT @Before_Tokens = COUNT(*) FROM [RefreshTokens];
    SELECT @Before_Users = COUNT(*) FROM [Users];
    SELECT @Before_Salons = COUNT(*) FROM [Salons];

    -- --------------------------------------------------------------------------
    -- STEP 4: EXECUTE DELETIONS IN STRICT FOREIGN-KEY (LEAF-TO-ROOT) ORDER
    -- (POSITIVE MEMBERSHIP IN #ConfirmedTest... ONLY)
    -- --------------------------------------------------------------------------
    PRINT 'Step 2: Executing deletions in strict foreign-key order using positive test ID sets...';

    DECLARE @Deleted_Formulas INT, @Deleted_Reviews INT, @Deleted_OffPeak INT, @Deleted_SubTx INT;
    DECLARE @Deleted_Cards INT, @Deleted_Reminders INT, @Deleted_Notifs INT, @Deleted_Appts INT;
    DECLARE @Deleted_Breaks INT, @Deleted_Schedules INT, @Deleted_Staff INT, @Deleted_Services INT;
    DECLARE @Deleted_Payouts INT, @Deleted_Compliance INT, @Deleted_ResetTokens INT, @Deleted_Tokens INT;
    DECLARE @Deleted_Users INT, @Deleted_Salons INT;

    -- 4.1 Empty Leaf Tables (Targeting test salons and test users only)
    DELETE FROM [ClientFormulaRecords]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
       OR CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Formulas = @@ROWCOUNT;

    DELETE FROM [Reviews]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
       OR CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Reviews = @@ROWCOUNT;

    DELETE FROM [SalonOffPeakRules]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_OffPeak = @@ROWCOUNT;

    DELETE FROM [SubscriptionTransactions]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_SubTx = @@ROWCOUNT;

    -- 4.2 ClientConsultationCards (Leaf: depends on Salons, Users)
    -- Deletes test cards linked to confirmed test users (Cards 1, 3, 5 on Salon 1, and Cards 2, 4, 6 on test salons)
    DELETE FROM [ClientConsultationCards]
    WHERE CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Cards = @@ROWCOUNT;
    PRINT '  [1/14] ClientConsultationCards deleted: ' + CAST(@Deleted_Cards AS VARCHAR(10));

    -- 4.3 AppointmentReminders (Leaf: depends on Appointments)
    DELETE ar
    FROM [AppointmentReminders] ar
    INNER JOIN [Appointments] a ON ar.AppointmentId = a.Id
    WHERE a.SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
      AND a.CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Reminders = @@ROWCOUNT;
    PRINT '  [2/14] AppointmentReminders deleted:    ' + CAST(@Deleted_Reminders AS VARCHAR(10));

    -- 4.4 Notifications (Leaf: depends on Users)
    DELETE FROM [Notifications]
    WHERE RecipientUserId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Notifs = @@ROWCOUNT;
    PRINT '  [3/14] Notifications deleted:           ' + CAST(@Deleted_Notifs AS VARCHAR(10));

    -- 4.5 Appointments (Depends on Salons, Services, Staff, Users)
    DELETE FROM [Appointments]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
      AND CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Appts = @@ROWCOUNT;
    PRINT '  [4/14] Appointments deleted:            ' + CAST(@Deleted_Appts AS VARCHAR(10));

    -- 4.6 StaffBreaks (Depends on StaffSchedules)
    DELETE sb
    FROM [StaffBreaks] sb
    INNER JOIN [StaffSchedules] ss ON sb.StaffScheduleId = ss.Id
    INNER JOIN [Staff] s ON ss.StaffId = s.Id
    WHERE s.SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_Breaks = @@ROWCOUNT;
    PRINT '  [5/14] StaffBreaks deleted:             ' + CAST(@Deleted_Breaks AS VARCHAR(10));

    -- 4.7 StaffSchedules (Depends on Staff)
    DELETE ss
    FROM [StaffSchedules] ss
    INNER JOIN [Staff] s ON ss.StaffId = s.Id
    WHERE s.SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_Schedules = @@ROWCOUNT;
    PRINT '  [6/14] StaffSchedules deleted:          ' + CAST(@Deleted_Schedules AS VARCHAR(10));

    -- 4.8 Staff (Depends on Salons)
    DELETE FROM [Staff]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_Staff = @@ROWCOUNT;
    PRINT '  [7/14] Staff deleted:                   ' + CAST(@Deleted_Staff AS VARCHAR(10));

    -- 4.9 Services (Depends on Salons)
    DELETE FROM [Services]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
      AND Id NOT IN (1, 86);
    SET @Deleted_Services = @@ROWCOUNT;
    PRINT '  [8/14] Services deleted:                ' + CAST(@Deleted_Services AS VARCHAR(10));

    -- 4.10 VendorPayouts (Depends on Salons)
    DELETE FROM [VendorPayouts]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_Payouts = @@ROWCOUNT;
    PRINT '  [9/14] VendorPayouts deleted:           ' + CAST(@Deleted_Payouts AS VARCHAR(10));

    -- 4.11 VendorComplianceLogs (Depends on Salons)
    DELETE FROM [VendorComplianceLogs]
    WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_Compliance = @@ROWCOUNT;
    PRINT '  [10/14] VendorComplianceLogs deleted:   ' + CAST(@Deleted_Compliance AS VARCHAR(10));

    -- 4.12 PasswordResetTokens (Depends on Users)
    DELETE FROM [PasswordResetTokens]
    WHERE UserId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_ResetTokens = @@ROWCOUNT;
    PRINT '  [11/14] PasswordResetTokens deleted:    ' + CAST(@Deleted_ResetTokens AS VARCHAR(10));

    -- 4.13 RefreshTokens (Depends on Users - Positive test user deletion only)
    DELETE FROM [RefreshTokens]
    WHERE UserId IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Tokens = @@ROWCOUNT;
    PRINT '  [12/14] RefreshTokens deleted:          ' + CAST(@Deleted_Tokens AS VARCHAR(10));

    -- 4.14 Users (Depends on Salons - Positive test user deletion only)
    DELETE FROM [Users]
    WHERE Id IN (SELECT UserId FROM #ConfirmedTestUsers);
    SET @Deleted_Users = @@ROWCOUNT;
    PRINT '  [13/14] Users deleted:                  ' + CAST(@Deleted_Users AS VARCHAR(10));

    -- 4.15 Salons (Root - Positive test salon deletion only)
    DELETE FROM [Salons]
    WHERE Id IN (SELECT SalonId FROM #ConfirmedTestSalons);
    SET @Deleted_Salons = @@ROWCOUNT;
    PRINT '  [14/14] Salons deleted:                 ' + CAST(@Deleted_Salons AS VARCHAR(10));

    -- --------------------------------------------------------------------------
    -- STEP 5: POST-DELETION INTEGRITY ASSERTIONS
    -- --------------------------------------------------------------------------
    PRINT 'Step 3: Running post-deletion safety assertions...';

    DECLARE @After_Formulas INT, @After_Reviews INT, @After_OffPeak INT, @After_SubTx INT;
    DECLARE @After_Cards INT, @After_Reminders INT, @After_Notifs INT, @After_Appts INT;
    DECLARE @After_Breaks INT, @After_Schedules INT, @After_Staff INT, @After_Services INT;
    DECLARE @After_Payouts INT, @After_Compliance INT, @After_ResetTokens INT, @After_Tokens INT;
    DECLARE @After_Users INT, @After_Salons INT;

    SELECT @After_Formulas = COUNT(*) FROM [ClientFormulaRecords];
    SELECT @After_Reviews = COUNT(*) FROM [Reviews];
    SELECT @After_OffPeak = COUNT(*) FROM [SalonOffPeakRules];
    SELECT @After_SubTx = COUNT(*) FROM [SubscriptionTransactions];
    SELECT @After_Cards = COUNT(*) FROM [ClientConsultationCards];
    SELECT @After_Reminders = COUNT(*) FROM [AppointmentReminders];
    SELECT @After_Notifs = COUNT(*) FROM [Notifications];
    SELECT @After_Appts = COUNT(*) FROM [Appointments];
    SELECT @After_Breaks = COUNT(*) FROM [StaffBreaks];
    SELECT @After_Schedules = COUNT(*) FROM [StaffSchedules];
    SELECT @After_Staff = COUNT(*) FROM [Staff];
    SELECT @After_Services = COUNT(*) FROM [Services];
    SELECT @After_Payouts = COUNT(*) FROM [VendorPayouts];
    SELECT @After_Compliance = COUNT(*) FROM [VendorComplianceLogs];
    SELECT @After_ResetTokens = COUNT(*) FROM [PasswordResetTokens];
    SELECT @After_Tokens = COUNT(*) FROM [RefreshTokens];
    SELECT @After_Users = COUNT(*) FROM [Users];
    SELECT @After_Salons = COUNT(*) FROM [Salons];

    -- Verify exact remaining counts (Genuine + Unknown preserved)
    IF @After_Salons <> (2 + @UnknownSalonsCount)
        RAISERROR('POST-CHECK FAILED: Unexpected salons remaining! Expected %d, found %d!', 16, 1, (2 + @UnknownSalonsCount), @After_Salons);
    IF @After_Users <> (5 + @UnknownUsersCount)
        RAISERROR('POST-CHECK FAILED: Unexpected users remaining! Expected %d, found %d!', 16, 1, (5 + @UnknownUsersCount), @After_Users);
    IF @After_Services < 2
        RAISERROR('POST-CHECK FAILED: Genuine services compromised! Found %d services!', 16, 1, @After_Services);
    IF @After_Tokens <> @Before_GenuineTokens
        RAISERROR('POST-CHECK FAILED: Genuine tokens compromised! Expected %d, found %d!', 16, 1, @Before_GenuineTokens, @After_Tokens);
    
    -- Assert that ZERO confirmed test salons or users remain
    IF EXISTS (SELECT 1 FROM [Salons] WHERE Id IN (SELECT SalonId FROM #ConfirmedTestSalons))
        RAISERROR('POST-CHECK FAILED: Confirmed test salons still exist in table!', 16, 1);
    IF EXISTS (SELECT 1 FROM [Users] WHERE Id IN (SELECT UserId FROM #ConfirmedTestUsers))
        RAISERROR('POST-CHECK FAILED: Confirmed test users still exist in table!', 16, 1);
    IF EXISTS (SELECT 1 FROM [RefreshTokens] WHERE UserId IN (SELECT UserId FROM #ConfirmedTestUsers))
        RAISERROR('POST-CHECK FAILED: Confirmed test refresh tokens still exist in table!', 16, 1);

    PRINT '  Post-checks passed: All assertions validated cleanly.';

    -- --------------------------------------------------------------------------
    -- STEP 6: BEFORE / AFTER SUMMARY REPORT ACROSS ALL 18 TABLES
    -- --------------------------------------------------------------------------
    PRINT '';
    PRINT '==================================================================';
    PRINT 'RECONCILIATION SUMMARY REPORT (ALL 18 APPLICATION TABLES)';
    PRINT '==================================================================';
    PRINT 'Table Name                  Before    Deleted    Remaining (Expected)';
    PRINT '------------------------------------------------------------------';
    PRINT 'ClientFormulaRecords      ' + STR(@Before_Formulas, 8)    + STR(@Deleted_Formulas, 11)    + STR(@After_Formulas, 12) + ' (0)';
    PRINT 'Reviews                   ' + STR(@Before_Reviews, 8)     + STR(@Deleted_Reviews, 11)     + STR(@After_Reviews, 12) + ' (0)';
    PRINT 'SalonOffPeakRules         ' + STR(@Before_OffPeak, 8)     + STR(@Deleted_OffPeak, 11)     + STR(@After_OffPeak, 12) + ' (0)';
    PRINT 'SubscriptionTransactions  ' + STR(@Before_SubTx, 8)       + STR(@Deleted_SubTx, 11)       + STR(@After_SubTx, 12) + ' (0)';
    PRINT 'ClientConsultationCards   ' + STR(@Before_Cards, 8)       + STR(@Deleted_Cards, 11)       + STR(@After_Cards, 12) + ' (0)';
    PRINT 'AppointmentReminders      ' + STR(@Before_Reminders, 8)   + STR(@Deleted_Reminders, 11)   + STR(@After_Reminders, 12) + ' (0)';
    PRINT 'Notifications             ' + STR(@Before_Notifs, 8)      + STR(@Deleted_Notifs, 11)      + STR(@After_Notifs, 12) + ' (0)';
    PRINT 'Appointments              ' + STR(@Before_Appts, 8)       + STR(@Deleted_Appts, 11)       + STR(@After_Appts, 12) + ' (0)';
    PRINT 'StaffBreaks               ' + STR(@Before_Breaks, 8)      + STR(@Deleted_Breaks, 11)      + STR(@After_Breaks, 12) + ' (0)';
    PRINT 'StaffSchedules            ' + STR(@Before_Schedules, 8)   + STR(@Deleted_Schedules, 11)   + STR(@After_Schedules, 12) + ' (0)';
    PRINT 'Staff                     ' + STR(@Before_Staff, 8)       + STR(@Deleted_Staff, 11)       + STR(@After_Staff, 12) + ' (0)';
    PRINT 'Services                  ' + STR(@Before_Services, 8)    + STR(@Deleted_Services, 11)    + STR(@After_Services, 12) + ' (2)';
    PRINT 'VendorPayouts             ' + STR(@Before_Payouts, 8)     + STR(@Deleted_Payouts, 11)     + STR(@After_Payouts, 12) + ' (0)';
    PRINT 'VendorComplianceLogs      ' + STR(@Before_Compliance, 8)  + STR(@Deleted_Compliance, 11)  + STR(@After_Compliance, 12) + ' (0)';
    PRINT 'PasswordResetTokens       ' + STR(@Before_ResetTokens, 8) + STR(@Deleted_ResetTokens, 11) + STR(@After_ResetTokens, 12) + ' (0)';
    PRINT 'RefreshTokens             ' + STR(@Before_Tokens, 8)      + STR(@Deleted_Tokens, 11)      + STR(@After_Tokens, 12) + ' (' + CAST(@Before_GenuineTokens AS VARCHAR(10)) + ')';
    PRINT 'Users                     ' + STR(@Before_Users, 8)       + STR(@Deleted_Users, 11)       + STR(@After_Users, 12) + ' (' + CAST(5 + @UnknownUsersCount AS VARCHAR(10)) + ')';
    PRINT 'Salons                    ' + STR(@Before_Salons, 8)      + STR(@Deleted_Salons, 11)      + STR(@After_Salons, 12) + ' (' + CAST(2 + @UnknownSalonsCount AS VARCHAR(10)) + ')';
    PRINT '------------------------------------------------------------------';
    DECLARE @TotalDeleted INT = @Deleted_Formulas + @Deleted_Reviews + @Deleted_OffPeak + @Deleted_SubTx +
                               @Deleted_Cards + @Deleted_Reminders + @Deleted_Notifs + @Deleted_Appts +
                               @Deleted_Breaks + @Deleted_Schedules + @Deleted_Staff + @Deleted_Services +
                               @Deleted_Payouts + @Deleted_Compliance + @Deleted_ResetTokens + @Deleted_Tokens +
                               @Deleted_Users + @Deleted_Salons;
    PRINT 'TOTAL ROWS DELETED:       ' + STR(@TotalDeleted, 19);
    PRINT '==================================================================';

    -- --------------------------------------------------------------------------
    -- STEP 7: CONDITIONAL COMMIT OR ROLLBACK
    -- --------------------------------------------------------------------------
    IF @ExecuteActualDelete = 1
    BEGIN
        COMMIT TRANSACTION;
        PRINT '';
        PRINT '>>> SUCCESS: ACTUAL DELETION COMMITTED. <<<';
        PRINT 'Live production database db77856 has been cleanly pruned.';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT '';
        PRINT '>>> DRY RUN FINISHED: TRANSACTION ROLLED BACK SAFELY. <<<';
        PRINT 'ZERO rows were modified in the live production database.';
        PRINT 'Set @ExecuteActualDelete = 1 when ready to execute for real.';
    END

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    PRINT '';
    PRINT '******************************************************************';
    PRINT 'CRITICAL ERROR: Transaction aborted and rolled back!';
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line:    ' + CAST(ERROR_LINE() AS VARCHAR(10));
    PRINT 'Error Number:  ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    PRINT '******************************************************************';
    THROW;
END CATCH;
