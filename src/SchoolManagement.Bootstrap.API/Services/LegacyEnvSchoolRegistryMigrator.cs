using Microsoft.Extensions.Options;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;

namespace SchoolManagement.Bootstrap.API.Services;

/// <summary>
/// Phase 8 — one-shot : si <c>Bootstrap:Schools</c> legacy est encore présent,
/// upsert les URLs dans SQL (sans credential). Ne réécrit pas un credential existant.
/// </summary>
public sealed class LegacyEnvSchoolRegistryMigrator : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BootstrapOptions> _options;
    private readonly ILogger<LegacyEnvSchoolRegistryMigrator> _logger;

    public LegacyEnvSchoolRegistryMigrator(
        IServiceScopeFactory scopeFactory,
        IOptions<BootstrapOptions> options,
        ILogger<LegacyEnvSchoolRegistryMigrator> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (options.Schools.Count == 0)
        {
            _logger.LogInformation(
                "Bootstrap legacy env Schools vide — registre SQL fait foi.");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBootstrapSchoolRegistryRepository>();

        foreach (var school in options.Schools)
        {
            if (school.SchoolId == Guid.Empty ||
                string.IsNullOrWhiteSpace(school.ActivationBaseUrl) ||
                string.IsNullOrWhiteSpace(school.CloudBaseUrl))
            {
                _logger.LogWarning(
                    "Entrée legacy Schools ignorée (SchoolId/URLs incomplets) : {SchoolId}",
                    school.SchoolId);
                continue;
            }

            var name = school.SchoolId == SchoolRegistry.EcoleTestSchoolId
                ? "ECOLE TEST"
                : $"School {school.SchoolId:D}";

            Guid? serverInstanceId = null;
            if (!string.IsNullOrWhiteSpace(school.ServerInstanceId) &&
                Guid.TryParse(school.ServerInstanceId, out var parsed))
            {
                serverInstanceId = parsed;
            }

            await repo.UpsertSchoolAsync(
                new BootstrapSchoolRegistryUpsertRequest
                {
                    SchoolId = school.SchoolId,
                    SchoolName = name,
                    ActivationBaseUrl = school.ActivationBaseUrl,
                    CloudBaseUrl = school.CloudBaseUrl,
                    PublicKeyFingerprint = school.PublicKeyFingerprint,
                    KeyVersion = school.KeyVersion,
                    ServerInstanceId = serverInstanceId,
                    Credential = null,
                },
                cancellationToken);

            var active = await repo.GetActiveCredentialAsync(school.SchoolId, cancellationToken);
            _logger.LogWarning(
                "Legacy Bootstrap:Schools migré vers SQL pour {SchoolId} ({SchoolName}). " +
                "Credential actif présent={HasCredential}. " +
                "Retirer Bootstrap__Schools__* de Coolify après vérification establishment.",
                school.SchoolId,
                name,
                active is not null);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
