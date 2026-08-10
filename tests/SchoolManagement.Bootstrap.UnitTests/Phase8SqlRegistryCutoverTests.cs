using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SchoolManagement.Bootstrap.API.Establishment;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using SchoolManagement.Bootstrap.API.Services;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

[Trait("Phase", "8")]
public sealed class Phase8SqlRegistryCutoverTests
{
    private const string SecretHash = "phase8-ecole-test-hmac-secret-hash";

    [Fact]
    public async Task SchoolRegistry_Resolves_From_Sql_Without_Legacy_Schools()
    {
        await using var db = CreateDb();
        var repo = new EfBootstrapSchoolRegistryRepository(db);
        await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
        {
            SchoolId = SchoolRegistry.EcoleTestSchoolId,
            SchoolName = "ECOLE TEST",
            ActivationBaseUrl = "http://169.58.93.203:1804",
            CloudBaseUrl = "http://169.58.93.203:1804",
            ServerInstanceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Credential = new BootstrapCredentialUpsert
            {
                CredentialId = Guid.NewGuid(),
                CredentialVersion = 1,
                SecretHash = SecretHash,
            },
        });

        var registry = new SchoolRegistry(
            repo,
            Options.Create(new BootstrapOptions
            {
                AllowLegacyEnvSchoolRegistry = false,
                Schools = [],
            }));

