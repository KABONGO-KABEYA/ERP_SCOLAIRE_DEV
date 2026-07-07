-- ERP Administration Scolaire RDC
-- Vues, fonctions et procédures stockées
-- Exécuter APRÈS 001_InitialCreate_EF.sql sur la base SchoolManagementRDC

USE [SchoolManagementRDC];
GO

-- ============================================================
-- FONCTIONS UTILITAIRES
-- ============================================================

IF OBJECT_ID(N'dbo.fn_CalculerMoyennePonderee', N'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_CalculerMoyennePonderee;
GO

CREATE FUNCTION dbo.fn_CalculerMoyennePonderee
(
    @StudentId UNIQUEIDENTIFIER,
    @AcademicPeriodId UNIQUEIDENTIFIER
)
RETURNS DECIMAL(5, 2)
AS
BEGIN
    DECLARE @Moyenne DECIMAL(5, 2);

    SELECT @Moyenne = CASE
        WHEN SUM(c.Coefficient) = 0 THEN 0
        ELSE SUM(ge.Score * e.Weight * c.Coefficient) / SUM(e.Weight * c.Coefficient)
    END
    FROM GradeEntries ge
    INNER JOIN Evaluations e ON e.Id = ge.EvaluationId AND e.IsDeleted = 0
    INNER JOIN Courses c ON c.Id = e.CourseId AND c.IsDeleted = 0
    WHERE ge.StudentId = @StudentId
      AND e.AcademicPeriodId = @AcademicPeriodId
      AND ge.IsDeleted = 0
      AND ge.IsAbsent = 0;

    RETURN ISNULL(@Moyenne, 0);
END;
GO

IF OBJECT_ID(N'dbo.fn_CalculerRang', N'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_CalculerRang;
GO

CREATE FUNCTION dbo.fn_CalculerRang
(
    @StudentId UNIQUEIDENTIFIER,
    @ClassRoomId UNIQUEIDENTIFIER,
    @AcademicPeriodId UNIQUEIDENTIFIER
)
RETURNS INT
AS
BEGIN
    DECLARE @Rang INT;
    DECLARE @Moyenne DECIMAL(5, 2);

    SELECT @Moyenne = Average
    FROM PeriodResults
    WHERE StudentId = @StudentId
      AND ClassRoomId = @ClassRoomId
      AND AcademicPeriodId = @AcademicPeriodId
      AND IsDeleted = 0;

    SELECT @Rang = COUNT(*) + 1
    FROM PeriodResults
    WHERE ClassRoomId = @ClassRoomId
      AND AcademicPeriodId = @AcademicPeriodId
      AND IsDeleted = 0
      AND Average > @Moyenne;

    RETURN ISNULL(@Rang, 1);
END;
GO

-- ============================================================
-- VUES RAPPORTS
-- ============================================================

IF OBJECT_ID(N'dbo.vw_SituationFinanciereEleve', N'V') IS NOT NULL
    DROP VIEW dbo.vw_SituationFinanciereEleve;
GO

CREATE VIEW dbo.vw_SituationFinanciereEleve
AS
SELECT
    s.Id AS StudentId,
    s.RegistrationNumber,
    s.LastName,
    s.FirstName,
    sfb.AcademicYearId,
    ay.Label AS AcademicYearLabel,
    sfb.FeeTypeId,
    ft.Code AS FeeTypeCode,
    ft.Name AS FeeTypeName,
    sfb.Currency,
    sfb.AmountDue,
    sfb.AmountPaid,
    (sfb.AmountDue - sfb.AmountPaid) AS Balance,
    CASE
        WHEN sfb.AmountPaid >= sfb.AmountDue THEN N'À jour'
        WHEN sfb.AmountPaid > 0 THEN N'Partiel'
        ELSE N'Débiteur'
    END AS PaymentStatus
FROM StudentFeeBalances sfb
INNER JOIN Students s ON s.Id = sfb.StudentId AND s.IsDeleted = 0
INNER JOIN AcademicYears ay ON ay.Id = sfb.AcademicYearId AND ay.IsDeleted = 0
INNER JOIN FeeTypes ft ON ft.Id = sfb.FeeTypeId AND ft.IsDeleted = 0
WHERE sfb.IsDeleted = 0;
GO

IF OBJECT_ID(N'dbo.vw_EffectifsParClasse', N'V') IS NOT NULL
    DROP VIEW dbo.vw_EffectifsParClasse;
GO

CREATE VIEW dbo.vw_EffectifsParClasse
AS
SELECT
    cr.Id AS ClassRoomId,
    cr.Code AS ClassCode,
    cr.Name AS ClassName,
    ay.Id AS AcademicYearId,
    ay.Label AS AcademicYearLabel,
    sec.Name AS SectionName,
    opt.Name AS StudyOptionName,
    COUNT(e.Id) AS TotalStudents,
    SUM(CASE WHEN s.Gender = 1 THEN 1 ELSE 0 END) AS MaleCount,
    SUM(CASE WHEN s.Gender = 2 THEN 1 ELSE 0 END) AS FemaleCount
FROM ClassRooms cr
INNER JOIN AcademicYears ay ON ay.Id = cr.AcademicYearId AND ay.IsDeleted = 0
INNER JOIN Sections sec ON sec.Id = cr.SectionId AND sec.IsDeleted = 0
LEFT JOIN StudyOptions opt ON opt.Id = cr.StudyOptionId AND opt.IsDeleted = 0
LEFT JOIN Enrollments e ON e.ClassRoomId = cr.Id AND e.IsActive = 1 AND e.IsDeleted = 0
LEFT JOIN Students s ON s.Id = e.StudentId AND s.IsDeleted = 0
WHERE cr.IsDeleted = 0
GROUP BY cr.Id, cr.Code, cr.Name, ay.Id, ay.Label, sec.Name, opt.Name;
GO

IF OBJECT_ID(N'dbo.vw_MoyennesParClasse', N'V') IS NOT NULL
    DROP VIEW dbo.vw_MoyennesParClasse;
GO

CREATE VIEW dbo.vw_MoyennesParClasse
AS
SELECT
    pr.ClassRoomId,
    cr.Code AS ClassCode,
    cr.Name AS ClassName,
    pr.AcademicPeriodId,
    ap.Name AS PeriodName,
    COUNT(pr.Id) AS StudentCount,
    AVG(pr.Average) AS ClassAverage,
    MAX(pr.Average) AS MaxAverage,
    MIN(pr.Average) AS MinAverage,
    SUM(CASE WHEN pr.Average >= 10 THEN 1 ELSE 0 END) AS PassCount,
    SUM(CASE WHEN pr.Average < 10 THEN 1 ELSE 0 END) AS FailCount
FROM PeriodResults pr
INNER JOIN ClassRooms cr ON cr.Id = pr.ClassRoomId AND cr.IsDeleted = 0
INNER JOIN AcademicPeriods ap ON ap.Id = pr.AcademicPeriodId AND ap.IsDeleted = 0
WHERE pr.IsDeleted = 0
GROUP BY pr.ClassRoomId, cr.Code, cr.Name, pr.AcademicPeriodId, ap.Name;
GO

-- ============================================================
-- PROCÉDURES STOCKÉES
-- ============================================================

IF OBJECT_ID(N'dbo.sp_EnregistrerPaiement', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_EnregistrerPaiement;
GO

CREATE PROCEDURE dbo.sp_EnregistrerPaiement
    @PaymentId UNIQUEIDENTIFIER,
    @SchoolId UNIQUEIDENTIFIER,
    @StudentId UNIQUEIDENTIFIER,
    @AcademicYearId UNIQUEIDENTIFIER,
    @CashRegisterId UNIQUEIDENTIFIER,
    @BankId UNIQUEIDENTIFIER = NULL,
    @ReceiptNumber NVARCHAR(50),
    @TotalAmount DECIMAL(18, 2),
    @Currency INT,
    @PaymentMethod NVARCHAR(50),
    @ReceivedByUserId UNIQUEIDENTIFIER = NULL,
    @CreatedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM Payments WHERE ReceiptNumber = @ReceiptNumber AND IsDeleted = 0)
        BEGIN
            RAISERROR(N'Le numéro de reçu existe déjà.', 16, 1);
            RETURN;
        END

        INSERT INTO Payments (
            Id, SchoolId, StudentId, AcademicYearId, CashRegisterId, BankId,
            ReceiptNumber, PaymentDate, TotalAmount, Currency, Status, PaymentMethod,
            ReceivedByUserId, CreatedAt, CreatedBy, IsDeleted
        )
        VALUES (
            @PaymentId, @SchoolId, @StudentId, @AcademicYearId, @CashRegisterId, @BankId,
            @ReceiptNumber, SYSUTCDATETIME(), @TotalAmount, @Currency, 3, @PaymentMethod,
            @ReceivedByUserId, SYSUTCDATETIME(), @CreatedBy, 0
        );

        DECLARE @BalanceAfter DECIMAL(18, 2);
        SELECT @BalanceAfter = ISNULL(
            (SELECT TOP 1 BalanceAfter FROM CashMovements
             WHERE CashRegisterId = @CashRegisterId AND IsDeleted = 0
             ORDER BY MovementDate DESC), 0) + @TotalAmount;

        INSERT INTO CashMovements (
            Id, CashRegisterId, PaymentId, MovementDate, MovementType,
            Amount, Currency, BalanceAfter, Description, UserId,
            CreatedAt, CreatedBy, IsDeleted
        )
        VALUES (
            NEWID(), @CashRegisterId, @PaymentId, SYSUTCDATETIME(), N'IN',
            @TotalAmount, @Currency, @BalanceAfter, N'Paiement ' + @ReceiptNumber, @ReceivedByUserId,
            SYSUTCDATETIME(), @CreatedBy, 0
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_CalculerBulletin', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CalculerBulletin;
GO

CREATE PROCEDURE dbo.sp_CalculerBulletin
    @StudentId UNIQUEIDENTIFIER,
    @AcademicYearId UNIQUEIDENTIFIER,
    @AcademicPeriodId UNIQUEIDENTIFIER,
    @ClassRoomId UNIQUEIDENTIFIER,
    @CalculatedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Average DECIMAL(5, 2) = dbo.fn_CalculerMoyennePonderee(@StudentId, @AcademicPeriodId);
        DECLARE @Percentage DECIMAL(5, 2) = (@Average / 20.0) * 100.0;
        DECLARE @PeriodResultId UNIQUEIDENTIFIER = NEWID();
        DECLARE @ClassSize INT;
        DECLARE @Rank INT;

        SELECT @ClassSize = COUNT(*)
        FROM Enrollments
        WHERE ClassRoomId = @ClassRoomId AND AcademicYearId = @AcademicYearId AND IsActive = 1 AND IsDeleted = 0;

        IF EXISTS (
            SELECT 1 FROM PeriodResults
            WHERE StudentId = @StudentId AND AcademicPeriodId = @AcademicPeriodId AND IsDeleted = 0
        )
        BEGIN
            UPDATE PeriodResults
            SET Average = @Average,
                Percentage = @Percentage,
                ClassSize = @ClassSize,
                UpdatedAt = SYSUTCDATETIME(),
                UpdatedBy = @CalculatedBy
            WHERE StudentId = @StudentId AND AcademicPeriodId = @AcademicPeriodId AND IsDeleted = 0;

            SELECT @PeriodResultId = Id FROM PeriodResults
            WHERE StudentId = @StudentId AND AcademicPeriodId = @AcademicPeriodId AND IsDeleted = 0;
        END
        ELSE
        BEGIN
            INSERT INTO PeriodResults (
                Id, StudentId, AcademicYearId, AcademicPeriodId, ClassRoomId,
                Average, Percentage, Rank, ClassSize, CouncilDecision, IsPublished,
                CreatedAt, CreatedBy, IsDeleted
            )
            VALUES (
                @PeriodResultId, @StudentId, @AcademicYearId, @AcademicPeriodId, @ClassRoomId,
                @Average, @Percentage, 1, @ClassSize, 5, 0,
                SYSUTCDATETIME(), @CalculatedBy, 0
            );
        END

        -- Recalcul des rangs pour la classe
        ;WITH Ranked AS (
            SELECT Id, RANK() OVER (ORDER BY Average DESC) AS NewRank
            FROM PeriodResults
            WHERE ClassRoomId = @ClassRoomId AND AcademicPeriodId = @AcademicPeriodId AND IsDeleted = 0
        )
        UPDATE pr
        SET pr.Rank = r.NewRank
        FROM PeriodResults pr
        INNER JOIN Ranked r ON r.Id = pr.Id;

        COMMIT TRANSACTION;

        SELECT @PeriodResultId AS PeriodResultId, @Average AS Average, @Percentage AS Percentage;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

IF OBJECT_ID(N'dbo.sp_ClotureAnneeScolaire', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_ClotureAnneeScolaire;
GO

CREATE PROCEDURE dbo.sp_ClotureAnneeScolaire
    @AcademicYearId UNIQUEIDENTIFIER,
    @ClosedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM AcademicYears WHERE Id = @AcademicYearId AND IsDeleted = 0)
        BEGIN
            RAISERROR(N'Année scolaire introuvable.', 16, 1);
            RETURN;
        END

        UPDATE AcademicPeriods
        SET IsClosed = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ClosedBy
        WHERE AcademicYearId = @AcademicYearId AND IsDeleted = 0;

        UPDATE Evaluations
        SET IsOpen = 0, IsPublished = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ClosedBy
        WHERE AcademicYearId = @AcademicYearId AND IsDeleted = 0;

        UPDATE AcademicYears
        SET IsClosed = 1, IsCurrent = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ClosedBy
        WHERE Id = @AcademicYearId AND IsDeleted = 0;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

PRINT N'Vues, fonctions et procédures stockées créées avec succès.';
