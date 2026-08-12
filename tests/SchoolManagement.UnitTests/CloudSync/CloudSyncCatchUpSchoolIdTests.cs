using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

/// <summary>
/// Vérifie que le filtre SchoolId du catch-up s'appuie sur les métadonnées EF
/// (et non la réflexion CLR) — corrige PeriodResults sans colonne mappée.
/// </summary>
public sealed class CloudSyncCatchUpSchoolIdTests
{
    [Theory]
    [InlineData(typeof(DisciplineRecord), true)]
    [InlineData(typeof(MeritRecord), true)]
    [InlineData(typeof(PeriodResult), false)]
    public void CatchUp_uses_ef_mapped_SchoolId_not_clr_property(Type entityType, bool expectsMappedSchoolId)
    {
        using var ctx = CreateContext();

        var mapped = HasMappedSchoolId(ctx, entityType);
        mapped.Should().Be(expectsMappedSchoolId);

        var clrHasSchoolId = entityType.GetProperty("SchoolId")?.PropertyType == typeof(Guid);
        if (entityType == typeof(PeriodResult))
        {
            clrHasSchoolId.Should().BeTrue("PeriodResult possède SchoolId CLR mais ignoré par EF");
            mapped.Should().BeFalse("le catch-up ne doit pas filtrer PeriodResults par SchoolId direct");
        }
    }

    [Fact]
    public async Task CatchUp_query_DisciplineRecords_does_not_throw_when_SchoolId_is_mapped()
    {
        var schoolId = await SeedCatchUpFixturesAsync();
        await using var ctx = CreateContext();
        ConfigureSyncContext(ctx, schoolId);

        var act = () => QueryCatchUpChangesAsync<DisciplineRecord>(ctx, schoolId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CatchUp_query_MeritRecords_does_not_throw_when_SchoolId_is_mapped()
    {
        var schoolId = await SeedCatchUpFixturesAsync();
        await using var ctx = CreateContext();
        ConfigureSyncContext(ctx, schoolId);

        var act = () => QueryCatchUpChangesAsync<MeritRecord>(ctx, schoolId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CatchUp_query_PeriodResults_does_not_throw_without_mapped_SchoolId()
    {
        var schoolId = await SeedCatchUpFixturesAsync();
        await using var ctx = CreateContext();
        ConfigureSyncContext(ctx, schoolId);

        var act = () => QueryCatchUpChangesAsync<PeriodResult>(ctx, schoolId);
        await act.Should().NotThrowAsync();
    }

    private static SchoolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SchoolDbContext(options);
    }

    private static void ConfigureSyncContext(SchoolDbContext ctx, Guid schoolId)
    {
        ctx.SuppressCloudSyncEnqueue = true;
        ctx.IgnoreSchoolScope = true;
        ctx.OverrideTenantSchoolId = schoolId;
    }

    private static bool HasMappedSchoolId(SchoolDbContext ctx, Type entityType) =>
        entityType != typeof(School)
        && ctx.Model.FindEntityType(entityType)?.FindProperty("SchoolId")?.ClrType == typeof(Guid);

    private static async Task<List<TEntity>> QueryCatchUpChangesAsync<TEntity>(
        SchoolDbContext local,
        Guid localSchoolId)
        where TEntity : Domain.Common.AuditableEntity
    {
        var since = DateTime.UtcNow.AddDays(-1);
        var query = local.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e =>
                e.CreatedAt > since
                || (e.UpdatedAt != null && e.UpdatedAt > since)
                || (e.DeletedAt != null && e.DeletedAt > since));

        if (HasMappedSchoolId(local, typeof(TEntity)))
        {
            query = query.Where(e => EF.Property<Guid>(e, "SchoolId") == localSchoolId);
        }

        return await query
            .OrderBy(e => e.UpdatedAt ?? e.CreatedAt)
            .Take(500)
            .ToListAsync();
    }

    private static async Task<Guid> SeedCatchUpFixturesAsync()
    {
        await using var seed = CreateContext();
        seed.IgnoreSchoolScope = true;

        var school = new School
        {
            Id = Guid.NewGuid(),
            Name = "Ecole test catch-up",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        seed.Schools.Add(school);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            SchoolId = school.Id,
            FirstName = "Test",
            LastName = "CatchUp",
            CreatedAt = DateTime.UtcNow
        };
        seed.Students.Add(student);

        seed.DisciplineRecords.Add(new DisciplineRecord
        {
            Id = Guid.NewGuid(),
            SchoolId = school.Id,
            StudentId = student.Id,
            IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IncidentType = "Test",
            Description = "Catch-up discipline",
            CreatedAt = DateTime.UtcNow
        });

        seed.MeritRecords.Add(new MeritRecord
        {
            Id = Guid.NewGuid(),
            SchoolId = school.Id,
            StudentId = student.Id,
            AwardDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MeritType = "Test",
            Description = "Catch-up mérite",
            CreatedAt = DateTime.UtcNow
        });

        seed.PeriodResults.Add(new PeriodResult
        {
            Id = Guid.NewGuid(),
            SchoolId = school.Id,
            StudentId = student.Id,
            AcademicYearId = Guid.NewGuid(),
            AcademicPeriodId = Guid.NewGuid(),
            ClassRoomId = Guid.NewGuid(),
            Average = 10,
            Percentage = 50,
            Rank = 1,
            ClassSize = 1,
            CreatedAt = DateTime.UtcNow
        });

        await seed.SaveChangesAsync();
        return school.Id;
    }
}
