-- =============================================================================
-- 010_Purge_Production_Virgin.sql
-- Base Production "vierge" pour nouveau déploiement établissement.
-- Conserve : permissions globales, nomenclatures géo, paramètres techniques
-- Supprime : école, utilisateurs, rôles école, frais, élèves, finance, logs…
-- L'assistant premier démarrage recrée école + admin + année + frais de base.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

-- Notifications / sync / logs
IF OBJECT_ID(N'dbo.NotificationRecipients', N'U') IS NOT NULL DELETE FROM dbo.NotificationRecipients;
IF OBJECT_ID(N'dbo.ParentDeviceTokens', N'U') IS NOT NULL DELETE FROM dbo.ParentDeviceTokens;
IF OBJECT_ID(N'dbo.SchoolNotifications', N'U') IS NOT NULL DELETE FROM dbo.SchoolNotifications;
IF OBJECT_ID(N'dbo.SyncOutboxItem', N'U') IS NOT NULL DELETE FROM dbo.SyncOutboxItem;
IF OBJECT_ID(N'dbo.SyncOutboxUnit', N'U') IS NOT NULL DELETE FROM dbo.SyncOutboxUnit;
IF OBJECT_ID(N'dbo.SyncJournal', N'U') IS NOT NULL DELETE FROM dbo.SyncJournal;
IF OBJECT_ID(N'dbo.SyncWatermark', N'U') IS NOT NULL DELETE FROM dbo.SyncWatermark;
IF OBJECT_ID(N'dbo.AuditEntries', N'U') IS NOT NULL DELETE FROM dbo.AuditEntries;
IF OBJECT_ID(N'dbo.LoginHistory', N'U') IS NOT NULL DELETE FROM dbo.LoginHistory;
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NOT NULL DELETE FROM dbo.RefreshTokens;
IF OBJECT_ID(N'dbo.DeliberationAuditEntries', N'U') IS NOT NULL DELETE FROM dbo.DeliberationAuditEntries;

-- Cartes
IF OBJECT_ID(N'dbo.CarteImpression', N'U') IS NOT NULL DELETE FROM dbo.CarteImpression;
IF OBJECT_ID(N'dbo.CarteHistorique', N'U') IS NOT NULL DELETE FROM dbo.CarteHistorique;
IF OBJECT_ID(N'dbo.Carte', N'U') IS NOT NULL DELETE FROM dbo.Carte;
IF OBJECT_ID(N'dbo.CarteParametres', N'U') IS NOT NULL DELETE FROM dbo.CarteParametres;
IF OBJECT_ID(N'dbo.CarteModele', N'U') IS NOT NULL DELETE FROM dbo.CarteModele;

-- Notes / résultats
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
IF OBJECT_ID(N'dbo.ClassPeriodDeliberationMinutes', N'U') IS NOT NULL DELETE FROM dbo.ClassPeriodDeliberationMinutes;
IF OBJECT_ID(N'dbo.ClassPeriodResultValidationEvents', N'U') IS NOT NULL DELETE FROM dbo.ClassPeriodResultValidationEvents;
IF OBJECT_ID(N'dbo.ClassPeriodResultValidations', N'U') IS NOT NULL DELETE FROM dbo.ClassPeriodResultValidations;

-- Présences
IF OBJECT_ID(N'dbo.StudentAttendances', N'U') IS NOT NULL DELETE FROM dbo.StudentAttendances;
IF OBJECT_ID(N'dbo.TeacherAttendances', N'U') IS NOT NULL DELETE FROM dbo.TeacherAttendances;
IF OBJECT_ID(N'dbo.DisciplineRecords', N'U') IS NOT NULL DELETE FROM dbo.DisciplineRecords;
IF OBJECT_ID(N'dbo.MeritRecords', N'U') IS NOT NULL DELETE FROM dbo.MeritRecords;
IF OBJECT_ID(N'dbo.Announcements', N'U') IS NOT NULL DELETE FROM dbo.Announcements;
IF OBJECT_ID(N'dbo.CalendarEvents', N'U') IS NOT NULL DELETE FROM dbo.CalendarEvents;
IF OBJECT_ID(N'dbo.ScheduleSlots', N'U') IS NOT NULL DELETE FROM dbo.ScheduleSlots;

