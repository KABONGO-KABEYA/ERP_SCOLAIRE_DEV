using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.Infrastructure.Services;

/// <summary>Purge périodique des drafts temp/{draftId} expirés (P3).</summary>
public sealed class EnrollmentDraftCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnrollmentDraftCleanupHostedService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public EnrollmentDraftCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<EnrollmentDraftCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                // Singleton storage — resolve without scope dependency issues.
                var storage = scope.ServiceProvider.GetRequiredService<IStudentDossierStorageService>();
                var purged = storage.PurgeExpiredDrafts(DateTime.UtcNow);
                if (purged > 0)
                {
                    _logger.LogInformation("Purge drafts inscription : {Count} dossier(s) temp supprimé(s).", purged);
                }

                var retried = await storage.RetryPendingPromotionsAsync(stoppingToken);
                if (retried > 0)
                {
                    _logger.LogInformation(
                        "Retry promotion drafts : {Count} draft(s) promu(s) vers students/{{StudentId}}.",
                        retried);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec purge drafts inscription.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
