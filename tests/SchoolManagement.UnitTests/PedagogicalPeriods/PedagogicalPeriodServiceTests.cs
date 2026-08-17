using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.DTOs;
using SchoolManagement.Application.PedagogicalPeriods.Services;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;
using SchoolManagement.UnitTests.TestSupport;
using Xunit;

namespace SchoolManagement.UnitTests.PedagogicalPeriods;

public sealed class PedagogicalPeriodServiceTests
{
    [Fact]
    public async Task SeedDefaultStructureForNewYearAsync_Succeeds_Without_Pedagogical_Permission()
    {
        var fixture = CreateFixture(hasPedagogicalPermission: false);

        var result = await fixture.Service.SeedDefaultStructureForNewYearAsync(
            fixture.Year.SchoolId,
            fixture.Year.Id);

        result.AcademicYearId.Should().Be(fixture.Year.Id);
        fixture.Mains.Should().HaveCount(5);
        fixture.Subs.Should().HaveCount(15);
        fixture.Subs.Should().OnlyContain(s => s.Status == AcademicSubPeriodStatus.AVenir);
        fixture.Subs.Should().OnlyContain(s => s.StartDate == null && s.EndDate == null);
    }

    [Fact]
    public async Task SeedDefaultStructureForNewYearAsync_Succeeds_When_Current_User_Is_Anonymous()
    {
        var fixture = CreateFixture(hasPedagogicalPermission: false);
        fixture.CurrentUser.UserId.Returns((Guid?)null);
        fixture.CurrentUser.SchoolId.Returns((Guid?)null);
        fixture.CurrentUser.HasPermission(Arg.Any<string>()).Returns(false);

        await fixture.Service.SeedDefaultStructureForNewYearAsync(fixture.Year.SchoolId, fixture.Year.Id);

        fixture.Mains.Should().NotBeEmpty();
        fixture.Subs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateDefaultStructureAsync_Throws_Without_Pedagogical_Permission()
    {
        var fixture = CreateFixture(hasPedagogicalPermission: false);

        var act = async () => await fixture.Service.CreateDefaultStructureAsync(
            fixture.Year.SchoolId,
            new CreatePedagogicalStructureRequest(fixture.Year.Id, ReplaceExisting: false));

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Message.Should().Be("Réservé à l'administration pédagogique.");
        fixture.Mains.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDefaultStructureAsync_Succeeds_With_Pedagogical_Permission()
    {
        var fixture = CreateFixture(hasPedagogicalPermission: true);

        var result = await fixture.Service.CreateDefaultStructureAsync(
            fixture.Year.SchoolId,
            new CreatePedagogicalStructureRequest(fixture.Year.Id, ReplaceExisting: false));

        result.AcademicYearId.Should().Be(fixture.Year.Id);
        fixture.Mains.Should().HaveCount(5);
        fixture.Subs.Should().HaveCount(15);
        fixture.Subs.Should().OnlyContain(s => s.Status == AcademicSubPeriodStatus.AVenir);
    }

    [Fact]
    public async Task OpenSubPeriodAsync_Still_Requires_Pedagogical_Permission()
    {
        var privileged = CreateFixture(hasPedagogicalPermission: true);
        await privileged.Service.SeedDefaultStructureForNewYearAsync(
            privileged.Year.SchoolId,
            privileged.Year.Id);

        var unprivilegedUser = Substitute.For<ICurrentUserService>();
        unprivilegedUser.HasPermission(Arg.Any<string>()).Returns(false);
        var unprivileged = new PedagogicalPeriodService(
            new InMemoryRepository<AcademicYear>(privileged.Years),
            new InMemoryRepository<AcademicMainPeriod>(privileged.Mains),
            new InMemoryRepository<AcademicPeriod>(privileged.Subs),
            unprivilegedUser,
            new NoOpUnitOfWork());

        var firstSub = privileged.Subs[0];
        var act = async () => await unprivileged.OpenSubPeriodAsync(privileged.Year.SchoolId, firstSub.Id);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Message.Should().Be("Réservé à l'administration pédagogique.");
        firstSub.Status.Should().Be(AcademicSubPeriodStatus.AVenir);
    }

    private static Fixture CreateFixture(bool hasPedagogicalPermission)
    {
        var year = new AcademicYear
        {
            SchoolId = Guid.NewGuid(),
            Label = "2026-2027",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 7, 31),
            IsCurrent = true
        };

        var years = new List<AcademicYear> { year };
        var mains = new List<AcademicMainPeriod>();
        var subs = new List<AcademicPeriod>();

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.HasPermission(Permissions.PedagogicalPeriodsManage)
            .Returns(hasPedagogicalPermission);
        currentUser.HasPermission(Permissions.AdminFull).Returns(false);
        currentUser.Permissions.Returns(
            hasPedagogicalPermission
                ? [Permissions.PedagogicalPeriodsManage]
                : Array.Empty<string>());
        currentUser.UserId.Returns(hasPedagogicalPermission ? Guid.NewGuid() : null);
        currentUser.SchoolId.Returns(hasPedagogicalPermission ? year.SchoolId : null);

        var service = new PedagogicalPeriodService(
            new InMemoryRepository<AcademicYear>(years),
            new InMemoryRepository<AcademicMainPeriod>(mains),
            new InMemoryRepository<AcademicPeriod>(subs),
            currentUser,
            new NoOpUnitOfWork());

        return new Fixture(service, year, years, mains, subs, currentUser);
    }

    private sealed record Fixture(
        PedagogicalPeriodService Service,
        AcademicYear Year,
        List<AcademicYear> Years,
        List<AcademicMainPeriod> Mains,
        List<AcademicPeriod> Subs,
        ICurrentUserService CurrentUser);
}
