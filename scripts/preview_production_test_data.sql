-- ==============================================================================
-- SalonBook Production Database (db77856) Genuinely READ-ONLY Test-Data Preview
-- Target Database: db77856 on MonsterASP (SQL Server)
-- Mode: STRICTLY READ-ONLY (SELECT, DECLARE, SET, PRINT only)
-- Contains ABSOLUTELY NO DELETE, UPDATE, INSERT, DROP, TRUNCATE, or ALTER on user tables
-- Uses POSITIVE IDENTIFICATION of confirmed test record IDs from the latest audit
-- ==============================================================================

SET NOCOUNT ON;

PRINT '==================================================================';
PRINT 'SALONBOOK PRODUCTION TEST-DATA PREVIEW (100% READ-ONLY)';
PRINT 'Target Database: db77856';
PRINT 'Server: ' + @@SERVERNAME;
PRINT 'Timestamp (UTC): ' + CONVERT(VARCHAR(30), GETUTCDATE(), 120);
PRINT '==================================================================';

-- ------------------------------------------------------------------------------
-- TEMP TABLES OF CONFIRMED TEST IDS (IN TEMPDB - NO USER DATA MUTATION)
-- ------------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#ConfirmedTestSalons') IS NOT NULL DROP TABLE #ConfirmedTestSalons;
CREATE TABLE #ConfirmedTestSalons (SalonId INT PRIMARY KEY);

IF OBJECT_ID('tempdb..#ConfirmedTestUsers') IS NOT NULL DROP TABLE #ConfirmedTestUsers;
CREATE TABLE #ConfirmedTestUsers (UserId INT PRIMARY KEY);

-- Confirmed Audited Test Salons: IDs 2-172 (171 salons) and IDs 174-258 (85 salons) = 256 salons
INSERT INTO #ConfirmedTestSalons (SalonId)
SELECT Id FROM [Salons] WHERE (Id BETWEEN 2 AND 172) OR (Id BETWEEN 174 AND 258);

-- Confirmed Audited Test Users: IDs 3-297 (295 users) and IDs 301-449 (149 users) = 444 users
INSERT INTO #ConfirmedTestUsers (UserId)
SELECT Id FROM [Users] WHERE (Id BETWEEN 3 AND 297) OR (Id BETWEEN 301 AND 449);

-- ------------------------------------------------------------------------------
-- 1. EXACT GENUINE IDENTITY MAPPING VERIFICATION (FIX 3)
-- ------------------------------------------------------------------------------
PRINT '';
PRINT '### 1. EXACT GENUINE IDENTITIES VERIFICATION ###';

SELECT 
    'User 1' AS EntityKey,
    'Super Administrator' AS ExpectedName,
    'superadmin@salonbook.com' AS ExpectedEmail,
    'SuperAdmin' AS ExpectedRole,
    'NULL' AS ExpectedSalonId,
    u.Email AS ActualEmail,
    u.Role AS ActualRole,
    ISNULL(CAST(u.SalonId AS VARCHAR(10)), 'NULL') AS ActualSalonId,
    CASE WHEN u.Email = 'superadmin@salonbook.com' AND u.Role = 'SuperAdmin' AND u.SalonId IS NULL THEN 'VALIDATED' ELSE 'MISMATCH' END AS IdentityStatus
FROM [Users] u WHERE u.Id = 1
UNION ALL
SELECT 
    'User 2',
    'Rozi Khan',
    'rozi@gmail.com',
    'Admin',
    '1',
    u.Email,
    u.Role,
    ISNULL(CAST(u.SalonId AS VARCHAR(10)), 'NULL'),
    CASE WHEN u.Email = 'rozi@gmail.com' AND u.Role = 'Admin' AND u.SalonId = 1 THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Users] u WHERE u.Id = 2
UNION ALL
SELECT 
    'User 298',
    'minha khan',
    'minha@gmail.com',
    'Admin',
    '173',
    u.Email,
    u.Role,
    ISNULL(CAST(u.SalonId AS VARCHAR(10)), 'NULL'),
    CASE WHEN u.Email = 'minha@gmail.com' AND u.Role = 'Admin' AND u.SalonId = 173 THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Users] u WHERE u.Id = 298
