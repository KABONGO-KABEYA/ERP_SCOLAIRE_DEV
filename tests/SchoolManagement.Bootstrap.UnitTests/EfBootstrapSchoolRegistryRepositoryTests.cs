using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

public sealed class EfBootstrapSchoolRegistryRepositoryTests
{
    private static BootstrapDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BootstrapDbContext(options);
    }

    [Fact]
    public async Task UpsertSchool_CreatesRegistryAndActiveCredential()
    {
        await using var db = CreateDb();
        var repo = new EfBootstrapSchoolRegistryRepository(db);
        var schoolId = Guid.Parse("71635f62-b975-479d-9e6e-fbacd05e4996");
        var credentialId = Guid.NewGuid();

        var entry = await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
        {
            SchoolId = schoolId,
            SchoolName = "ECOLE TEST",
            ActivationBaseUrl = "http://169.58.93.203:1804",
            CloudBaseUrl = "http://169.58.93.203:1804",
            Credential = new BootstrapCredentialUpsert
            {
                CredentialId = credentialId,
                CredentialVersion = 1,
                SecretHash = "abc123hash",
                CreatedBy = "phase1-test",
            },
        });

        entry.SchoolId.Should().Be(schoolId);
        entry.IsActive.Should().BeTrue();

        var loaded = await repo.GetBySchoolIdAsync(schoolId);
        loaded.Should().NotBeNull();
        loaded!.SchoolName.Should().Be("ECOLE TEST");

        var active = await repo.GetActiveCredentialAsync(schoolId);
        active.Should().NotBeNull();
        active!.Id.Should().Be(credentialId);
        active.Status.Should().Be(EstablishmentCredentialStatuses.Active);
        active.CredentialVersion.Should().Be(1);
    }

    [Fact]
    public async Task RotateCredential_RevokesPrevious_AndActivatesNew()
    {
        await using var db = CreateDb();
        var repo = new EfBootstrapSchoolRegistryRepository(db);
        var schoolId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
        {
            SchoolId = schoolId,
            SchoolName = "Ecole A",
            ActivationBaseUrl = "http://a.example",
            CloudBaseUrl = "http://cloud.example",
            Credential = new BootstrapCredentialUpsert
            {
                CredentialId = firstId,
                CredentialVersion = 1,
                SecretHash = "hash-v1",
            },
        });

        var (revoked, active) = await repo.RotateCredentialAsync(
            schoolId,
            new BootstrapCredentialUpsert
            {
                CredentialId = secondId,
                CredentialVersion = 2,
                SecretHash = "hash-v2",
            },
            reason: "Admin rotation");

        revoked.Id.Should().Be(firstId);
        revoked.Status.Should().Be(EstablishmentCredentialStatuses.Revoked);
        revoked.RevokedReason.Should().Be("Admin rotation");

        active.Id.Should().Be(secondId);
        active.Status.Should().Be(EstablishmentCredentialStatuses.Active);
        active.CredentialVersion.Should().Be(2);

        var onlyActive = await repo.GetActiveCredentialAsync(schoolId);
        onlyActive!.Id.Should().Be(secondId);

        var old = await repo.GetCredentialByIdAsync(firstId);
        old!.Status.Should().Be(EstablishmentCredentialStatuses.Revoked);
    }

    [Fact]
    public async Task CreateAndCompleteSession_Works()
    {
        await using var db = CreateDb();
        var repo = new EfBootstrapSchoolRegistryRepository(db);
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();

        await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
        {
            SchoolId = schoolId,
            SchoolName = "Ecole S",
            ActivationBaseUrl = "http://s.example",
            CloudBaseUrl = "http://cloud.example",
            Credential = new BootstrapCredentialUpsert
            {
                CredentialId = credentialId,
                CredentialVersion = 1,
                SecretHash = "h",
            },
        });

        var session = await repo.CreateSessionAsync(
            schoolId,
            credentialId,
            "device-1",
            DateTime.UtcNow.AddMinutes(15));

        session.Status.Should().Be(EstablishmentSessionStatuses.Pending);

        await repo.MarkSessionCompletedAsync(session.Id);
        var done = await repo.GetSessionAsync(session.Id);
        done!.Status.Should().Be(EstablishmentSessionStatuses.Completed);
        done.CompletedAtUtc.Should().NotBeNull();
    }
}
