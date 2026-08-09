namespace SchoolManagement.UnitTests.Tenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Tenancy;
using Xunit;

public sealed class SchoolTenancyFilterTests
{
    [Fact]
    public async Task Evaluation_from_other_school_is_hidden_by_query_filter()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Guid schoolA;
        Guid schoolB;
        Guid evaluationB;

        await using (var seed = new SchoolDbContext(options))
        {
            seed.IgnoreSchoolScope = true;

            var a = NewSchool("Ecole A");
            var b = NewSchool("Ecole B");
            seed.Schools.AddRange(a, b);
            await seed.SaveChangesAsync();

            schoolA = a.Id;
            schoolB = b.Id;

            var yearB = NewYear(b.Id);
            seed.AcademicYears.Add(yearB);

            var classB = NewClassRoom(b.Id, yearB.Id);
            seed.ClassRooms.Add(classB);
            await seed.SaveChangesAsync();

            var eval = new Evaluation
            {
                AcademicYearId = yearB.Id,
                AcademicPeriodId = Guid.NewGuid(),
                CourseAssignmentId = Guid.NewGuid(),
                EvaluationTypeId = Guid.NewGuid(),
                CourseId = Guid.NewGuid(),
                ClassRoomId = classB.Id,
                Title = "Interro B",
                EvaluationDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            seed.Evaluations.Add(eval);
            await seed.SaveChangesAsync();
            evaluationB = eval.Id;
        }

        await using var ctx = new SchoolDbContext(options);
        ctx.OverrideTenantSchoolId = schoolA;
        ctx.IgnoreSchoolScope = false;

        var found = await ctx.Evaluations.FirstOrDefaultAsync(e => e.Id == evaluationB);
        found.Should().BeNull();
    }

    [Fact]
    public async Task RequireForSchoolAsync_throws_when_evaluation_belongs_to_other_school()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Guid schoolA;
        Guid schoolB;
        Guid evaluationB;

        await using (var seed = new SchoolDbContext(options))
        {
            seed.IgnoreSchoolScope = true;

            var a = NewSchool("Ecole A");
            var b = NewSchool("Ecole B");
            seed.Schools.AddRange(a, b);
            await seed.SaveChangesAsync();
            schoolA = a.Id;
            schoolB = b.Id;

            var yearB = NewYear(b.Id);
            seed.AcademicYears.Add(yearB);
            var classB = NewClassRoom(b.Id, yearB.Id);
            seed.ClassRooms.Add(classB);
            await seed.SaveChangesAsync();

            var eval = new Evaluation
            {
                AcademicYearId = yearB.Id,
                AcademicPeriodId = Guid.NewGuid(),
                CourseAssignmentId = Guid.NewGuid(),
                EvaluationTypeId = Guid.NewGuid(),
                CourseId = Guid.NewGuid(),
                ClassRoomId = classB.Id,
                Title = "Devoir B",
                EvaluationDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            seed.Evaluations.Add(eval);
            await seed.SaveChangesAsync();
            evaluationB = eval.Id;
        }

        await using var ctx = new SchoolDbContext(options);
        var tenancy = new SchoolTenancyService(ctx);

        var act = () => tenancy.RequireForSchoolAsync<Evaluation>(schoolA, evaluationB);
        await act.Should().ThrowAsync<SchoolTenancyAccessDeniedException>();
    }

    [Fact]
    public async Task RequireForSchoolAsync_returns_entity_for_own_school()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Guid schoolA;
        Guid evaluationA;

        await using (var seed = new SchoolDbContext(options))
        {
            seed.IgnoreSchoolScope = true;

            var a = NewSchool("Ecole A");
            seed.Schools.Add(a);
            await seed.SaveChangesAsync();
            schoolA = a.Id;

            var year = NewYear(a.Id);
            seed.AcademicYears.Add(year);
            var room = NewClassRoom(a.Id, year.Id);
            seed.ClassRooms.Add(room);
            await seed.SaveChangesAsync();

            var eval = new Evaluation
            {
                AcademicYearId = year.Id,
                AcademicPeriodId = Guid.NewGuid(),
                CourseAssignmentId = Guid.NewGuid(),
                EvaluationTypeId = Guid.NewGuid(),
                CourseId = Guid.NewGuid(),
                ClassRoomId = room.Id,
                Title = "Devoir A",
                EvaluationDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            seed.Evaluations.Add(eval);
            await seed.SaveChangesAsync();
            evaluationA = eval.Id;
        }

        await using var ctx = new SchoolDbContext(options);
        var tenancy = new SchoolTenancyService(ctx);

        var entity = await tenancy.RequireForSchoolAsync<Evaluation>(schoolA, evaluationA);
        entity.Id.Should().Be(evaluationA);
    }

    [Fact]
    public async Task Enrollment_from_other_school_is_hidden_by_query_filter()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Guid schoolA;
        Guid enrollmentB;

        await using (var seed = new SchoolDbContext(options))
        {
            seed.IgnoreSchoolScope = true;

            var a = NewSchool("Ecole A");
            var b = NewSchool("Ecole B");
            seed.Schools.AddRange(a, b);
            await seed.SaveChangesAsync();
            schoolA = a.Id;

            var studentB = new Domain.Entities.Students.Student
            {
                SchoolId = b.Id,
                FirstName = "Bob",
                LastName = "B",
                DateOfBirth = new DateOnly(2010, 1, 1),
                CreatedAt = DateTime.UtcNow
            };
            seed.Students.Add(studentB);

            var yearB = NewYear(b.Id);
            seed.AcademicYears.Add(yearB);
            var classB = NewClassRoom(b.Id, yearB.Id);
            seed.ClassRooms.Add(classB);
            await seed.SaveChangesAsync();

            var enrollment = new Domain.Entities.Students.Enrollment
            {
                StudentId = studentB.Id,
                ClassRoomId = classB.Id,
                AcademicYearId = yearB.Id,
                EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                FeePricingCategoryId = Guid.NewGuid(),
                Status = EnrollmentStatus.Inscrit,
                CreatedAt = DateTime.UtcNow
            };
            seed.Enrollments.Add(enrollment);
            await seed.SaveChangesAsync();
            enrollmentB = enrollment.Id;
        }

        await using var ctx = new SchoolDbContext(options);
        ctx.OverrideTenantSchoolId = schoolA;

        var found = await ctx.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentB);
        found.Should().BeNull();
    }

    private static School NewSchool(string name) => new()
    {
        Name = name,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static AcademicYear NewYear(Guid schoolId) => new()
    {
        SchoolId = schoolId,
        Label = "2025-2026",
        StartDate = new DateOnly(2025, 9, 1),
        EndDate = new DateOnly(2026, 6, 30),
        IsCurrent = true,
        CreatedAt = DateTime.UtcNow
    };

    private static ClassRoom NewClassRoom(Guid schoolId, Guid yearId) => new()
    {
        SchoolId = schoolId,
        AcademicYearId = yearId,
        SectionId = Guid.NewGuid(),
        Code = "1A",
        Name = "A",
        Level = 1,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
}