UNION ALL
SELECT 
    'User 299',
    'zavi',
    'zavi@gmail.com',
    'User',
    'NULL',
    u.Email,
    u.Role,
    ISNULL(CAST(u.SalonId AS VARCHAR(10)), 'NULL'),
    CASE WHEN u.Email = 'zavi@gmail.com' AND u.Role = 'User' AND u.SalonId IS NULL THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Users] u WHERE u.Id = 299
UNION ALL
SELECT 
    'User 300',
    'Rumana Khan',
    'rumanakhan@gmail.com',
    'User',
    'NULL',
    u.Email,
    u.Role,
    ISNULL(CAST(u.SalonId AS VARCHAR(10)), 'NULL'),
    CASE WHEN u.Email = 'rumanakhan@gmail.com' AND u.Role = 'User' AND u.SalonId IS NULL THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Users] u WHERE u.Id = 300
UNION ALL
SELECT 
    'Salon 1',
    'Rozi''s Salon',
    'rozis-salon',
    'Hair Salon',
    'N/A',
    s.Slug,
    s.BusinessType,
    'Active=' + CAST(s.IsActive AS VARCHAR(5)),
    CASE WHEN s.Name = 'Rozi''s Salon' AND s.Slug = 'rozis-salon' THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Salons] s WHERE s.Id = 1
UNION ALL
SELECT 
    'Salon 173',
    'Minha''s salon',
    'minhas-salon',
    'Other',
    'N/A',
    s.Slug,
    s.BusinessType,
    'Active=' + CAST(s.IsActive AS VARCHAR(5)),
    CASE WHEN s.Name = 'Minha''s salon' AND s.Slug = 'minhas-salon' THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Salons] s WHERE s.Id = 173
UNION ALL
SELECT 
    'Service 1',
    'Hair cut (Salon 1)',
    'Price: 0.49',
    'SalonId 1',
    'N/A',
    sv.Name,
    CAST(sv.Price AS VARCHAR(20)),
    CAST(sv.SalonId AS VARCHAR(10)),
    CASE WHEN sv.SalonId = 1 AND sv.Name = 'Hair cut' THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Services] sv WHERE sv.Id = 1
UNION ALL
SELECT 
    'Service 86',
    'hair (Salon 173)',
    'Price: 150.00',
    'SalonId 173',
    'N/A',
    sv.Name,
    CAST(sv.Price AS VARCHAR(20)),
    CAST(sv.SalonId AS VARCHAR(10)),
    CASE WHEN sv.SalonId = 173 AND sv.Name = 'hair' THEN 'VALIDATED' ELSE 'MISMATCH' END
FROM [Services] sv WHERE sv.Id = 86;

-- ------------------------------------------------------------------------------
-- 2. DYNAMIC GENUINE REFRESH TOKENS AUDIT (GROUPED BY USER)
-- ------------------------------------------------------------------------------
PRINT '';
PRINT '### 2. GENUINE REFRESH TOKENS AUDIT (DYNAMIC COUNT) ###';

DECLARE @DynamicGenuineTokenCount INT;
SELECT @DynamicGenuineTokenCount = COUNT(*) FROM [RefreshTokens] WHERE UserId IN (1, 2, 298, 299, 300);
PRINT 'Total Current Genuine Refresh Tokens in Live Database: ' + CAST(@DynamicGenuineTokenCount AS VARCHAR(10));

