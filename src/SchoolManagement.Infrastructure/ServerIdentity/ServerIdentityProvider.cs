using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Infrastructure.Persistence;

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
        _fileStore = new ServerIdentityFileStore(AppContext.BaseDirectory, encryption);
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
        var softwareVersion = _configuration["App:Version"]
                              ?? typeof(ServerIdentityProvider).Assembly.GetName().Version?.ToString(3)
                              ?? "1.0.0";

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
            serverRole);

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
            "local");
}

public sealed class ServerIdentityInitializationHostedService : IHostedService
{
    private readonly IServerIdentityProvider _provider;

    public ServerIdentityInitializationHostedService(IServerIdentityProvider provider)
    {
        _provider = provider;
    }

    public async Task StartAsync(CancellationToken cancellationToken) =>
        await _provider.RefreshAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
