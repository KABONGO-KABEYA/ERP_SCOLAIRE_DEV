namespace SchoolManagement.IntegrationTests.MultiTenant;

using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Deliberation;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Hr;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Crée un jeu de données complet et autonome pour une école de test, puis le supprime.
/// Écrit avec <c>IgnoreSchoolScope</c> : c'est le seul contexte autorisé à franchir les filtres tenant.
/// </summary>
internal static class MultiTenantSeeder
{
    internal static async Task<TenantTestSchool> SeedAsync(
        SchoolDbContext context,
        string marker,
        Func<Guid, Guid, string> tokenFactory,
        CancellationToken cancellationToken = default)
    {
        context.IgnoreSchoolScope = true;
        context.SuppressCloudSyncEnqueue = true;

        var school = new School
        {
            Name = $"Ecole {marker}",
            LegalName = $"Ecole {marker}",
            City = "Kinshasa",
            IsActive = true
        };
        context.Schools.Add(school);
        await context.SaveChangesAsync(cancellationToken);

        var year = new AcademicYear
        {
            SchoolId = school.Id,
            Label = $"2025-2026 {marker}",
            StartDate = new DateOnly(2025, 9, 1),
            EndDate = new DateOnly(2026, 6, 30),
            IsCurrent = true
        };
        context.AcademicYears.Add(year);
        await context.SaveChangesAsync(cancellationToken);

        var mainPeriod = new AcademicMainPeriod
        {
            SchoolId = school.Id,
            AcademicYearId = year.Id,
            CycleGroup = PedagogicalCycleGroup.MaternellePrimaire,
            Name = $"Trimestre {marker}",
            PeriodType = AcademicPeriodType.Trimestre,
            OrderIndex = 1
        };
        context.AcademicMainPeriods.Add(mainPeriod);
        await context.SaveChangesAsync(cancellationToken);

        var period = new AcademicPeriod
        {
            SchoolId = school.Id,
            AcademicYearId = year.Id,
            MainPeriodId = mainPeriod.Id,
            Name = $"P1 {marker}",
            PeriodType = AcademicPeriodType.Trimestre,
            OrderIndex = 1,
            Status = AcademicSubPeriodStatus.Ouverte
        };
        context.AcademicPeriods.Add(period);

        var section = new Section
        {
            SchoolId = school.Id,
            Code = $"{marker}S",
            Name = $"Section {marker}",
            Cycle = EducationCycle.Primaire
        };
        context.Sections.Add(section);

        var pedagogicalClass = new PedagogicalClass
        {
            SchoolId = school.Id,
            TemplateCode = $"{marker}-PRI-1",
            Program = SchoolProgram.Primaire,
            LevelOrder = 1,
            DisplayName = $"1ere primaire {marker}",
            IsEnabled = true
        };
        context.PedagogicalClasses.Add(pedagogicalClass);
        await context.SaveChangesAsync(cancellationToken);

        var classRoom = new ClassRoom
        {
            SchoolId = school.Id,
            AcademicYearId = year.Id,
            PedagogicalClassId = pedagogicalClass.Id,
            SectionId = section.Id,
            Code = $"{marker}-1A",
            Name = $"A {marker}",
            Level = 1,
            IsActive = true
        };
        context.ClassRooms.Add(classRoom);

        var course = new Course
        {
            SchoolId = school.Id,
            Code = $"{marker}-MATH",
            Name = $"Mathematiques {marker}",
            Coefficient = 1,
            MaxScore = 20
        };
        context.Courses.Add(course);

        // Les cours ne sont visibles par une école que via ce lien (voir SchoolCourseScope).
        context.PedagogicalClassCourses.Add(new PedagogicalClassCourse
        {
            SchoolId = school.Id,
            PedagogicalClassId = pedagogicalClass.Id,
            CourseId = course.Id,
            MaxScore = 20
        });

        var pricingCategory = new FeePricingCategory
        {
            SchoolId = school.Id,
            Code = $"{marker}G",
            Name = $"General {marker}",
            IsActive = true
        };
        context.FeePricingCategories.Add(pricingCategory);

        var feeType = new FeeType
        {
            SchoolId = school.Id,
            Code = $"{marker}F",
            Name = $"Frais scolaire {marker}",
            Currency = Currency.CDF,
            IsMandatory = true,
            IsActive = true
        };
        context.FeeTypes.Add(feeType);

        var installment = new FeeInstallment
        {
            SchoolId = school.Id,
            Name = $"Tranche 1 {marker}",
            SortOrder = 1,
            IsActive = true
        };
        context.FeeInstallments.Add(installment);

        var evaluationType = new EvaluationTypeDefinition
        {
            SchoolId = school.Id,
            Code = $"{marker}I",
            Name = $"Interrogation {marker}",
            IsActive = true
        };
        context.EvaluationTypes.Add(evaluationType);

        var teacher = new Teacher
        {
            SchoolId = school.Id,
            EmployeeNumber = $"{marker}-T1",
            FirstName = "Prof",
            LastName = marker,
            IsActive = true
        };
        context.Teachers.Add(teacher);

        var mention = new ResultMentionDefinition
        {
            SchoolId = school.Id,
            Label = $"Satisfaction {marker}",
            MinPercentageInclusive = 50,
            MaxPercentageInclusive = 69,
            SortOrder = 1,
            IsActive = true
        };
        context.ResultMentionDefinitions.Add(mention);

        var department = new HrDepartment
        {
            SchoolId = school.Id,
            Code = $"{marker}D",
            Name = $"Direction {marker}",
            IsActive = true
        };
        context.HrDepartments.Add(department);
        await context.SaveChangesAsync(cancellationToken);

        var jobFunction = new HrJobFunction
        {
            SchoolId = school.Id,
            DepartmentId = department.Id,
            Name = $"Enseignant {marker}",
            IsActive = true
        };
        context.HrJobFunctions.Add(jobFunction);

        var personnelProfile = new PersonnelHrProfile
        {
            SchoolId = school.Id,
            TeacherId = teacher.Id,
            Category = PersonnelCategory.Enseignant,
            DepartmentId = department.Id,
            Status = PersonnelStatus.Actif
        };
        context.PersonnelHrProfiles.Add(personnelProfile);

        var student = new Student
        {
            SchoolId = school.Id,
            RegistrationNumber = $"{marker}-0001",
            FirstName = "Eleve",
            LastName = marker,
            Gender = Gender.Masculin,
            DateOfBirth = new DateOnly(2014, 5, 12)
        };
        context.Students.Add(student);

        var cardTemplate = new CardTemplate
        {
            SchoolId = school.Id,
            Name = $"Modele {marker}",
            Kind = CardTemplateKind.Eleve,
            IsActive = true
        };
        context.CardTemplates.Add(cardTemplate);

        var user = new UserAccount
        {
            SchoolId = school.Id,
            UserName = $"user-{marker}".ToLowerInvariant(),
            Email = $"user-{marker}@test.local".ToLowerInvariant(),
            PasswordHash = "$2a$11$0000000000000000000000000000000000000000000000000000",
            FirstName = "Utilisateur",
            LastName = marker,
            IsActive = true
        };
        context.UserAccounts.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        var courseAssignment = new CourseAssignment
        {
            TeacherId = teacher.Id,
            CourseId = course.Id,
            ClassRoomId = classRoom.Id,
            AcademicYearId = year.Id,
            PedagogicalClassId = pedagogicalClass.Id,
            IsActive = true,
            MaxScore = 20,
            WeeklyHours = 4
        };
        context.CourseAssignments.Add(courseAssignment);

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            AcademicYearId = year.Id,
            ClassRoomId = classRoom.Id,
            FeePricingCategoryId = pricingCategory.Id,
            Status = EnrollmentStatus.Inscrit,
            EnrollmentDate = new DateOnly(2025, 9, 5),
            IsActive = true
        };
        context.Enrollments.Add(enrollment);