-- Finance opérationnelle + paramétrage financier école
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
IF OBJECT_ID(N'dbo.ClassFeeAmounts', N'U') IS NOT NULL DELETE FROM dbo.ClassFeeAmounts;
IF OBJECT_ID(N'dbo.FeeTypeInstallments', N'U') IS NOT NULL DELETE FROM dbo.FeeTypeInstallments;
IF OBJECT_ID(N'dbo.FeeInstallments', N'U') IS NOT NULL DELETE FROM dbo.FeeInstallments;
IF OBJECT_ID(N'dbo.FeePricingCategories', N'U') IS NOT NULL DELETE FROM dbo.FeePricingCategories;
IF OBJECT_ID(N'dbo.FeeTypes', N'U') IS NOT NULL DELETE FROM dbo.FeeTypes;
IF OBJECT_ID(N'dbo.FinCleRepartitionDetail', N'U') IS NOT NULL DELETE FROM dbo.FinCleRepartitionDetail;
IF OBJECT_ID(N'dbo.FinCleRepartition', N'U') IS NOT NULL DELETE FROM dbo.FinCleRepartition;
IF OBJECT_ID(N'dbo.FinDestinationRepartition', N'U') IS NOT NULL DELETE FROM dbo.FinDestinationRepartition;
IF OBJECT_ID(N'dbo.FinRetenueConfiguration', N'U') IS NOT NULL DELETE FROM dbo.FinRetenueConfiguration;
IF OBJECT_ID(N'dbo.FinRetenue', N'U') IS NOT NULL DELETE FROM dbo.FinRetenue;
IF OBJECT_ID(N'dbo.FinHistoriqueTaux', N'U') IS NOT NULL DELETE FROM dbo.FinHistoriqueTaux;
IF OBJECT_ID(N'dbo.FinTauxChange', N'U') IS NOT NULL DELETE FROM dbo.FinTauxChange;
IF OBJECT_ID(N'dbo.FinTypeTaux', N'U') IS NOT NULL DELETE FROM dbo.FinTypeTaux;
IF OBJECT_ID(N'dbo.FinEtablissementDevise', N'U') IS NOT NULL DELETE FROM dbo.FinEtablissementDevise;
IF OBJECT_ID(N'dbo.CashRegisters', N'U') IS NOT NULL DELETE FROM dbo.CashRegisters;
IF OBJECT_ID(N'dbo.Banks', N'U') IS NOT NULL DELETE FROM dbo.Banks;
IF OBJECT_ID(N'dbo.AppConfigurations', N'U') IS NOT NULL DELETE FROM dbo.AppConfigurations;

-- Élèves / inscriptions
IF OBJECT_ID(N'dbo.EnrollmentPricingCategoryHistory', N'U') IS NOT NULL DELETE FROM dbo.EnrollmentPricingCategoryHistory;
IF OBJECT_ID(N'dbo.StudentDocuments', N'U') IS NOT NULL DELETE FROM dbo.StudentDocuments;
IF OBJECT_ID(N'dbo.StudentStatusHistory', N'U') IS NOT NULL DELETE FROM dbo.StudentStatusHistory;
IF OBJECT_ID(N'dbo.Enrollments', N'U') IS NOT NULL DELETE FROM dbo.Enrollments;
IF OBJECT_ID(N'dbo.StudentGuardians', N'U') IS NOT NULL DELETE FROM dbo.StudentGuardians;
IF OBJECT_ID(N'dbo.Guardians', N'U') IS NOT NULL DELETE FROM dbo.Guardians;
IF OBJECT_ID(N'dbo.Students', N'U') IS NOT NULL DELETE FROM dbo.Students;

