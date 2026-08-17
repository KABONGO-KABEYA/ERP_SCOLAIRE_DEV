using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Application.SchoolEstablishment;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Application.Setup.DTOs;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Setup;
using SchoolManagement.Shared.Constants;
using Xunit;

namespace SchoolManagement.UnitTests.Setup;

public sealed class InitialSetupServiceTests
{
    [Fact]
    public async Task GetStatus_NeedsSetup_When_No_School()
    {
        await using var db = CreateInMemoryDb();
        db.Permissions.Add(NewPermission());
        await db.SaveChangesAsync();

        var service = CreateService(db, Substitute.For<ISchoolService>());
        var status = await service.GetStatusAsync();

        status.NeedsSetup.Should().BeTrue();
        status.HasPermissions.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteAsync_Throws_When_School_Already_Exists()
    {
        await using var db = CreateInMemoryDb();
        db.Schools.Add(new School { Name = "Déjà là", IsActive = true });
        await db.SaveChangesAsync();

        var service = CreateService(db, Substitute.For<ISchoolService>());

        var act = async () => await service.CompleteAsync(SampleRequest());

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Message.Should().Be("La configuration initiale a déjà été effectuée.");
    }

    private static InitialSetupService CreateService(SchoolDbContext db, ISchoolService schoolService) =>
        new(
            db,
            Substitute.For<IPasswordHasher>(),
            schoolService,
            Substitute.For<SchoolManagement.Application.SchoolFees.Interfaces.ISchoolFeeService>(),
            Substitute.For<IServerIdentityProvider>(),
            Substitute.For<ISchoolEstablishmentService>(),
            NullLogger<InitialSetupService>.Instance);

    private static SchoolDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SchoolDbContext(options) { IgnoreSchoolScope = true, SuppressCloudSyncEnqueue = true };
    }

    private static Permission NewPermission() => new()
    {
        Code = Permissions.SchoolsRead,
        Module = "schools",
        Action = PermissionAction.Read,
        DisplayName = "Lire école",
        Description = "Lire école",
        IsActive = true
    };

    private static CompleteInitialSetupRequest SampleRequest() => new(
        "École Test",
        null,
        null,
        null,
        null,
        null,
        null,
        Currency.CDF,
        null,
        null,
        "2026-2027",
        new DateOnly(2026, 9, 1),
        new DateOnly(2027, 7, 31),
        "admin",
        "admin@test.local",
        "Admin@2026",
        "Jean",
        "Admin",
        [
            new InitialFeeTypeRequest("Frais scolaires", Currency.CDF, true),
            new InitialFeeTypeRequest("Frais tenue Gym", Currency.CDF, true)
        ],
        ["Acompte", "1ère tranche", "2ème tranche", "3ème tranche"],
        ["Général"]);
}
