using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SchoolManagement.Application.SchoolEstablishment;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Domain.Entities.SchoolEstablishment;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.SchoolEstablishment;
using Xunit;

namespace SchoolManagement.SchoolEstablishment.UnitTests;

public sealed class SchoolEstablishmentServiceTests
{
    private static SchoolDbContext CreateDb(Guid? tenantSchoolId = null)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new SchoolDbContext(options);
        db.IgnoreSchoolScope = true;
        if (tenantSchoolId is Guid id)
        {
            db.OverrideTenantSchoolId = id;
        }

        return db;
    }

    private static SchoolEstablishmentService CreateSut(
        SchoolDbContext db,
        IBootstrapSchoolRegistryClient registry,
        SchoolBootstrapRegistryOptions? options = null)
    {
        options ??= new SchoolBootstrapRegistryOptions
        {
            RegistryBaseUrl = "https://bootstrap.test",
            RelayApiKey = "test-key",
            ActivationBaseUrl = "http://school.test:5096",
            CloudBaseUrl = "http://cloud.test:1804",
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var urls = new SchoolBootstrapPublishUrls(Options.Create(options), config);
        var identity = Substitute.For<IServerIdentityProvider>();
        identity.Current.Returns(new ServerIdentitySnapshot(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            null,
            "ECOLE",
            null,
            "sha256:abc",
            1,
            "1.0",
            "v1",
            2,
            "Local",
            1));

        return new SchoolEstablishmentService(
            db,
            registry,
            urls,
            identity,
            NullLogger<SchoolEstablishmentService>.Instance);
    }

    [Fact]
    public async Task Provision_CreatesLocalCredential_AndPublishesUpsert()
    {
        await using var db = CreateDb();
        var schoolId = Guid.NewGuid();
        db.Schools.Add(new School { Id = schoolId, Name = "ECOLE TEST", Country = "RDC", IsActive = true });
        await db.SaveChangesAsync();

        var registry = Substitute.For<IBootstrapSchoolRegistryClient>();
        BootstrapRegistryUpsertPayload? captured = null;
        registry.UpsertSchoolAsync(Arg.Do<BootstrapRegistryUpsertPayload>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = CreateSut(db, registry);
        var qr = await sut.ProvisionForNewSchoolAsync(schoolId, "ECOLE TEST");

        qr.SchoolId.Should().Be(schoolId);
        qr.CredentialVersion.Should().Be(1);
        qr.BootstrapSyncPending.Should().BeFalse();
        qr.BootstrapSyncStatus.Should().Be(SchoolEstablishmentBootstrapSyncStatuses.Synced);
        qr.Token.Should().NotBeNullOrWhiteSpace();
        qr.QrPayload.Should().StartWith("erp-scolaire://establish?token=");
        qr.Token.Should().NotContain("SecretHash", "pas de secret dans le JWT payload visible");

        captured.Should().NotBeNull();
        captured!.SchoolId.Should().Be(schoolId);
        captured.Credential.SecretHash.Should().NotBeNullOrWhiteSpace();
        captured.Credential.SecretHash.Length.Should().Be(64); // sha256 hex

        var rows = await db.SchoolEstablishmentCredentials.IgnoreQueryFilters()
            .Where(c => !c.IsDeleted && c.SchoolId == schoolId).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Status.Should().Be(SchoolEstablishmentCredentialStatuses.Active);
        rows[0].BootstrapSyncPending.Should().BeFalse();

        // Idempotent : second provision ne crée pas de doublon Active.
        var again = await sut.ProvisionForNewSchoolAsync(schoolId, "ECOLE TEST");
        again.CredentialId.Should().Be(qr.CredentialId);
        (await db.SchoolEstablishmentCredentials.IgnoreQueryFilters()
            .CountAsync(c => !c.IsDeleted && c.SchoolId == schoolId)).Should().Be(1);
    }

    [Fact]
    public async Task Provision_WhenBootstrapFails_KeepsSchoolCredential_WithSyncPending()
    {
        await using var db = CreateDb();
        var schoolId = Guid.NewGuid();
        db.Schools.Add(new School { Id = schoolId, Name = "ECOLE OFFLINE", Country = "RDC", IsActive = true });
        await db.SaveChangesAsync();

        var registry = Substitute.For<IBootstrapSchoolRegistryClient>();
        registry.UpsertSchoolAsync(Arg.Any<BootstrapRegistryUpsertPayload>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new HttpRequestException("connection refused"));

        var sut = CreateSut(db, registry);
        var qr = await sut.ProvisionForNewSchoolAsync(schoolId, "ECOLE OFFLINE");

        qr.BootstrapSyncPending.Should().BeTrue();
        qr.BootstrapSyncStatus.Should().Be(SchoolEstablishmentBootstrapSyncStatuses.Failed);
        qr.CredentialId.Should().NotBeEmpty();

        var row = await db.SchoolEstablishmentCredentials.IgnoreQueryFilters()
            .SingleAsync(c => !c.IsDeleted && c.SchoolId == schoolId);
        row.Status.Should().Be(SchoolEstablishmentCredentialStatuses.Active);
        row.BootstrapSyncPending.Should().BeTrue();
        row.LastBootstrapSyncError.Should().NotBeNullOrWhiteSpace();
        row.LastBootstrapSyncError.Should().NotContain(row.SecretHash);
    }

    [Fact]
    public async Task RetryBootstrapSync_Succeeds_AndClearsPending()
    {
        await using var db = CreateDb();
        var schoolId = Guid.NewGuid();
        db.Schools.Add(new School { Id = schoolId, Name = "ECOLE RETRY", Country = "RDC", IsActive = true });
        var credential = new SchoolEstablishmentCredential
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            CredentialVersion = 1,
            SecretHash = SchoolEstablishmentCrypto.CreateSecretHash(),
            Status = SchoolEstablishmentCredentialStatuses.Active,
            BootstrapSyncPending = true,
            BootstrapSyncStatus = SchoolEstablishmentBootstrapSyncStatuses.Failed,
            LastBootstrapSyncError = "Bootstrap injoignable.",
        };
        db.SchoolEstablishmentCredentials.Add(credential);
        await db.SaveChangesAsync();

        var registry = Substitute.For<IBootstrapSchoolRegistryClient>();
        registry.UpsertSchoolAsync(Arg.Any<BootstrapRegistryUpsertPayload>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = CreateSut(db, registry);
        var result = await sut.RetryBootstrapSyncAsync(schoolId);

        result.Success.Should().BeTrue();
        result.BootstrapSyncPending.Should().BeFalse();
        result.BootstrapSyncStatus.Should().Be(SchoolEstablishmentBootstrapSyncStatuses.Synced);

        var row = await db.SchoolEstablishmentCredentials.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == credential.Id);
        row.BootstrapSyncPending.Should().BeFalse();
        row.BootstrapSyncedAtUtc.Should().NotBeNull();
        await registry.Received(1).UpsertSchoolAsync(Arg.Any<BootstrapRegistryUpsertPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rotate_RevokesPrevious_ActivatesNew_AndCallsBootstrapRotate()
    {
        await using var db = CreateDb();
        var schoolId = Guid.NewGuid();
        db.Schools.Add(new School { Id = schoolId, Name = "ECOLE ROTATE", Country = "RDC", IsActive = true });
        var firstId = Guid.NewGuid();
        db.SchoolEstablishmentCredentials.Add(new SchoolEstablishmentCredential
        {
            Id = firstId,
            SchoolId = schoolId,
            CredentialVersion = 1,
            SecretHash = SchoolEstablishmentCrypto.CreateSecretHash(),
            Status = SchoolEstablishmentCredentialStatuses.Active,
            BootstrapSyncPending = false,
            BootstrapSyncStatus = SchoolEstablishmentBootstrapSyncStatuses.Synced,
        });
        await db.SaveChangesAsync();

        var registry = Substitute.For<IBootstrapSchoolRegistryClient>();
        BootstrapRegistryCredentialPayload? rotated = null;
        registry.RotateCredentialAsync(
                schoolId,
                Arg.Do<BootstrapRegistryCredentialPayload>(c => rotated = c),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = CreateSut(db, registry);
        var qr = await sut.RotateAsync(schoolId, Guid.NewGuid(), "Admin rotation");

        qr.CredentialId.Should().NotBe(firstId);
        qr.CredentialVersion.Should().Be(2);
        qr.BootstrapSyncPending.Should().BeFalse();

        var old = await db.SchoolEstablishmentCredentials.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == firstId);
        old.Status.Should().Be(SchoolEstablishmentCredentialStatuses.Revoked);
        old.RevokedReason.Should().Be("Admin rotation");

        var active = await db.SchoolEstablishmentCredentials.IgnoreQueryFilters()
            .SingleAsync(c => !c.IsDeleted && c.SchoolId == schoolId && c.Status == SchoolEstablishmentCredentialStatuses.Active);
        active.Id.Should().Be(qr.CredentialId);
        active.CredentialVersion.Should().Be(2);

        rotated.Should().NotBeNull();
        rotated!.CredentialId.Should().Be(qr.CredentialId);
        rotated.CredentialVersion.Should().Be(2);
    }

    [Fact]
    public async Task Rotate_WhenBootstrapFails_KeepsNewLocalActive_WithSyncPending()
    {
        await using var db = CreateDb();
        var schoolId = Guid.NewGuid();
        db.Schools.Add(new School { Id = schoolId, Name = "ECOLE ROTATE FAIL", Country = "RDC", IsActive = true });
        db.SchoolEstablishmentCredentials.Add(new SchoolEstablishmentCredential
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            CredentialVersion = 1,
            SecretHash = SchoolEstablishmentCrypto.CreateSecretHash(),
            Status = SchoolEstablishmentCredentialStatuses.Active,
            BootstrapSyncPending = false,
            BootstrapSyncStatus = SchoolEstablishmentBootstrapSyncStatuses.Synced,
        });
        await db.SaveChangesAsync();

        var registry = Substitute.For<IBootstrapSchoolRegistryClient>();
        registry.RotateCredentialAsync(
                Arg.Any<Guid>(),
                Arg.Any<BootstrapRegistryCredentialPayload>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new BootstrapRegistryClientException(
                System.Net.HttpStatusCode.ServiceUnavailable,
                "Publication Bootstrap (rotate) refusée (503)."));

        var sut = CreateSut(db, registry);
        var qr = await sut.RotateAsync(schoolId, null, "test");

        qr.CredentialVersion.Should().Be(2);
        qr.BootstrapSyncPending.Should().BeTrue();

        (await db.SchoolEstablishmentCredentials.IgnoreQueryFilters().CountAsync(
            c => !c.IsDeleted && c.SchoolId == schoolId && c.Status == SchoolEstablishmentCredentialStatuses.Active))
            .Should().Be(1);
        (await db.SchoolEstablishmentCredentials.IgnoreQueryFilters().CountAsync(
            c => !c.IsDeleted && c.SchoolId == schoolId && c.Status == SchoolEstablishmentCredentialStatuses.Revoked))
            .Should().Be(1);
    }

    [Fact]
    public void CreateSecretHash_NeverEqualsRaw_AndIsHex64()
    {
        var hash = SchoolEstablishmentCrypto.CreateSecretHash();
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }
}