        var resolved = await registry.ResolveAsync(SchoolRegistry.EcoleTestSchoolId);
        resolved.SchoolId.Should().Be(SchoolRegistry.EcoleTestSchoolId);
        resolved.ActivationBaseUrl.Should().Be("http://169.58.93.203:1804");
        resolved.CloudBaseUrl.Should().Be("http://169.58.93.203:1804");
        resolved.ServerInstanceId.Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    }

    [Fact]
    public async Task SchoolRegistry_Fails_When_Missing_Sql_And_Legacy_Disabled()
    {
        await using var db = CreateDb();
        var registry = new SchoolRegistry(
            new EfBootstrapSchoolRegistryRepository(db),
            Options.Create(new BootstrapOptions
            {
                AllowLegacyEnvSchoolRegistry = false,
                Schools =
                [
                    new SchoolRegistryEntryOptions
                    {
                        SchoolId = SchoolRegistry.EcoleTestSchoolId,
                        ActivationBaseUrl = "http://legacy.example",
                        CloudBaseUrl = "http://legacy.example",
                    },
                ],
            }));

        var act = () => registry.ResolveAsync(SchoolRegistry.EcoleTestSchoolId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*legacy env désactivé*");
    }

    [Fact]
    public async Task LegacyMigrator_Upserts_EcoleTest_Urls_Into_Sql()
    {
        await using var db = CreateDb();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped<IBootstrapSchoolRegistryRepository, EfBootstrapSchoolRegistryRepository>();
        await using var provider = services.BuildServiceProvider();

        var options = Options.Create(new BootstrapOptions
        {
            Schools =
            [
                new SchoolRegistryEntryOptions
                {
                    SchoolId = SchoolRegistry.EcoleTestSchoolId,
                    ActivationBaseUrl = "http://169.58.93.203:1804",
                    CloudBaseUrl = "http://169.58.93.203:1804",
                },
            ],
        });

        var migrator = new LegacyEnvSchoolRegistryMigrator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<LegacyEnvSchoolRegistryMigrator>.Instance);

        await migrator.StartAsync(CancellationToken.None);

        var entry = await db.SchoolRegistry.SingleAsync(s => s.SchoolId == SchoolRegistry.EcoleTestSchoolId);
        entry.SchoolName.Should().Be("ECOLE TEST");
        entry.ActivationBaseUrl.Should().Be("http://169.58.93.203:1804");
        entry.CloudBaseUrl.Should().Be("http://169.58.93.203:1804");
        entry.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Establishment_StartComplete_Works_Without_Legacy_Schools_Env()
    {
        await using var factory = new BootstrapWebApplicationFactory();
        // Factory : AllowLegacy=false, Schools=[].
        var schoolId = SchoolRegistry.EcoleTestSchoolId;
        var credentialId = Guid.NewGuid();
        var deviceId = "phase8-device";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IBootstrapSchoolRegistryRepository>();
            await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
            {
                SchoolId = schoolId,
                SchoolName = "ECOLE TEST",
                ActivationBaseUrl = "http://169.58.93.203:1804",
                CloudBaseUrl = "http://169.58.93.203:1804",
                ServerInstanceId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                Credential = new BootstrapCredentialUpsert
                {
                    CredentialId = credentialId,
                    CredentialVersion = 1,
                    SecretHash = SecretHash,
                },
            });

            var options = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOptions>>().Value;
            options.Schools.Should().BeEmpty();
            options.AllowLegacyEnvSchoolRegistry.Should().BeFalse();
        }

        var token = EstablishmentJwtValidator.CreateSignedToken(
            schoolId, credentialId, 1, SecretHash);

        using var client = factory.CreateClient();
        var start = await client.PostAsJsonAsync("/establishment/start", new { token, deviceId });
        start.EnsureSuccessStatusCode();
        var session = await start.Content.ReadFromJsonAsync<StartOk>();
        session.Should().NotBeNull();

        var complete = await client.PostAsJsonAsync("/establishment/complete", new
        {
            establishmentSessionId = session!.EstablishmentSessionId,
            deviceId,
        });
        complete.EnsureSuccessStatusCode();
        var binding = await complete.Content.ReadFromJsonAsync<BindingOk>();
        binding!.SchoolId.Should().Be(schoolId);
        binding.SchoolName.Should().Be("ECOLE TEST");
        binding.CloudBaseUrl.Should().Be("http://169.58.93.203:1804");
        binding.ServerInstanceId.Should().Be(Guid.Parse("11111111-2222-3333-4444-555555555555"));
    }

    [Fact]
    public async Task Health_Reports_EcoleTest_And_No_Business_Legacy_Dependency_Defaults()
    {
        await using var factory = new BootstrapWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IBootstrapSchoolRegistryRepository>();
            await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
            {
                SchoolId = SchoolRegistry.EcoleTestSchoolId,
                SchoolName = "ECOLE TEST",
                ActivationBaseUrl = "http://169.58.93.203:1804",
                CloudBaseUrl = "http://169.58.93.203:1804",
                Credential = new BootstrapCredentialUpsert
                {
                    CredentialId = Guid.NewGuid(),
                    CredentialVersion = 1,
                    SecretHash = SecretHash,
                },
            });
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("service").GetString().Should().Be("bootstrap");
        root.GetProperty("allowLegacyEnvSchoolRegistry").GetBoolean().Should().BeFalse();
        root.GetProperty("legacyEnvSchoolsConfigured").GetInt32().Should().Be(0);
        root.GetProperty("ecoleTestPresent").GetBoolean().Should().BeTrue();
        root.GetProperty("schoolsRegistered").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        root.GetProperty("activeCredentials").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Active_Credential_Matches_Upserted_Local_Material()
    {
        await using var db = CreateDb();
        var repo = new EfBootstrapSchoolRegistryRepository(db);
        var credentialId = Guid.NewGuid();
        await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
        {
            SchoolId = SchoolRegistry.EcoleTestSchoolId,
            SchoolName = "ECOLE TEST",
            ActivationBaseUrl = "http://169.58.93.203:1804",
            CloudBaseUrl = "http://169.58.93.203:1804",
            Credential = new BootstrapCredentialUpsert
            {
                CredentialId = credentialId,
                CredentialVersion = 3,
                SecretHash = SecretHash,
            },
        });

        var active = await repo.GetActiveCredentialAsync(SchoolRegistry.EcoleTestSchoolId);
        active.Should().NotBeNull();
        active!.Id.Should().Be(credentialId);
        active.SecretHash.Should().Be(SecretHash);
        active.CredentialVersion.Should().Be(3);
        active.Status.Should().Be(EstablishmentCredentialStatuses.Active);
        active.TokenType.Should().Be(EstablishmentTokenTypes.SchoolEstablishment);
    }

    private static BootstrapDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseInMemoryDatabase("phase8-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new BootstrapDbContext(options);
    }

    private sealed class StartOk
    {
        public Guid EstablishmentSessionId { get; set; }
    }

    private sealed class BindingOk
    {
        public Guid SchoolId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public string CloudBaseUrl { get; set; } = string.Empty;
        public Guid ServerInstanceId { get; set; }
    }
}