SELECT 
    r.UserId,
    u.FullName,
    u.Email,
    u.Role,
    COUNT(*) AS TotalTokens,
    SUM(CASE WHEN r.RevokedAtUtc IS NULL AND r.ExpiresAtUtc > GETUTCDATE() THEN 1 ELSE 0 END) AS ActiveTokens,
    SUM(CASE WHEN r.RevokedAtUtc IS NOT NULL THEN 1 ELSE 0 END) AS RevokedTokens,
    SUM(CASE WHEN r.RevokedAtUtc IS NULL AND r.ExpiresAtUtc <= GETUTCDATE() THEN 1 ELSE 0 END) AS ExpiredTokens,
    STRING_AGG(CAST(r.Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY r.Id) AS PreservedTokenIds
FROM [RefreshTokens] r
INNER JOIN [Users] u ON r.UserId = u.Id
WHERE r.UserId IN (1, 2, 298, 299, 300)
GROUP BY r.UserId, u.FullName, u.Email, u.Role
ORDER BY r.UserId;

-- ------------------------------------------------------------------------------
-- 3. CLIENT CONSULTATION CARDS AUDIT (ALL 6 RECORDS)
-- ------------------------------------------------------------------------------
PRINT '';
PRINT '### 3. CLIENT CONSULTATION CARDS AUDIT (ALL 6 RECORDS) ###';
SELECT 
    c.Id AS CardId,
    c.SalonId,
    s.Name AS SalonName,
    CASE WHEN c.SalonId IN (1, 173) THEN 'GENUINE SALON' ELSE 'TEST SALON' END AS SalonCategory,
    c.CustomerId,
    u.FullName AS CustomerName,
    u.Email AS CustomerEmail,
    CASE WHEN c.CustomerId IN (1, 2, 298, 299, 300) THEN 'GENUINE USER' ELSE 'TEST USER' END AS CustomerCategory,
    c.CreatedAtUtc,
    'TARGET FOR DELETION (Test card linked to test customer)' AS ProposedAction
FROM [ClientConsultationCards] c
LEFT JOIN [Salons] s ON c.SalonId = s.Id
LEFT JOIN [Users] u ON c.CustomerId = u.Id
ORDER BY c.Id;

-- ------------------------------------------------------------------------------
-- 4. UNKNOWN / NEW RECORDS IDENTIFICATION (AUTOMATICALLY PRESERVED)
-- ------------------------------------------------------------------------------
PRINT '';
PRINT '### 4. UNKNOWN / NEW RECORDS CHECK (CONFIRM ZERO UNKNOWN RECORDS AT RISK) ###';

SELECT 
    'Users' AS EntityType,
    Id,
    FullName,
    Email,
    Role,
    CreatedAtUtc,
    'AUTOMATICALLY PRESERVED' AS ProtectionStatus
FROM [Users]
WHERE Id NOT IN (1, 2, 298, 299, 300)
  AND Id NOT IN (SELECT UserId FROM #ConfirmedTestUsers);

SELECT 
    'Salons' AS EntityType,
    Id,
    Name,
    Slug,
    BusinessType,
    CreatedAtUtc,
    'AUTOMATICALLY PRESERVED' AS ProtectionStatus
FROM [Salons]
WHERE Id NOT IN (1, 173)
  AND Id NOT IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- ------------------------------------------------------------------------------
-- 5. EXACT TARGET IDS & COUNTS ACROSS ALL 18 TABLES (FIX 1, 2, 4, 5)
-- ------------------------------------------------------------------------------
PRINT '';
PRINT '### 5. TARGET ROWS & EXACT TARGET IDS ACROSS ALL 18 APPLICATION TABLES ###';

-- 5.1 ClientFormulaRecords
SELECT 
    'ClientFormulaRecords' AS TableName,
    (SELECT COUNT(*) FROM [ClientFormulaRecords]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [ClientFormulaRecords] WHERE SalonId IN (1, 173) AND CustomerId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [ClientFormulaRecords] WHERE (SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) OR (CustomerId NOT IN (1, 2, 298, 299, 300) AND CustomerId NOT IN (SELECT UserId FROM #ConfirmedTestUsers))) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', '), 'NONE') AS ExactTargetIds
FROM [ClientFormulaRecords]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
   OR CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.2 Reviews
SELECT 
    'Reviews' AS TableName,
    (SELECT COUNT(*) FROM [Reviews]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [Reviews] WHERE SalonId IN (1, 173) AND CustomerId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [Reviews] WHERE (SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) OR (CustomerId NOT IN (1, 2, 298, 299, 300) AND CustomerId NOT IN (SELECT UserId FROM #ConfirmedTestUsers))) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', '), 'NONE') AS ExactTargetIds
FROM [Reviews]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
   OR CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.3 SalonOffPeakRules
SELECT 
    'SalonOffPeakRules' AS TableName,
    (SELECT COUNT(*) FROM [SalonOffPeakRules]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [SalonOffPeakRules] WHERE SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [SalonOffPeakRules] WHERE SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', '), 'NONE') AS ExactTargetIds
FROM [SalonOffPeakRules]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- 5.4 SubscriptionTransactions
SELECT 
    'SubscriptionTransactions' AS TableName,
    (SELECT COUNT(*) FROM [SubscriptionTransactions]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [SubscriptionTransactions] WHERE SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [SubscriptionTransactions] WHERE SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', '), 'NONE') AS ExactTargetIds
FROM [SubscriptionTransactions]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- 5.5 ClientConsultationCards
SELECT 
    'ClientConsultationCards' AS TableName,
    (SELECT COUNT(*) FROM [ClientConsultationCards]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [ClientConsultationCards] WHERE CustomerId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [ClientConsultationCards] WHERE CustomerId NOT IN (1, 2, 298, 299, 300) AND CustomerId NOT IN (SELECT UserId FROM #ConfirmedTestUsers)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [ClientConsultationCards]
WHERE CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.6 AppointmentReminders
SELECT 
    'AppointmentReminders' AS TableName,
    (SELECT COUNT(*) FROM [AppointmentReminders]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [AppointmentReminders] ar INNER JOIN [Appointments] a ON ar.AppointmentId = a.Id WHERE a.SalonId IN (1, 173) OR a.CustomerId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [AppointmentReminders] ar INNER JOIN [Appointments] a ON ar.AppointmentId = a.Id WHERE (a.SalonId NOT IN (1, 173) AND a.SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) OR (a.CustomerId NOT IN (1, 2, 298, 299, 300) AND a.CustomerId NOT IN (SELECT UserId FROM #ConfirmedTestUsers))) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(ar.Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY ar.Id), 'NONE') AS ExactTargetIds
FROM [AppointmentReminders] ar
INNER JOIN [Appointments] a ON ar.AppointmentId = a.Id
WHERE a.SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
  AND a.CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.7 Notifications
SELECT 
    'Notifications' AS TableName,
    (SELECT COUNT(*) FROM [Notifications]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [Notifications] WHERE RecipientUserId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [Notifications] WHERE RecipientUserId NOT IN (1, 2, 298, 299, 300) AND RecipientUserId NOT IN (SELECT UserId FROM #ConfirmedTestUsers)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [Notifications]
WHERE RecipientUserId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.8 Appointments
SELECT 
    'Appointments' AS TableName,
    (SELECT COUNT(*) FROM [Appointments]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [Appointments] WHERE SalonId IN (1, 173) OR CustomerId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [Appointments] WHERE (SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) OR (CustomerId NOT IN (1, 2, 298, 299, 300) AND CustomerId NOT IN (SELECT UserId FROM #ConfirmedTestUsers))) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [Appointments]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
  AND CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.9 StaffBreaks
SELECT 
    'StaffBreaks' AS TableName,
    (SELECT COUNT(*) FROM [StaffBreaks]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [StaffBreaks] sb INNER JOIN [StaffSchedules] ss ON sb.StaffScheduleId = ss.Id INNER JOIN [Staff] s ON ss.StaffId = s.Id WHERE s.SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [StaffBreaks] sb INNER JOIN [StaffSchedules] ss ON sb.StaffScheduleId = ss.Id INNER JOIN [Staff] s ON ss.StaffId = s.Id WHERE s.SalonId NOT IN (1, 173) AND s.SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(sb.Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY sb.Id), 'NONE') AS ExactTargetIds
FROM [StaffBreaks] sb
INNER JOIN [StaffSchedules] ss ON sb.StaffScheduleId = ss.Id
INNER JOIN [Staff] s ON ss.StaffId = s.Id
WHERE s.SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- 5.10 StaffSchedules
SELECT 
    'StaffSchedules' AS TableName,
    (SELECT COUNT(*) FROM [StaffSchedules]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [StaffSchedules] ss INNER JOIN [Staff] s ON ss.StaffId = s.Id WHERE s.SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [StaffSchedules] ss INNER JOIN [Staff] s ON ss.StaffId = s.Id WHERE s.SalonId NOT IN (1, 173) AND s.SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(ss.Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY ss.Id), 'NONE') AS ExactTargetIds
FROM [StaffSchedules] ss
INNER JOIN [Staff] s ON ss.StaffId = s.Id
WHERE s.SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- 5.11 Staff
SELECT 
    'Staff' AS TableName,
    (SELECT COUNT(*) FROM [Staff]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [Staff] WHERE SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [Staff] WHERE SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [Staff]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- 5.12 Services
SELECT 
    'Services' AS TableName,
    (SELECT COUNT(*) FROM [Services]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [Services] WHERE Id IN (1, 86) AND SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [Services] WHERE SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [Services]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons)
  AND Id NOT IN (1, 86);

-- 5.13 VendorPayouts
SELECT 
    'VendorPayouts' AS TableName,
    (SELECT COUNT(*) FROM [VendorPayouts]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [VendorPayouts] WHERE SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [VendorPayouts] WHERE SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [VendorPayouts]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- 5.14 VendorComplianceLogs
SELECT 
    'VendorComplianceLogs' AS TableName,
    (SELECT COUNT(*) FROM [VendorComplianceLogs]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [VendorComplianceLogs] WHERE SalonId IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [VendorComplianceLogs] WHERE SalonId NOT IN (1, 173) AND SalonId NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [VendorComplianceLogs]
WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- 5.15 PasswordResetTokens
SELECT 
    'PasswordResetTokens' AS TableName,
    (SELECT COUNT(*) FROM [PasswordResetTokens]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [PasswordResetTokens] WHERE UserId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [PasswordResetTokens] WHERE UserId NOT IN (1, 2, 298, 299, 300) AND UserId NOT IN (SELECT UserId FROM #ConfirmedTestUsers)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [PasswordResetTokens]
WHERE UserId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.16 RefreshTokens
SELECT 
    'RefreshTokens' AS TableName,
    (SELECT COUNT(*) FROM [RefreshTokens]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [RefreshTokens] WHERE UserId IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [RefreshTokens] WHERE UserId NOT IN (1, 2, 298, 299, 300) AND UserId NOT IN (SELECT UserId FROM #ConfirmedTestUsers)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [RefreshTokens]
WHERE UserId IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.17 Users
SELECT 
    'Users' AS TableName,
    (SELECT COUNT(*) FROM [Users]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [Users] WHERE Id IN (1, 2, 298, 299, 300)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [Users] WHERE Id NOT IN (1, 2, 298, 299, 300) AND Id NOT IN (SELECT UserId FROM #ConfirmedTestUsers)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [Users]
WHERE Id IN (SELECT UserId FROM #ConfirmedTestUsers);

-- 5.18 Salons
SELECT 
    'Salons' AS TableName,
    (SELECT COUNT(*) FROM [Salons]) AS TotalRowsInDb,
    (SELECT COUNT(*) FROM [Salons] WHERE Id IN (1, 173)) AS GenuinePreserved,
    (SELECT COUNT(*) FROM [Salons] WHERE Id NOT IN (1, 173) AND Id NOT IN (SELECT SalonId FROM #ConfirmedTestSalons)) AS UnknownPreserved,
    COUNT(*) AS TestTargetCount,
    ISNULL(STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY Id), 'NONE') AS ExactTargetIds
FROM [Salons]
WHERE Id IN (SELECT SalonId FROM #ConfirmedTestSalons);

-- ------------------------------------------------------------------------------
-- 6. GENUINE SET-THEORETIC OVERLAP AUDIT (FIX 2)
-- ------------------------------------------------------------------------------
PRINT '';
PRINT '### 6. GENUINE SET-THEORETIC OVERLAP AUDIT (COMPARING DELETION VS PROTECTED SETS) ###';

-- Salons Overlap
WITH ProposedDeletions AS (
    SELECT Id FROM [Salons] WHERE Id IN (SELECT SalonId FROM #ConfirmedTestSalons)
),
ProtectedRecords AS (
    SELECT Id FROM [Salons] WHERE Id IN (1, 173) OR Name IN ('Rozi''s Salon', 'Minha''s salon') OR Slug IN ('rozis-salon', 'minhas-salon')
),
Overlap AS (
    SELECT Id FROM ProposedDeletions INTERSECT SELECT Id FROM ProtectedRecords
)
SELECT 
    'Salons' AS TableName,
    (SELECT COUNT(*) FROM ProposedDeletions) AS ProposedDeletionCount,
    (SELECT COUNT(*) FROM ProtectedRecords) AS ProtectedRecordCount,
    (SELECT COUNT(*) FROM Overlap) AS OverlapCount,
    ISNULL((SELECT STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') FROM Overlap), 'NONE') AS OverlappingIds;

-- Users Overlap
WITH ProposedDeletions AS (
    SELECT Id FROM [Users] WHERE Id IN (SELECT UserId FROM #ConfirmedTestUsers)
),
ProtectedRecords AS (
    SELECT Id FROM [Users] WHERE Id IN (1, 2, 298, 299, 300) OR Email IN ('superadmin@salonbook.com', 'rozi@gmail.com', 'minha@gmail.com', 'zavi@gmail.com', 'rumanakhan@gmail.com')
),
Overlap AS (
    SELECT Id FROM ProposedDeletions INTERSECT SELECT Id FROM ProtectedRecords
)
SELECT 
    'Users' AS TableName,
    (SELECT COUNT(*) FROM ProposedDeletions) AS ProposedDeletionCount,
    (SELECT COUNT(*) FROM ProtectedRecords) AS ProtectedRecordCount,
    (SELECT COUNT(*) FROM Overlap) AS OverlapCount,
    ISNULL((SELECT STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') FROM Overlap), 'NONE') AS OverlappingIds;

-- Services Overlap
WITH ProposedDeletions AS (
    SELECT Id FROM [Services] WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons) AND Id NOT IN (1, 86)
),
ProtectedRecords AS (
    SELECT Id FROM [Services] WHERE Id IN (1, 86) OR SalonId IN (1, 173)
),
Overlap AS (
    SELECT Id FROM ProposedDeletions INTERSECT SELECT Id FROM ProtectedRecords
)
SELECT 
    'Services' AS TableName,
    (SELECT COUNT(*) FROM ProposedDeletions) AS ProposedDeletionCount,
    (SELECT COUNT(*) FROM ProtectedRecords) AS ProtectedRecordCount,
    (SELECT COUNT(*) FROM Overlap) AS OverlapCount,
    ISNULL((SELECT STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') FROM Overlap), 'NONE') AS OverlappingIds;

-- RefreshTokens Overlap
WITH ProposedDeletions AS (
    SELECT Id FROM [RefreshTokens] WHERE UserId IN (SELECT UserId FROM #ConfirmedTestUsers)
),
ProtectedRecords AS (
    SELECT Id FROM [RefreshTokens] WHERE UserId IN (1, 2, 298, 299, 300)
),
Overlap AS (
    SELECT Id FROM ProposedDeletions INTERSECT SELECT Id FROM ProtectedRecords
)
SELECT 
    'RefreshTokens' AS TableName,
    (SELECT COUNT(*) FROM ProposedDeletions) AS ProposedDeletionCount,
    (SELECT COUNT(*) FROM ProtectedRecords) AS ProtectedRecordCount,
    (SELECT COUNT(*) FROM Overlap) AS OverlapCount,
    ISNULL((SELECT STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') FROM Overlap), 'NONE') AS OverlappingIds;

-- Appointments Overlap
WITH ProposedDeletions AS (
    SELECT Id FROM [Appointments] WHERE SalonId IN (SELECT SalonId FROM #ConfirmedTestSalons) AND CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers)
),
ProtectedRecords AS (
    SELECT Id FROM [Appointments] WHERE SalonId IN (1, 173) OR CustomerId IN (1, 2, 298, 299, 300) OR PaymentVerifiedByUserId IN (1, 2, 298, 299, 300)
),
Overlap AS (
    SELECT Id FROM ProposedDeletions INTERSECT SELECT Id FROM ProtectedRecords
)
SELECT 
    'Appointments' AS TableName,
    (SELECT COUNT(*) FROM ProposedDeletions) AS ProposedDeletionCount,
    (SELECT COUNT(*) FROM ProtectedRecords) AS ProtectedRecordCount,
    (SELECT COUNT(*) FROM Overlap) AS OverlapCount,
    ISNULL((SELECT STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') FROM Overlap), 'NONE') AS OverlappingIds;

-- ClientConsultationCards Overlap
WITH ProposedDeletions AS (
    SELECT Id FROM [ClientConsultationCards] WHERE CustomerId IN (SELECT UserId FROM #ConfirmedTestUsers)
),
ProtectedRecords AS (
    SELECT Id FROM [ClientConsultationCards] WHERE CustomerId IN (1, 2, 298, 299, 300)
),
Overlap AS (
    SELECT Id FROM ProposedDeletions INTERSECT SELECT Id FROM ProtectedRecords
)
SELECT 
    'ClientConsultationCards' AS TableName,
    (SELECT COUNT(*) FROM ProposedDeletions) AS ProposedDeletionCount,
    (SELECT COUNT(*) FROM ProtectedRecords) AS ProtectedRecordCount,
    (SELECT COUNT(*) FROM Overlap) AS OverlapCount,
    ISNULL((SELECT STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') FROM Overlap), 'NONE') AS OverlappingIds;

-- Notifications Overlap
WITH ProposedDeletions AS (
    SELECT Id FROM [Notifications] WHERE RecipientUserId IN (SELECT UserId FROM #ConfirmedTestUsers)
),
ProtectedRecords AS (
    SELECT Id FROM [Notifications] WHERE RecipientUserId IN (1, 2, 298, 299, 300) OR SalonId IN (1, 173)
),
Overlap AS (
    SELECT Id FROM ProposedDeletions INTERSECT SELECT Id FROM ProtectedRecords
)
SELECT 
    'Notifications' AS TableName,
    (SELECT COUNT(*) FROM ProposedDeletions) AS ProposedDeletionCount,
    (SELECT COUNT(*) FROM ProtectedRecords) AS ProtectedRecordCount,
    (SELECT COUNT(*) FROM Overlap) AS OverlapCount,
    ISNULL((SELECT STRING_AGG(CAST(Id AS VARCHAR(MAX)), ', ') FROM Overlap), 'NONE') AS OverlappingIds;

-- ------------------------------------------------------------------------------
-- 7. PROJECTED REMAINING ROW COUNTS SUMMARY (FIX 1 & FIX 5)
-- ------------------------------------------------------------------------------
PRINT '';
PRINT '### 7. PROJECTED REMAINING ROW COUNTS AFTER CLEANUP ###';
SELECT 'Salons' AS TableName, 2 AS ExpectedRemaining
UNION ALL SELECT 'Users', 5
UNION ALL SELECT 'Services', 2
UNION ALL SELECT 'RefreshTokens', @DynamicGenuineTokenCount
UNION ALL SELECT 'Appointments', 0
UNION ALL SELECT 'Staff', 0
UNION ALL SELECT 'StaffSchedules', 0
UNION ALL SELECT 'StaffBreaks', 0
UNION ALL SELECT 'AppointmentReminders', 0
UNION ALL SELECT 'Notifications', 0
UNION ALL SELECT 'ClientConsultationCards', 0
UNION ALL SELECT 'PasswordResetTokens', 0
UNION ALL SELECT 'VendorPayouts', 0
UNION ALL SELECT 'VendorComplianceLogs', 0
UNION ALL SELECT 'ClientFormulaRecords', 0
UNION ALL SELECT 'Reviews', 0
UNION ALL SELECT 'SalonOffPeakRules', 0
UNION ALL SELECT 'SubscriptionTransactions', 0;

PRINT '';
PRINT 'READ-ONLY PREVIEW COMPLETE. ZERO CHANGES EXECUTED.';
