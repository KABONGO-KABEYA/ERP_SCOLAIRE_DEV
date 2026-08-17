using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.Services;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Services;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;
using SchoolManagement.UnitTests.TestSupport;
using Xunit;

namespace SchoolManagement.UnitTests.Schools;

public sealed class SchoolServiceCreateAcademicYearTests
{
    [Fact]
    public async Task CreateAcademicYearAsync_Seeds_Default_Structure_Without_Pedagogical_Permission()
    {
        var schoolId = Guid.NewGuid();
        var years = new List<AcademicYear>();
        var mains = new List<AcademicMainPeriod>();
        var subs = new List<AcademicPeriod>();

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.HasPermission(Arg.Any<string>()).Returns(false);
        currentUser.HasPermission(Permissions.SchoolsUpdate).Returns(true);
        currentUser.UserId.Returns(Guid.NewGuid());
        currentUser.SchoolId.Returns(schoolId);

        var pedagogical = new PedagogicalPeriodService(
            new InMemoryRepository<AcademicYear>(years),
            new InMemoryRepository<AcademicMainPeriod>(mains),
            new InMemoryRepository<AcademicPeriod>(subs),
            currentUser,
            new NoOpUnitOfWork());

        var schoolService = CreateSchoolService(years, pedagogical);

        var created = await schoolService.CreateAcademicYearAsync(
            schoolId,
            new CreateAcademicYearRequest(
                "2026-2027",
                new DateOnly(2026, 9, 1),
                new DateOnly(2027, 7, 31),
                SetAsCurrent: false));

        created.Label.Should().Be("2026-2027");
        years.Should().ContainSingle(y => y.Id == created.Id && y.SchoolId == schoolId);
        mains.Should().HaveCount(5);
        subs.Should().HaveCount(15);
        subs.Should().OnlyContain(s => s.Status == AcademicSubPeriodStatus.AVenir);
        subs.Should().OnlyContain(s => s.AcademicYearId == created.Id);
    }

    [Fact]
    public async Task CreateAcademicYearAsync_Calls_Seed_Not_Managed_CreateDefaultStructure()
    {
        var schoolId = Guid.NewGuid();
        var years = new List<AcademicYear>();
        var pedagogical = Substitute.For<IPedagogicalPeriodService>();

        var schoolService = CreateSchoolService(years, pedagogical);

        var created = await schoolService.CreateAcademicYearAsync(
            schoolId,
            new CreateAcademicYearRequest(
                "2026-2027",
                new DateOnly(2026, 9, 1),
                new DateOnly(2027, 7, 31),
                SetAsCurrent: false));

        await pedagogical.Received(1).SeedDefaultStructureForNewYearAsync(
            schoolId,
            created.Id,
            Arg.Any<CancellationToken>());
        await pedagogical.DidNotReceive().CreateDefaultStructureAsync(
            Arg.Any<Guid>(),
            Arg.Any<SchoolManagement.Application.PedagogicalPeriods.DTOs.CreatePedagogicalStructureRequest>(),
            Arg.Any<CancellationToken>());
    }

    private static SchoolService CreateSchoolService(
        List<AcademicYear> years,
        IPedagogicalPeriodService pedagogical)
    {
        return new SchoolService(
            Substitute.For<IRepository<School>>(),
            new InMemoryRepository<AcademicYear>(years),
            Substitute.For<IRepository<AcademicPeriod>>(),
            Substitute.For<IRepository<ClassRoom>>(),
            Substitute.For<IRepository<PedagogicalClass>>(),
            Substitute.For<IRepository<Section>>(),
            Substitute.For<IRepository<Course>>(),
            Substitute.For<IRepository<PedagogicalClassCourse>>(),
            Substitute.For<IRepository<FeeType>>(),
            Substitute.For<IRepository<CashRegister>>(),
            Substitute.For<IRepository<AppConfiguration>>(),
            pedagogical,
            new NoOpUnitOfWork());
    }
}