        var document = new StudentDocument
        {
            StudentId = student.Id,
            DocumentType = "Bulletin",
            FileName = $"doc-{marker}.pdf",
            StoragePath = $"tests/{marker}/doc.pdf",
            MimeType = "application/pdf",
            FileSizeBytes = 1024
        };
        context.StudentDocuments.Add(document);

        var studentCard = new StudentCard
        {
            SchoolId = school.Id,
            StudentId = student.Id,
            AcademicYearId = year.Id,
            TemplateId = cardTemplate.Id,
            CardNumber = $"{marker}-CARD-1",
            QrToken = $"{marker}-QR-TOKEN",
            Status = StudentCardStatus.Active,
            IssuedAt = DateTime.UtcNow
        };
        context.StudentCards.Add(studentCard);

        var payment = new Payment
        {
            SchoolId = school.Id,
            StudentId = student.Id,
            AcademicYearId = year.Id,
            ReceiptNumber = $"{marker}-REC-1",
            PaymentDate = DateTime.UtcNow,
            TotalAmount = 50000m,
            Currency = Currency.CDF,
            Status = PaymentStatus.Complet
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);

        context.PaymentLines.Add(new PaymentLine
        {
            PaymentId = payment.Id,
            FeeTypeId = feeType.Id,
            Amount = 50000m,
            Currency = Currency.CDF,
            Description = $"Ligne {marker}"
        });

