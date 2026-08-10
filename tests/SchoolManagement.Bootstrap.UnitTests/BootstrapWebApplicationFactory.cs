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

namespace SchoolManagement.Bootstrap.UnitTests;

/// <summary>Hôte Bootstrap isolé (InMemory) — Phase 8 : pas de Schools__* legacy.</summary>
public sealed class BootstrapWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestRelayApiKey = "phase2-test-bootstrap-relay-key";

    private readonly string _dbName = "BootstrapRegistryTests-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Bootstrap:ConnectionString", string.Empty);
        builder.UseSetting("Bootstrap:RelayApiKey", TestRelayApiKey);
        builder.UseSetting("Bootstrap:AllowLegacyEnvSchoolRegistry", "false");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:ConnectionString"] = string.Empty,
                ["Bootstrap:RelayApiKey"] = TestRelayApiKey,
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

            services.PostConfigure<BootstrapOptions>(options =>
            {
                options.RelayApiKey = TestRelayApiKey;
                options.ConnectionString = string.Empty;
                options.EstablishmentSessionMinutes = 15;
                options.AllowLegacyEnvSchoolRegistry = false;
                options.Schools = [];
            });
        });
    }
}