-- Enseignants / cours / classes
IF OBJECT_ID(N'dbo.CourseAssignments', N'U') IS NOT NULL DELETE FROM dbo.CourseAssignments;
IF OBJECT_ID(N'dbo.PedagogicalClassCourses', N'U') IS NOT NULL DELETE FROM dbo.PedagogicalClassCourses;
IF OBJECT_ID(N'dbo.MaximaParPeriode', N'U') IS NOT NULL DELETE FROM dbo.MaximaParPeriode;
IF OBJECT_ID(N'dbo.EvaluationTypes', N'U') IS NOT NULL DELETE FROM dbo.EvaluationTypes;
IF OBJECT_ID(N'dbo.PersonnelHrProfiles', N'U') IS NOT NULL DELETE FROM dbo.PersonnelHrProfiles;
IF OBJECT_ID(N'dbo.Teachers', N'U') IS NOT NULL DELETE FROM dbo.Teachers;
IF OBJECT_ID(N'dbo.ClassRooms', N'U') IS NOT NULL DELETE FROM dbo.ClassRooms;
IF OBJECT_ID(N'dbo.PedagogicalClasses', N'U') IS NOT NULL DELETE FROM dbo.PedagogicalClasses;
IF OBJECT_ID(N'dbo.Sections', N'U') IS NOT NULL DELETE FROM dbo.Sections;
IF OBJECT_ID(N'dbo.StudyOptions', N'U') IS NOT NULL DELETE FROM dbo.StudyOptions;
IF OBJECT_ID(N'dbo.Options', N'U') IS NOT NULL DELETE FROM dbo.Options;
IF OBJECT_ID(N'dbo.Cycles', N'U') IS NOT NULL DELETE FROM dbo.Cycles;
IF OBJECT_ID(N'dbo.AcademicPeriods', N'U') IS NOT NULL DELETE FROM dbo.AcademicPeriods;
IF OBJECT_ID(N'dbo.AcademicMainPeriods', N'U') IS NOT NULL DELETE FROM dbo.AcademicMainPeriods;
IF OBJECT_ID(N'dbo.AcademicYears', N'U') IS NOT NULL DELETE FROM dbo.AcademicYears;
IF OBJECT_ID(N'dbo.PedagogicalPeriods', N'U') IS NOT NULL DELETE FROM dbo.PedagogicalPeriods;
IF OBJECT_ID(N'dbo.Courses', N'U') IS NOT NULL DELETE FROM dbo.Courses;
IF OBJECT_ID(N'dbo.Branches', N'U') IS NOT NULL DELETE FROM dbo.Branches;

-- Branding
IF OBJECT_ID(N'dbo.SchoolLogos', N'U') IS NOT NULL DELETE FROM dbo.SchoolLogos;
IF OBJECT_ID(N'dbo.SchoolDocumentHeaders', N'U') IS NOT NULL DELETE FROM dbo.SchoolDocumentHeaders;
IF OBJECT_ID(N'dbo.SchoolSignatures', N'U') IS NOT NULL DELETE FROM dbo.SchoolSignatures;
IF OBJECT_ID(N'dbo.SchoolStamps', N'U') IS NOT NULL DELETE FROM dbo.SchoolStamps;
IF OBJECT_ID(N'dbo.SchoolDocumentFooters', N'U') IS NOT NULL DELETE FROM dbo.SchoolDocumentFooters;
IF OBJECT_ID(N'dbo.DocumentBranding', N'U') IS NOT NULL DELETE FROM dbo.DocumentBranding;

-- Sécurité : vider utilisateurs / rôles école (permissions globales conservées)
IF OBJECT_ID(N'dbo.UserRoleAssignments', N'U') IS NOT NULL DELETE FROM dbo.UserRoleAssignments;
IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NOT NULL DELETE FROM dbo.RolePermissions;
IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NOT NULL DELETE FROM dbo.UserAccounts;
IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL DELETE FROM dbo.Roles;

-- École (après enfants)
IF OBJECT_ID(N'dbo.Schools', N'U') IS NOT NULL DELETE FROM dbo.Schools;

-- Contrôles
DECLARE @students INT = CASE WHEN OBJECT_ID(N'dbo.Students', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM Students) END;
DECLARE @schools INT = CASE WHEN OBJECT_ID(N'dbo.Schools', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM Schools) END;
DECLARE @fees INT = CASE WHEN OBJECT_ID(N'dbo.FeeTypes', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM FeeTypes) END;
DECLARE @users INT = CASE WHEN OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM UserAccounts) END;
DECLARE @perms INT = CASE WHEN OBJECT_ID(N'dbo.Permissions', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM Permissions) END;

IF @students <> 0 OR @schools <> 0 OR @fees <> 0 OR @users <> 0
BEGIN
    RAISERROR(N'Contrôle post-purge virgin échoué — ROLLBACK.', 16, 1);
    SELECT @students AS Students, @schools AS Schools, @fees AS FeeTypes, @users AS Users, @perms AS Permissions;
    ROLLBACK TRANSACTION;
    RETURN;
END;

COMMIT TRANSACTION;
PRINT N'Purge Production Virgin terminée — permissions conservées ; école/admin à créer via assistant.';
PRINT N'Permissions restantes : ' + CONVERT(nvarchar(20), @perms);
GO
