using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.EnrollmentWizard;
using SchoolManagement.Infrastructure.RegistrationNumbers;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.EnrollmentWizard;

/// <summary>
/// Tests d'allocation contre SQL Server (verrous UPDLOCK).
/// Skip si la base Dev n'est pas joignable.
/// </summary>
[Trait("Category", "LiveSql")]
public sealed class RegistrationNumberAllocatorSqlTests
{
    private const string ConnectionString =
        "Server=localhost\\HEROS_SQL19;Database=SchoolManagementRDC_Development;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";

    [Fact]
    public async Task Allocate_Sequential_ProducesDistinctNumbers()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolId = await EnsureTestSchoolAsync(db);
        var year = 2099;
        await ResetCounterAsync(db, schoolId, year, nextValue: 1);

        var allocator = new RegistrationNumberAllocator(db);
        var a = await allocator.AllocateAsync(schoolId, year);
        var b = await allocator.AllocateAsync(schoolId, year);
        var c = await allocator.AllocateAsync(schoolId, year);

        a.Should().Be("ELV-2099-00001");
        b.Should().Be("ELV-2099-00002");
        c.Should().Be("ELV-2099-00003");
    }

    [Fact]
    public async Task Allocate_Concurrent_TenCalls_ProduceTenDistinct()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolId = await EnsureTestSchoolAsync(db);
        var year = 2098;
        await ResetCounterAsync(db, schoolId, year, nextValue: 1);

        var results = new string[10];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            new ParallelOptions { MaxDegreeOfParallelism = 10 },
            async (i, ct) =>
            {
                await using var scoped = CreateContext();
                scoped.IgnoreSchoolScope = true;
                var allocator = new RegistrationNumberAllocator(scoped);
                results[i] = await allocator.AllocateAsync(schoolId, year, ct);
            });

        results.Should().OnlyHaveUniqueItems();
        results.Should().AllSatisfy(r => r.Should().StartWith("ELV-2098-"));
        results.Select(r =>
        {
            RegistrationNumberFormat.TryParse(r, out _, out var seq).Should().BeTrue();
            return seq;
        }).OrderBy(x => x).Should().Equal(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task Allocate_MobileAndDesktopSimultaneous_ProduceDistinct()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolId = await EnsureTestSchoolAsync(db);
        var year = 2097;
        await ResetCounterAsync(db, schoolId, year, nextValue: 1);

        string? mobile = null;
        string? desktop = null;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                await using var scoped = CreateContext();
                scoped.IgnoreSchoolScope = true;
                mobile = await new RegistrationNumberAllocator(scoped).AllocateAsync(schoolId, year);
            }),
            Task.Run(async () =>
            {
                await using var scoped = CreateContext();
                scoped.IgnoreSchoolScope = true;
                desktop = await new RegistrationNumberAllocator(scoped).AllocateAsync(schoolId, year);
            }));

        mobile.Should().NotBeNullOrWhiteSpace();
        desktop.Should().NotBeNullOrWhiteSpace();
        mobile.Should().NotBe(desktop);
    }

    [Fact]
    public async Task Preview_DoesNotConsumeCounter()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolId = await EnsureTestSchoolAsync(db);
        var year = 2096;
        await ResetCounterAsync(db, schoolId, year, nextValue: 7);

        var allocator = new RegistrationNumberAllocator(db);
        var p1 = await allocator.PreviewNextAsync(schoolId, year);
        var p2 = await allocator.PreviewNextAsync(schoolId, year);
        var allocated = await allocator.AllocateAsync(schoolId, year);

        p1.Should().Be("ELV-2096-00007");
        p2.Should().Be("ELV-2096-00007");
        allocated.Should().Be("ELV-2096-00007");
        (await allocator.PreviewNextAsync(schoolId, year)).Should().Be("ELV-2096-00008");
    }

    [Fact]
    public async Task Allocate_TwoSchools_IndependentSequences()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolIds = await db.Schools.IgnoreQueryFilters().Select(s => s.Id).Take(2).ToListAsync();
        if (schoolIds.Count < 2)
        {
            // Environnement mono-école : vérifier l'indépendance par année (même règle métier de préfixe).
            var schoolId = schoolIds[0];
            await ResetCounterAsync(db, schoolId, 2095, nextValue: 1);
            await ResetCounterAsync(db, schoolId, 2094, nextValue: 1);
            var allocator = new RegistrationNumberAllocator(db);
            var y2095 = await allocator.AllocateAsync(schoolId, 2095);
            var y2094 = await allocator.AllocateAsync(schoolId, 2094);
            y2095.Should().Be("ELV-2095-00001");
            y2094.Should().Be("ELV-2094-00001");
            return;
        }

        var year = 2095;
        await ResetCounterAsync(db, schoolIds[0], year, nextValue: 1);
        await ResetCounterAsync(db, schoolIds[1], year, nextValue: 1);

        var allocatorMulti = new RegistrationNumberAllocator(db);
        var a = await allocatorMulti.AllocateAsync(schoolIds[0], year);
        var b = await allocatorMulti.AllocateAsync(schoolIds[1], year);

        a.Should().Be("ELV-2095-00001");
        b.Should().Be("ELV-2095-00001");
    }

    [Fact]
    public async Task Allocate_SeedFromExistingStudents_DoesNotReuseMaxIncludingDeleted()
    {
        await using var db = await CreateDbOrSkipAsync();
        if (db is null)
        {
            return;
        }

        var schoolId = await EnsureTestSchoolAsync(db);
        var year = 2093;

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM RegistrationNumberCounters WHERE SchoolId = {0} AND [Year] = {1};
            DELETE FROM Students WHERE SchoolId = {0} AND RegistrationNumber LIKE {2};
            """,
            schoolId, year, $"ELV-{year}-%");

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Students
                (Id, SchoolId, RegistrationNumber, FirstName, LastName, Gender, DateOfBirth,
                 CreatedAt, IsDeleted, IsArchived)
            VALUES
                ({0}, {1}, {2}, N'A', N'Active', 1, '2015-01-01', SYSUTCDATETIME(), 0, 0),
                ({3}, {1}, {4}, N'D', N'Deleted', 1, '2015-01-01', SYSUTCDATETIME(), 1, 0);
            """,
            Guid.NewGuid(),
            schoolId,
            $"ELV-{year}-00002",
            Guid.NewGuid(),
            $"ELV-{year}-00005");

        var allocator = new RegistrationNumberAllocator(db);
        var allocated = await allocator.AllocateAsync(schoolId, year);
        allocated.Should().Be($"ELV-{year}-00006");

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM RegistrationNumberCounters WHERE SchoolId = {0} AND [Year] = {1};
            DELETE FROM Students WHERE SchoolId = {0} AND RegistrationNumber LIKE {2};
            """,
            schoolId, year, $"ELV-{year}-%");
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
            // Ensure table exists (migration applied or create for test isolation via EnsureCreated is too broad).
            var canConnect = await db.Database.CanConnectAsync();
            if (!canConnect)
            {
                return null;
            }

            var exists = await db.Database
                .SqlQueryRaw<int>(
                    "SELECT CAST(CASE WHEN OBJECT_ID(N'dbo.RegistrationNumberCounters') IS NULL THEN 0 ELSE 1 END AS int) AS [Value]")
                .SingleAsync();
            if (exists == 0)
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

    private static async Task<Guid> EnsureTestSchoolAsync(SchoolDbContext db, string marker = "P4")
    {
        // Réutilise l'école Dev réelle pour FK Schools ; isole via Year artificiel (209x).
        var schoolId = await db.Schools
            .IgnoreQueryFilters()
            .Select(s => s.Id)
            .FirstAsync();
        _ = marker;
        return schoolId;
    }

    private static async Task ResetCounterAsync(SchoolDbContext db, Guid schoolId, int year, int nextValue)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM RegistrationNumberCounters WHERE SchoolId = {0} AND [Year] = {1};
            INSERT INTO RegistrationNumberCounters
                (Id, SchoolId, [Year], NextValue, CreatedAt, IsDeleted)
            VALUES
                ({2}, {0}, {1}, {3}, SYSUTCDATETIME(), 0);
            """,
            schoolId, year, Guid.NewGuid(), nextValue);
    }
}