        var evaluation = new Evaluation
        {
            AcademicYearId = year.Id,
            AcademicPeriodId = period.Id,
            CourseAssignmentId = courseAssignment.Id,
            EvaluationTypeId = evaluationType.Id,
            CourseId = course.Id,
            ClassRoomId = classRoom.Id,
            Title = $"Interro {marker}",
            EvaluationDate = new DateOnly(2025, 10, 15),
            MaxScore = 20,
            IsOpen = true
        };
        context.Evaluations.Add(evaluation);
        await context.SaveChangesAsync(cancellationToken);

        context.GradeEntries.Add(new GradeEntry
        {
            EvaluationId = evaluation.Id,
            StudentId = student.Id,
            Score = 15m,
            Comment = $"Note {marker}"
        });
        await context.SaveChangesAsync(cancellationToken);

        return new TenantTestSchool
        {
            Marker = marker,
            JwtToken = tokenFactory(user.Id, school.Id),
            SchoolId = school.Id,
            AcademicYearId = year.Id,
            AcademicPeriodId = period.Id,
            SectionId = section.Id,
            PedagogicalClassId = pedagogicalClass.Id,
            ClassRoomId = classRoom.Id,
            CourseId = course.Id,
            StudentId = student.Id,
            EnrollmentId = enrollment.Id,
            StudentDocumentId = document.Id,
            TeacherId = teacher.Id,
            CourseAssignmentId = courseAssignment.Id,
            EvaluationId = evaluation.Id,
            PaymentId = payment.Id,
            FeeTypeId = feeType.Id,
            PricingCategoryId = pricingCategory.Id,
            FeeInstallmentId = installment.Id,
            CardTemplateId = cardTemplate.Id,
            StudentCardId = studentCard.Id,
            UserId = user.Id,
            PersonnelProfileId = personnelProfile.Id,
            MentionId = mention.Id
        };
    }

    /// <summary>Supprime physiquement les données de test (ordre inverse des dépendances FK).</summary>
    internal static async Task CleanupAsync(
        SchoolDbContext context,
        IEnumerable<Guid> schoolIds,
        CancellationToken cancellationToken = default)
    {
        context.IgnoreSchoolScope = true;

        foreach (var schoolId in schoolIds)
        {
            foreach (var statement in CleanupStatements)
            {
                await context.Database.ExecuteSqlRawAsync(
                    statement,
                    [new Microsoft.Data.SqlClient.SqlParameter("@schoolId", schoolId)],
                    cancellationToken);
            }
        }
    }

    private static readonly string[] CleanupStatements =
    [
        "DELETE ge FROM GradeEntries ge INNER JOIN Students s ON s.Id = ge.StudentId WHERE s.SchoolId = @schoolId",
        "DELETE e FROM Evaluations e INNER JOIN ClassRooms c ON c.Id = e.ClassRoomId WHERE c.SchoolId = @schoolId",
        "DELETE ca FROM CourseAssignments ca INNER JOIN ClassRooms c ON c.Id = ca.ClassRoomId WHERE c.SchoolId = @schoolId",
        "DELETE pl FROM PaymentLines pl INNER JOIN Payments p ON p.Id = pl.PaymentId WHERE p.SchoolId = @schoolId",
        "DELETE FROM StudentFeeBalances WHERE StudentId IN (SELECT Id FROM Students WHERE SchoolId = @schoolId)",
        "DELETE FROM Payments WHERE SchoolId = @schoolId",
        "DELETE FROM CashMovements WHERE SchoolId = @schoolId",
        "DELETE FROM CarteHistorique WHERE CardId IN (SELECT Id FROM Carte WHERE SchoolId = @schoolId)",
        "DELETE FROM CarteImpression WHERE CardId IN (SELECT Id FROM Carte WHERE SchoolId = @schoolId)",
        "DELETE FROM Carte WHERE SchoolId = @schoolId",
        "DELETE FROM CarteModele WHERE SchoolId = @schoolId",
        "DELETE FROM CarteParametres WHERE SchoolId = @schoolId",
        "DELETE FROM StudentDocuments WHERE StudentId IN (SELECT Id FROM Students WHERE SchoolId = @schoolId)",
        "DELETE FROM StudentGuardians WHERE StudentId IN (SELECT Id FROM Students WHERE SchoolId = @schoolId)",
        "DELETE FROM StudentStatusHistory WHERE StudentId IN (SELECT Id FROM Students WHERE SchoolId = @schoolId)",
        "DELETE eh FROM EnrollmentPricingCategoryHistory eh INNER JOIN Enrollments en ON en.Id = eh.EnrollmentId "
            + "INNER JOIN Students s ON s.Id = en.StudentId WHERE s.SchoolId = @schoolId",
        "DELETE en FROM Enrollments en INNER JOIN Students s ON s.Id = en.StudentId WHERE s.SchoolId = @schoolId",
        "DELETE FROM Students WHERE SchoolId = @schoolId",
        "DELETE FROM PersonnelHrProfiles WHERE SchoolId = @schoolId",
        "DELETE FROM HrJobFunctions WHERE SchoolId = @schoolId",
        "DELETE FROM HrDepartments WHERE SchoolId = @schoolId",
        "DELETE FROM Teachers WHERE SchoolId = @schoolId",
        "DELETE FROM ClassFeeAmounts WHERE SchoolId = @schoolId",
        "DELETE FROM FeeTypeInstallments WHERE SchoolId = @schoolId",
        "DELETE FROM FeeInstallments WHERE SchoolId = @schoolId",
        "DELETE FROM FeeTypes WHERE SchoolId = @schoolId",
        "DELETE FROM FeePricingCategories WHERE SchoolId = @schoolId",
        "DELETE FROM ResultMentionDefinitions WHERE SchoolId = @schoolId",
        "DELETE FROM EvaluationTypes WHERE SchoolId = @schoolId",
        "DELETE FROM ClassRooms WHERE SchoolId = @schoolId",
        "DELETE FROM PedagogicalClassCourses WHERE SchoolId = @schoolId",
        "DELETE FROM Courses WHERE SchoolId = @schoolId",
        "DELETE FROM PedagogicalClasses WHERE SchoolId = @schoolId",
        "DELETE FROM Sections WHERE SchoolId = @schoolId",
        "DELETE FROM AcademicPeriods WHERE SchoolId = @schoolId",
        "DELETE FROM AcademicMainPeriods WHERE SchoolId = @schoolId",
        "DELETE FROM AcademicYears WHERE SchoolId = @schoolId",
        "DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM UserAccounts WHERE SchoolId = @schoolId)",
        "DELETE FROM UserRoleAssignments WHERE UserId IN (SELECT Id FROM UserAccounts WHERE SchoolId = @schoolId)",
        "DELETE FROM UserAccounts WHERE SchoolId = @schoolId",
        "DELETE FROM RolePermissions WHERE RoleId IN (SELECT Id FROM Roles WHERE SchoolId = @schoolId)",
        "DELETE FROM Roles WHERE SchoolId = @schoolId",
        "DELETE FROM AuditEntries WHERE SchoolId = @schoolId",
        "DELETE FROM LoginHistory WHERE SchoolId = @schoolId",
        "DELETE FROM SyncOutboxItem WHERE UnitId IN (SELECT Id FROM SyncOutboxUnit WHERE SchoolId = @schoolId)",
        "DELETE FROM SyncOutboxUnit WHERE SchoolId = @schoolId",
        "DELETE FROM SyncJournal WHERE SchoolId = @schoolId",
        "DELETE FROM SyncWatermark WHERE SchoolId = @schoolId",
        "DELETE FROM Schools WHERE Id = @schoolId"
    ];
}
