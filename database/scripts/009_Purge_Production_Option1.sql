-- =============================================================================
-- Purge Production Option 1 — SchoolManagementRDC_Production
-- Conserve : école, structure pédagogique, frais/config, rôles, permissions, admin
-- Supprime : élèves, parents, enseignants, inscriptions, paiements, notes,
--            notifications, logs, utilisateurs hors admin
-- Ne modifie PAS le schéma.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
USE [SchoolManagementRDC_Production];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @AdminId UNIQUEIDENTIFIER =
(
    SELECT TOP (1) Id FROM UserAccounts WHERE UserName = N'admin' ORDER BY CreatedAt
);

IF @AdminId IS NULL
BEGIN
    RAISERROR(N'Compte admin introuvable — purge annulée.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

PRINT N'Admin conservé : ' + CONVERT(nvarchar(36), @AdminId);

-- ---- Notifications ----
IF OBJECT_ID(N'dbo.NotificationRecipients', N'U') IS NOT NULL DELETE FROM dbo.NotificationRecipients;
IF OBJECT_ID(N'dbo.ParentDeviceTokens', N'U') IS NOT NULL DELETE FROM dbo.ParentDeviceTokens;
IF OBJECT_ID(N'dbo.SchoolNotifications', N'U') IS NOT NULL DELETE FROM dbo.SchoolNotifications;

-- ---- Sync / logs ----
IF OBJECT_ID(N'dbo.SyncOutboxItem', N'U') IS NOT NULL DELETE FROM dbo.SyncOutboxItem;
IF OBJECT_ID(N'dbo.SyncOutboxUnit', N'U') IS NOT NULL DELETE FROM dbo.SyncOutboxUnit;
IF OBJECT_ID(N'dbo.SyncJournal', N'U') IS NOT NULL DELETE FROM dbo.SyncJournal;
IF OBJECT_ID(N'dbo.SyncWatermark', N'U') IS NOT NULL DELETE FROM dbo.SyncWatermark;
IF OBJECT_ID(N'dbo.AuditEntries', N'U') IS NOT NULL DELETE FROM dbo.AuditEntries;
IF OBJECT_ID(N'dbo.LoginHistory', N'U') IS NOT NULL DELETE FROM dbo.LoginHistory;
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NOT NULL DELETE FROM dbo.RefreshTokens;
IF OBJECT_ID(N'dbo.DeliberationAuditEntries', N'U') IS NOT NULL DELETE FROM dbo.DeliberationAuditEntries;

-- ---- Cartes élèves ----
IF OBJECT_ID(N'dbo.CarteImpression', N'U') IS NOT NULL DELETE FROM dbo.CarteImpression;
IF OBJECT_ID(N'dbo.CarteHistorique', N'U') IS NOT NULL DELETE FROM dbo.CarteHistorique;
IF OBJECT_ID(N'dbo.Carte', N'U') IS NOT NULL DELETE FROM dbo.Carte;

-- ---- Notes / résultats / délibération ----
IF OBJECT_ID(N'dbo.ReportCardDetails', N'U') IS NOT NULL DELETE FROM dbo.ReportCardDetails;
IF OBJECT_ID(N'dbo.ReportCards', N'U') IS NOT NULL DELETE FROM dbo.ReportCards;
IF OBJECT_ID(N'dbo.GradeEntries', N'U') IS NOT NULL DELETE FROM dbo.GradeEntries;
IF OBJECT_ID(N'dbo.Evaluations', N'U') IS NOT NULL DELETE FROM dbo.Evaluations;
IF OBJECT_ID(N'dbo.PeriodResults', N'U') IS NOT NULL DELETE FROM dbo.PeriodResults;
IF OBJECT_ID(N'dbo.StudentPeriodConducts', N'U') IS NOT NULL DELETE FROM dbo.StudentPeriodConducts;
IF OBJECT_ID(N'dbo.PedagogicalBonusPoints', N'U') IS NOT NULL DELETE FROM dbo.PedagogicalBonusPoints;
IF OBJECT_ID(N'dbo.CourseExemptions', N'U') IS NOT NULL DELETE FROM dbo.CourseExemptions;
IF OBJECT_ID(N'dbo.StudentRemedialCourses', N'U') IS NOT NULL DELETE FROM dbo.StudentRemedialCourses;
IF OBJECT_ID(N'dbo.StudentRemedialSessions', N'U') IS NOT NULL DELETE FROM dbo.StudentRemedialSessions;
IF OBJECT_ID(N'dbo.DeliberationDecisionEvents', N'U') IS NOT NULL DELETE FROM dbo.DeliberationDecisionEvents;
IF OBJECT_ID(N'dbo.DeliberationDecisions', N'U') IS NOT NULL DELETE FROM dbo.DeliberationDecisions;
IF OBJECT_ID(N'dbo.__DeliberationFinalDecisionV2', N'U') IS NOT NULL DELETE FROM dbo.__DeliberationFinalDecisionV2;
IF OBJECT_ID(N'dbo.ClassPeriodDeliberationMinutes', N'U') IS NOT NULL DELETE FROM dbo.ClassPeriodDeliberationMinutes;
IF OBJECT_ID(N'dbo.ClassPeriodResultValidationEvents', N'U') IS NOT NULL DELETE FROM dbo.ClassPeriodResultValidationEvents;
IF OBJECT_ID(N'dbo.ClassPeriodResultValidations', N'U') IS NOT NULL DELETE FROM dbo.ClassPeriodResultValidations;

-- ---- Présences / discipline ----
IF OBJECT_ID(N'dbo.StudentAttendances', N'U') IS NOT NULL DELETE FROM dbo.StudentAttendances;
IF OBJECT_ID(N'dbo.TeacherAttendances', N'U') IS NOT NULL DELETE FROM dbo.TeacherAttendances;
IF OBJECT_ID(N'dbo.DisciplineRecords', N'U') IS NOT NULL DELETE FROM dbo.DisciplineRecords;
IF OBJECT_ID(N'dbo.MeritRecords', N'U') IS NOT NULL DELETE FROM dbo.MeritRecords;
IF OBJECT_ID(N'dbo.Announcements', N'U') IS NOT NULL DELETE FROM dbo.Announcements;
IF OBJECT_ID(N'dbo.CalendarEvents', N'U') IS NOT NULL DELETE FROM dbo.CalendarEvents;
IF OBJECT_ID(N'dbo.ScheduleSlots', N'U') IS NOT NULL DELETE FROM dbo.ScheduleSlots;

-- ---- Finance opérationnelle ----
IF OBJECT_ID(N'dbo.FinRetenueApplication', N'U') IS NOT NULL DELETE FROM dbo.FinRetenueApplication;
IF OBJECT_ID(N'dbo.FinRepartitionRecette', N'U') IS NOT NULL DELETE FROM dbo.FinRepartitionRecette;
IF OBJECT_ID(N'dbo.FinDepenseRepartitionDevise', N'U') IS NOT NULL DELETE FROM dbo.FinDepenseRepartitionDevise;
IF OBJECT_ID(N'dbo.FinDepense', N'U') IS NOT NULL DELETE FROM dbo.FinDepense;
IF OBJECT_ID(N'dbo.FinDemandePaiement', N'U') IS NOT NULL DELETE FROM dbo.FinDemandePaiement;
IF OBJECT_ID(N'dbo.CashMovements', N'U') IS NOT NULL DELETE FROM dbo.CashMovements;
IF OBJECT_ID(N'dbo.PaymentReversals', N'U') IS NOT NULL DELETE FROM dbo.PaymentReversals;
IF OBJECT_ID(N'dbo.PaymentLines', N'U') IS NOT NULL DELETE FROM dbo.PaymentLines;
IF OBJECT_ID(N'dbo.Payments', N'U') IS NOT NULL DELETE FROM dbo.Payments;
IF OBJECT_ID(N'dbo.PaymentModalities', N'U') IS NOT NULL DELETE FROM dbo.PaymentModalities;
IF OBJECT_ID(N'dbo.StudentFeeBalances', N'U') IS NOT NULL DELETE FROM dbo.StudentFeeBalances;

-- ---- Inscriptions / documents élèves ----
IF OBJECT_ID(N'dbo.EnrollmentPricingCategoryHistory', N'U') IS NOT NULL DELETE FROM dbo.EnrollmentPricingCategoryHistory;
IF OBJECT_ID(N'dbo.StudentDocuments', N'U') IS NOT NULL DELETE FROM dbo.StudentDocuments;
IF OBJECT_ID(N'dbo.StudentStatusHistory', N'U') IS NOT NULL DELETE FROM dbo.StudentStatusHistory;
IF OBJECT_ID(N'dbo.Enrollments', N'U') IS NOT NULL DELETE FROM dbo.Enrollments;
IF OBJECT_ID(N'dbo.StudentGuardians', N'U') IS NOT NULL DELETE FROM dbo.StudentGuardians;
IF OBJECT_ID(N'dbo.Guardians', N'U') IS NOT NULL DELETE FROM dbo.Guardians;
IF OBJECT_ID(N'dbo.Students', N'U') IS NOT NULL DELETE FROM dbo.Students;

-- ---- Enseignants / affectations ----
IF OBJECT_ID(N'dbo.CourseAssignments', N'U') IS NOT NULL DELETE FROM dbo.CourseAssignments;
IF OBJECT_ID(N'dbo.PersonnelHrProfiles', N'U') IS NOT NULL DELETE FROM dbo.PersonnelHrProfiles;
IF OBJECT_ID(N'dbo.Teachers', N'U') IS NOT NULL DELETE FROM dbo.Teachers;

-- ---- Utilisateurs hors admin ----
DELETE FROM dbo.UserRoleAssignments WHERE UserId <> @AdminId;
DELETE FROM dbo.UserAccounts WHERE Id <> @AdminId;

-- Contrôles post-purge
DECLARE @students INT = (SELECT COUNT(*) FROM Students);
DECLARE @users INT = (SELECT COUNT(*) FROM UserAccounts);
DECLARE @adminLeft INT = (SELECT COUNT(*) FROM UserAccounts WHERE UserName = N'admin');
DECLARE @perms INT = (SELECT COUNT(*) FROM Permissions);
DECLARE @roles INT = (SELECT COUNT(*) FROM Roles);
DECLARE @schools INT = (SELECT COUNT(*) FROM Schools);
DECLARE @classes INT = (SELECT COUNT(*) FROM PedagogicalClasses);
DECLARE @pays INT = (SELECT COUNT(*) FROM Payments);
DECLARE @enr INT = (SELECT COUNT(*) FROM Enrollments);

IF @students <> 0 OR @pays <> 0 OR @enr <> 0 OR @adminLeft <> 1 OR @users <> 1 OR @schools < 1
BEGIN
    RAISERROR(N'Contrôle post-purge échoué — ROLLBACK.', 16, 1);
    SELECT @students AS Students, @users AS Users, @adminLeft AS AdminLeft,
           @schools AS Schools, @pays AS Payments, @enr AS Enrollments,
           @perms AS Permissions, @roles AS Roles, @classes AS PedagogicalClasses;
    ROLLBACK TRANSACTION;
    RETURN;
END;

COMMIT TRANSACTION;

PRINT N'Purge Production Option 1 terminée avec succès.';
SELECT
    (SELECT COUNT(*) FROM Schools) AS Schools,
    (SELECT COUNT(*) FROM PedagogicalClasses) AS PedagogicalClasses,
    (SELECT COUNT(*) FROM Permissions) AS Permissions,
    (SELECT COUNT(*) FROM Roles) AS Roles,
    (SELECT COUNT(*) FROM UserAccounts) AS UserAccounts,
    (SELECT COUNT(*) FROM Students) AS Students,
    (SELECT COUNT(*) FROM Teachers) AS Teachers,
    (SELECT COUNT(*) FROM Guardians) AS Guardians,
    (SELECT COUNT(*) FROM Enrollments) AS Enrollments,
    (SELECT COUNT(*) FROM Payments) AS Payments,
    (SELECT COUNT(*) FROM SchoolNotifications) AS Notifications;
GO
