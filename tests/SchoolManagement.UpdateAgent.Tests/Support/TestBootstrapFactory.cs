using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Bootstrap.API.Establishment;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Services;

namespace SchoolManagement.UpdateAgent.Tests.Support;

public sealed class TestBootstrapFactory : WebApplicationFactory<global::Program>
{
    public const string TestRelayApiKey = "phase2-test-bootstrap-relay-key";
    public const string TestReleasePublishApiKey = "lot1-test-release-publish-key";
    public const string TestAgentProvisionApiKey = "lot2a1-test-agent-provision-key";
    public const string TestAgentJwtSigningKey = "lot2a1-test-agent-jwt-hmac-signing-key";

    private readonly string _dbName = "UpdateAgentBootstrap-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Bootstrap:ConnectionString", string.Empty);
        builder.UseSetting("Bootstrap:RelayApiKey", TestRelayApiKey);
        builder.UseSetting("Bootstrap:ReleasePublishApiKey", TestReleasePublishApiKey);
        builder.UseSetting("Bootstrap:AgentProvisionApiKey", TestAgentProvisionApiKey);
        builder.UseSetting("Bootstrap:AgentJwtSigningKey", TestAgentJwtSigningKey);
        builder.UseSetting("Bootstrap:AgentJwtMinutes", "30");
        builder.UseSetting("Bootstrap:AllowLegacyEnvSchoolRegistry", "false");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:ConnectionString"] = string.Empty,
                ["Bootstrap:RelayApiKey"] = TestRelayApiKey,
                ["Bootstrap:ReleasePublishApiKey"] = TestReleasePublishApiKey,
                ["Bootstrap:AgentProvisionApiKey"] = TestAgentProvisionApiKey,
                ["Bootstrap:AgentJwtSigningKey"] = TestAgentJwtSigningKey,
                ["Bootstrap:AgentJwtMinutes"] = "30",
                ["Bootstrap:AllowLegacyEnvSchoolRegistry"] = "false",
                ["Bootstrap:Schools:0:SchoolId"] = null,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<BootstrapDbContext>));
            services.RemoveAll(typeof(BootstrapDbContext));
            services.AddDbContext<BootstrapDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
            services.AddScoped<IBootstrapSchoolRegistryRepository, EfBootstrapSchoolRegistryRepository>();
            services.AddScoped<SchoolRegistry>();
            services.AddScoped<EstablishmentService>();
            services.AddScoped<BootstrapOrchestrator>();
            services.AddScoped<IUpdateReleaseCatalog, UpdateReleaseCatalog>();
            services.AddScoped<IUpdateAgentCredentialService, UpdateAgentCredentialService>();

            services.PostConfigure<BootstrapOptions>(options =>
            {
                options.RelayApiKey = TestRelayApiKey;
                options.ReleasePublishApiKey = TestReleasePublishApiKey;
                options.AgentProvisionApiKey = TestAgentProvisionApiKey;
                options.AgentJwtSigningKey = TestAgentJwtSigningKey;
                options.AgentJwtMinutes = 30;
                options.ConnectionString = string.Empty;
                options.EstablishmentSessionMinutes = 15;
                options.AllowLegacyEnvSchoolRegistry = false;
                options.Schools = [];
            });
        });
    }
}
