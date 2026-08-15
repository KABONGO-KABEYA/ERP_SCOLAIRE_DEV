using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Infrastructure.Persistence;
using System.Reflection;

namespace SchoolManagement.Infrastructure.ServerIdentity;

public sealed class ServerIdentityProvider : IServerIdentityProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerIdentityProvider> _logger;
    private readonly ServerIdentityFileStore _fileStore;
    private readonly object _gate = new();

    private ServerIdentitySnapshot _current = CreatePlaceholder();

    public ServerIdentityProvider(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ServerIdentityProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        var encryption = EncryptionServiceFactory.Create();
        var identityDirectory = ResolveIdentityDirectory(configuration);
        _fileStore = new ServerIdentityFileStore(identityDirectory, encryption);
        _logger.LogDebug("ServerIdentity directory: {Dir}", identityDirectory);
    }

    private static string ResolveIdentityDirectory(IConfiguration configuration)
    {
        var fromEnv = Environment.GetEnvironmentVariable("SERVER_IDENTITY_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        var fromConfig = configuration["ServerIdentity:Directory"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig.Trim();
        }

        var fileStorage = configuration["FileStorage:Root"]
                          ?? Environment.GetEnvironmentVariable("FILE_STORAGE_ROOT");
        if (!string.IsNullOrWhiteSpace(fileStorage))
        {
            return Path.Combine(fileStorage.Trim(), "server-identity");
        }

        return AppContext.BaseDirectory;
    }

    public ServerIdentitySnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var file = _fileStore.LoadOrCreateIfMissing();
        Guid? schoolId = null;
        string schoolName = _configuration["School:Name"]
                            ?? _configuration["School:DisplayName"]
                            ?? _configuration["Deployment:SchoolName"]
                            ?? "École";
        Guid? licenseId = null;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
            var school = await db.Schools
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (school is not null)
            {
                schoolId = school.Id;
                schoolName = school.Name;
                var licenseRaw = await db.AppConfigurations
                    .AsNoTracking()
                    .Where(c => c.SchoolId == school.Id && c.Key == ConnectionProtocolConstants.AppConfigurationLicenseIdKey)
                    .Select(c => c.Value)
                    .FirstOrDefaultAsync(cancellationToken);
                if (Guid.TryParse(licenseRaw, out var parsedLicense))
                {
                    licenseId = parsedLicense;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identité serveur : impossible de lire l'école en base (setup non terminé ?).");
        }

        var role = _configuration["Deployment:Role"] ?? "Local";
        var serverRole = role.Equals("Cloud", StringComparison.OrdinalIgnoreCase) ? "cloud" : "local";
        var softwareVersion = ReadEntrySoftwareVersion(_configuration);
        var schemaVersion = await ReadSchemaVersionAtStartupAsync(cancellationToken);

        var snapshot = new ServerIdentitySnapshot(
            file.ServerInstanceId,
            schoolId,
            schoolName,
            licenseId,
            file.PublicKeyFingerprint,
            file.KeyVersion,
            softwareVersion,
            ConnectionProtocolConstants.ApiVersion,
            ConnectionProtocolConstants.ProtocolVersion,
            serverRole,
            schemaVersion);

        lock (_gate)
        {
            _current = snapshot;
        }

        _logger.LogInformation(
            "Identité serveur chargée instance={InstanceId} school={SchoolId} keyVersion={KeyVersion} fingerprint={Fingerprint}",
            snapshot.ServerInstanceId,
            snapshot.SchoolId,
            snapshot.KeyVersion,
            snapshot.PublicKeyFingerprint);
    }

    private static ServerIdentitySnapshot CreatePlaceholder() =>
        new(
            Guid.Empty,
            null,
            "École",
            null,
            string.Empty,
            1,
            "1.0.0",
            ConnectionProtocolConstants.ApiVersion,
            ConnectionProtocolConstants.ProtocolVersion,
            "local",
            1);

    private static string ReadEntrySoftwareVersion(IConfiguration configuration)
    {
        var informational = System.Reflection.Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? informational : informational[..plus];
        }

        return configuration["App:Version"]
               ?? typeof(ServerIdentityProvider).Assembly.GetName().Version?.ToString(3)
               ?? "1.0.0";
    }

    /// <summary>Lecture unique au démarrage — le health ne requête pas SQL.</summary>
    private async Task<int> ReadSchemaVersionAtStartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
            await db.Database.OpenConnectionAsync(cancellationToken);
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.AppSchemaVersion', N'U') IS NULL
                    SELECT 1
                ELSE
                    SELECT SchemaVersion FROM dbo.AppSchemaVersion WHERE Id = 1;
                """;
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result is int i ? i : Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "schemaVersion snapshot : lecture AppSchemaVersion impossible, défaut 1.");
            return 1;
        }
    }
}

public sealed class ServerIdentityInitializationHostedService : IHostedService
{
    private readonly IServerIdentityProvider _provider;
    private readonly ILogger<ServerIdentityInitializationHostedService> _logger;

    public ServerIdentityInitializationHostedService(
        IServerIdentityProvider provider,
        ILogger<ServerIdentityInitializationHostedService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _provider.RefreshAsync(cancellationToken);
        }
        catch (ServerIdentityCorruptedException ex)
        {
            _logger.LogCritical(
                ex,
                "ServerIdentity.json invalide ou clé AES incorrecte — l'API reste en ligne avec identité placeholder. " +
                "Corrigez ERP_CONFIG_ENCRYPTION_KEY, restaurez ServerIdentity.json.bak ou supprimez le fichier sur le volume persistant.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Échec initialisation identité serveur — placeholder actif. Vérifiez SERVER_IDENTITY_DIR / permissions volume.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
