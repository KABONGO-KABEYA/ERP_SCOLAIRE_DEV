using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.RegistrationNumbers;
using Xunit;

namespace SchoolManagement.UnitTests.EnrollmentWizard;

/// <summary>
/// P1 — atomicité SQL : rollback d'ensemble métier + intégration compteur P4 en TX ambiante.
/// </summary>
[Trait("Category", "LiveSql")]
public sealed class EnrollmentCompleteTransactionSqlTests
{
    private const string ConnectionString =
        "Server=localhost\\HEROS_SQL19;Database=SchoolManagementRDC_Development;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";

    [Fact]
    public async Task T02_RollbackAfterStudentInsert_LeavesNoOrphanStudent()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolId = await db.Schools.IgnoreQueryFilters().Select(s => s.Id).FirstAsync();
        var studentId = Guid.NewGuid();
        var marker = $"P1RB-{studentId:N}"[..20];

        var uow = new UnitOfWork(db);
        var thrown = false;
        try
        {
            await uow.ExecuteInTransactionAsync(async ct =>
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO Students
                        (Id, SchoolId, RegistrationNumber, FirstName, LastName, Gender, DateOfBirth,
                         CreatedAt, IsDeleted, IsArchived)
                    VALUES
                        ({0}, {1}, {2}, N'Test', N'Rollback', 1, '2015-01-01', SYSUTCDATETIME(), 0, 0);
                    """,
                    studentId, schoolId, marker);

                // Simule une erreur après création Student (frais / enrollment / …).
                throw new InvalidOperationException("P1-forced-failure-after-student");
            }, CancellationToken.None);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("P1-forced-failure", StringComparison.Ordinal))
        {
            thrown = true;
        }

        thrown.Should().BeTrue();
        var exists = await db.Students.IgnoreQueryFilters()
            .AnyAsync(s => s.Id == studentId);
        exists.Should().BeFalse("le Student créé dans la TX doit disparaître après ROLLBACK");
    }

    [Fact]
    public async Task T04_RollbackAfterStudentAndEnrollment_LeavesNeither()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolId = await db.Schools.IgnoreQueryFilters().Select(s => s.Id).FirstAsync();
        var yearId = await db.AcademicYears.IgnoreQueryFilters()
            .Where(y => y.SchoolId == schoolId)
            .Select(y => y.Id)
            .FirstOrDefaultAsync();
        var classId = await db.ClassRooms.IgnoreQueryFilters()
            .Where(c => c.SchoolId == schoolId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
        if (yearId == Guid.Empty || classId == Guid.Empty)
        {
            return;
        }

        var categoryId = await db.Set<FeePricingCategory>()
            .IgnoreQueryFilters()
            .Where(c => c.SchoolId == schoolId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
        if (categoryId == Guid.Empty)
        {
            return;
        }

        var studentId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var marker = $"P1EN-{studentId:N}"[..20];

        var uow = new UnitOfWork(db);
        try
        {
            await uow.ExecuteInTransactionAsync(async ct =>
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO Students
                        (Id, SchoolId, RegistrationNumber, FirstName, LastName, Gender, DateOfBirth,
                         CreatedAt, IsDeleted, IsArchived)
                    VALUES
                        ({0}, {1}, {2}, N'Test', N'EnrollRb', 1, '2015-01-01', SYSUTCDATETIME(), 0, 0);
                    """,
                    studentId, schoolId, marker);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO Enrollments
                        (Id, StudentId, AcademicYearId, ClassRoomId, FeePricingCategoryId,
                         EnrollmentDate, Status, IsActive, CreatedAt, IsDeleted)
                    VALUES
                        ({0}, {1}, {2}, {3}, {4},
                         '2026-01-15', 1, 1, SYSUTCDATETIME(), 0);
                    """,
                    enrollmentId, studentId, yearId, classId, categoryId);

                throw new InvalidOperationException("P1-forced-failure-after-enrollment");
            }, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        (await db.Students.IgnoreQueryFilters().AnyAsync(s => s.Id == studentId)).Should().BeFalse();
        (await db.Enrollments.IgnoreQueryFilters().AnyAsync(e => e.Id == enrollmentId)).Should().BeFalse();
    }

    [Fact]
    public async Task T09_ExistingStudent_RollbackKeepsHistoricalRow()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var existing = await db.Students.IgnoreQueryFilters()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync();
        if (existing is null)
        {
            return;
        }

        var originalFirstName = existing.FirstName;
        var studentId = existing.Id;
        var enrollmentId = Guid.NewGuid();

        var yearId = await db.AcademicYears.IgnoreQueryFilters()
            .Where(y => y.SchoolId == existing.SchoolId)
            .Select(y => y.Id)
            .FirstOrDefaultAsync();
        var classId = await db.ClassRooms.IgnoreQueryFilters()
            .Where(c => c.SchoolId == existing.SchoolId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
        var categoryId = await db.Set<FeePricingCategory>()
            .IgnoreQueryFilters()
            .Where(c => c.SchoolId == existing.SchoolId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
        if (yearId == Guid.Empty || classId == Guid.Empty || categoryId == Guid.Empty)
        {
            return;
        }

        var uow = new UnitOfWork(db);
        try
        {
            await uow.ExecuteInTransactionAsync(async ct =>
            {
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Students SET FirstName = N'P1TEMP' WHERE Id = {0}",
                    studentId);

                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO Enrollments
                        (Id, StudentId, AcademicYearId, ClassRoomId, FeePricingCategoryId,
                         EnrollmentDate, Status, IsActive, CreatedAt, IsDeleted)
                    VALUES
                        ({0}, {1}, {2}, {3}, {4},
                         '2026-01-15', 1, 1, SYSUTCDATETIME(), 0);
                    """,
                    enrollmentId, studentId, yearId, classId, categoryId);

