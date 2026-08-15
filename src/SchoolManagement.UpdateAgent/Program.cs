using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "provision", StringComparison.OrdinalIgnoreCase))
        {
            return Provision(args);
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = AgentServiceNames.WindowsServiceName;
        });

        builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
        var dataRoot = builder.Configuration[$"{AgentOptions.SectionName}:DataRoot"];
        var apiRoot = builder.Configuration[$"{AgentOptions.SectionName}:ApiInstallRoot"];
        var logPaths = new AgentPaths(dataRoot, apiRoot, builder.Configuration[$"{AgentOptions.SectionName}:BackupRoot"]);
        Directory.CreateDirectory(logPaths.Logs);
        builder.Logging.AddProvider(new AgentFileLoggerProvider(logPaths.Logs));

        builder.Services.AddSingleton(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            var paths = new AgentPaths(opt.DataRoot, opt.ApiInstallRoot, opt.BackupRoot);
            paths.EnsureDirectories();
            return paths;
        });
        builder.Services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        builder.Services.AddSingleton<AgentCredentialStore>();
        builder.Services.AddSingleton<AgentStateStore>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value);

        builder.Services.AddHttpClient<IBootstrapAgentClient, BootstrapAgentClient>((sp, client) =>
            {
                var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
                var baseUrl = opt.BootstrapBaseUrl.Trim().TrimEnd('/') + "/";
                BootstrapUrlPolicy.EnsureAllowed(baseUrl, opt.AllowedHosts);
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromMinutes(5);
            })
            .ConfigurePrimaryHttpMessageHandler(UpdateTlsPolicy.CreateHandler);

        builder.Services.AddHttpClient("artifacts")
            .ConfigurePrimaryHttpMessageHandler(UpdateTlsPolicy.CreateHandler);

        builder.Services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            var hosts = ResolveHosts(opt);
            return new DownloadManager(
                factory.CreateClient("artifacts"),
                uri => UpdateUrlGuard.IsAllowed(uri, hosts, allowHttpForLocalHosts: true));
        });
        builder.Services.AddSingleton<IPackageAcquire>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            return new PackageAcquireService(
                sp.GetRequiredService<AgentPaths>(),
                sp.GetRequiredService<DownloadManager>(),
                ResolveHosts(opt));
        });
        builder.Services.AddHttpClient("health")
            .ConfigurePrimaryHttpMessageHandler(UpdateTlsPolicy.CreateHandler);
        builder.Services.AddSingleton<IApiHealthProbe>(sp =>
            new ApiHealthProbe(sp.GetRequiredService<IHttpClientFactory>().CreateClient("health")));
        builder.Services.AddSingleton<IDiskSpaceChecker, DriveDiskSpaceChecker>();
        builder.Services.AddSingleton<IApiDirectorySwapper, ApiDirectorySwapper>();
        builder.Services.AddSingleton<IApiWindowsService, ErpScolaireApiService>();
        builder.Services.AddSingleton<ISchoolDatabaseBackup>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            var paths = sp.GetRequiredService<AgentPaths>();
            if (string.IsNullOrWhiteSpace(opt.DatabaseConnectionString)
                || string.IsNullOrWhiteSpace(opt.ExpectedDatabaseName))
            {
                return new UnconfiguredSchoolBackup();
            }

            return new SqlSchoolDatabaseBackup(
                new SqlCommandBackupExecutor(opt.DatabaseConnectionString),
                sp.GetRequiredService<IDiskSpaceChecker>(),
                paths.Backups,
                opt.ExpectedDatabaseName,
                opt.MinFreeDiskBytes,
                opt.MinBackupBytes);
        });
        builder.Services.AddSingleton<ISchoolDatabaseRestore>(sp =>
            sp.GetRequiredService<ISchoolDatabaseBackup>() as ISchoolDatabaseRestore
            ?? new UnconfiguredSchoolBackup());
        builder.Services.AddSingleton<IMigrationEngine>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
            if (string.IsNullOrWhiteSpace(opt.DatabaseConnectionString))
            {
                return new UnconfiguredMigrationEngine();
            }

            return new SqlMigrationEngine(opt.DatabaseConnectionString);
        });
        builder.Services.AddSingleton<IDeployOrchestrator, DeployOrchestrator>();
        builder.Services.AddSingleton<AgentCycle>();
        builder.Services.AddHostedService<UpdateAgentWorker>();

        var host = builder.Build();
        await host.RunAsync();
        return 0;
    }

    internal static string[] ResolveHosts(AgentOptions opt)
    {
        if (opt.AllowedHosts is { Length: > 0 })
        {
            return opt.AllowedHosts;
        }

        if (Uri.TryCreate(opt.BootstrapBaseUrl, UriKind.Absolute, out var uri))
        {
            return [uri.Host];
        }

        return [];
    }

    internal static int Provision(string[] args)
    {
        try
        {
            var dataRoot = ReadArg(args, "--data-root");
            var paths = new AgentPaths(dataRoot);
            paths.EnsureDirectories();
            var store = new AgentCredentialStore(paths, new DpapiSecretProtector());
            var credential = new AgentCredential
            {
                ClientId = Guid.Parse(ReadArg(args, "--client-id") ?? throw Missing("--client-id")),
                ClientSecret = ReadArg(args, "--client-secret") ?? throw Missing("--client-secret"),
                CredentialVersion = int.Parse(ReadArg(args, "--credential-version") ?? "1"),
                SchoolId = Guid.Parse(ReadArg(args, "--school-id") ?? throw Missing("--school-id")),
                ServerInstanceId = Guid.TryParse(ReadArg(args, "--server-instance-id"), out var sid) ? sid : null,
            };
            store.Save(credential);
            Console.WriteLine("Credential DPAPI écrit (secret non affiché).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? ReadArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static AgentException Missing(string name) => new($"Argument {name} requis.");
}