                throw new InvalidOperationException("P1-forced-failure-reinscription");
            }, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        db.ChangeTracker.Clear();
        var reloaded = await db.Students.IgnoreQueryFilters().FirstAsync(s => s.Id == studentId);
        reloaded.FirstName.Should().Be(originalFirstName);
        (await db.Enrollments.IgnoreQueryFilters().AnyAsync(e => e.Id == enrollmentId)).Should().BeFalse();
    }

    [Fact]
    public async Task P4_Allocator_InAmbientTransaction_RollsBackCounterOnFailure()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        if (!await TableExistsAsync(db, "RegistrationNumberCounters"))
        {
            return;
        }

        var schoolId = await db.Schools.IgnoreQueryFilters().Select(s => s.Id).FirstAsync();
        const int year = 2088;

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM RegistrationNumberCounters WHERE SchoolId = {0} AND [Year] = {1};
            INSERT INTO RegistrationNumberCounters
                (Id, SchoolId, [Year], NextValue, CreatedAt, IsDeleted)
            VALUES
                ({2}, {0}, {1}, 10, SYSUTCDATETIME(), 0);
            """,
            schoolId, year, Guid.NewGuid());

        var uow = new UnitOfWork(db);
        var allocator = new RegistrationNumberAllocator(db);

        try
        {
            await uow.ExecuteInTransactionAsync(async ct =>
            {
                var allocated = await allocator.AllocateAsync(schoolId, year, ct);
                allocated.Should().Be("ELV-2088-00010");
                throw new InvalidOperationException("P1-forced-failure-after-allocate");
            }, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        db.ChangeTracker.Clear();
        var next = await db.RegistrationNumberCounters.IgnoreQueryFilters()
            .Where(c => c.SchoolId == schoolId && c.Year == year && !c.IsDeleted)
            .Select(c => c.NextValue)
            .SingleAsync();
        next.Should().Be(10, "l'incrément compteur P4 doit rollback avec la TX P1");
    }

    [Fact]
    public async Task P4_Allocator_InAmbientTransaction_CommitsCounterOnSuccess()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        if (!await TableExistsAsync(db, "RegistrationNumberCounters"))
        {
            return;
        }

        var schoolId = await db.Schools.IgnoreQueryFilters().Select(s => s.Id).FirstAsync();
        const int year = 2087;

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM RegistrationNumberCounters WHERE SchoolId = {0} AND [Year] = {1};
            INSERT INTO RegistrationNumberCounters
                (Id, SchoolId, [Year], NextValue, CreatedAt, IsDeleted)
            VALUES
                ({2}, {0}, {1}, 3, SYSUTCDATETIME(), 0);
            """,
            schoolId, year, Guid.NewGuid());

        var uow = new UnitOfWork(db);
        var allocator = new RegistrationNumberAllocator(db);

        await uow.ExecuteInTransactionAsync(async ct =>
        {
            (await allocator.AllocateAsync(schoolId, year, ct)).Should().Be("ELV-2087-00003");
            (await allocator.AllocateAsync(schoolId, year, ct)).Should().Be("ELV-2087-00004");
        }, CancellationToken.None);

        db.ChangeTracker.Clear();
        var next = await db.RegistrationNumberCounters.IgnoreQueryFilters()
            .Where(c => c.SchoolId == schoolId && c.Year == year && !c.IsDeleted)
            .Select(c => c.NextValue)
            .SingleAsync();
        next.Should().Be(5);
    }

    [Fact]
    public async Task T08_TwoParallelAmbientAllocations_ProduceDistinctMatricules()
    {
        await using var probe = await CreateDbOrSkipAsync();
        if (probe is null || !await TableExistsAsync(probe, "RegistrationNumberCounters"))
        {
            return;
        }

        var schoolId = await probe.Schools.IgnoreQueryFilters().Select(s => s.Id).FirstAsync();
        const int year = 2086;
        await probe.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM RegistrationNumberCounters WHERE SchoolId = {0} AND [Year] = {1};
            INSERT INTO RegistrationNumberCounters
                (Id, SchoolId, [Year], NextValue, CreatedAt, IsDeleted)
            VALUES
                ({2}, {0}, {1}, 1, SYSUTCDATETIME(), 0);
            """,
            schoolId, year, Guid.NewGuid());

        string? a = null;
        string? b = null;
        await Task.WhenAll(
            Task.Run(async () =>
            {
                await using var db = CreateContext();
                var uow = new UnitOfWork(db);
                await uow.ExecuteInTransactionAsync(async ct =>
                {
                    a = await new RegistrationNumberAllocator(db).AllocateAsync(schoolId, year, ct);
                }, CancellationToken.None);
            }),
            Task.Run(async () =>
            {
                await using var db = CreateContext();
                var uow = new UnitOfWork(db);
                await uow.ExecuteInTransactionAsync(async ct =>
                {
                    b = await new RegistrationNumberAllocator(db).AllocateAsync(schoolId, year, ct);
                }, CancellationToken.None);
            }));

        a.Should().NotBeNullOrWhiteSpace();
        b.Should().NotBeNullOrWhiteSpace();
        a.Should().NotBe(b);
    }

    private static SchoolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure(3))
            .Options;
        return new SchoolDbContext(options) { IgnoreSchoolScope = true };
    }

    private static async Task<SchoolDbContext?> CreateDbOrSkipAsync()
    {
        try
        {
            var db = CreateContext();
            if (!await db.Database.CanConnectAsync())
            {
                return null;
            }

            return db;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TableExistsAsync(SchoolDbContext db, string tableName)
    {
        var exists = await db.Database
            .SqlQueryRaw<int>(
                $"SELECT CAST(CASE WHEN OBJECT_ID(N'dbo.{tableName}') IS NULL THEN 0 ELSE 1 END AS int) AS [Value]")
            .SingleAsync();
        return exists == 1;
    }
}
