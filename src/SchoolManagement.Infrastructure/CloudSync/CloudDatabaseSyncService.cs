using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Configuration;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Synchronisation unidirectionnelle : base locale (LAN) → base cloud.
/// Upsert par Id ; ne remplace pas le cloud par des données plus anciennes.
/// </summary>
public sealed class CloudDatabaseSyncService : ICloudDatabaseSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CloudDatabaseConfigurationManager _cloudConfigManager;
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly ILogger<CloudDatabaseSyncService> _logger;
    private readonly string _stateFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CloudDatabaseSyncService(
        IServiceScopeFactory scopeFactory,
        CloudDatabaseConfigurationManager cloudConfigManager,
        DatabaseConnectionFactory connectionFactory,
        ILogger<CloudDatabaseSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _cloudConfigManager = cloudConfigManager;
        _connectionFactory = connectionFactory;
        _logger = logger;
        _stateFilePath = Path.Combine(
            Path.GetDirectoryName(_cloudConfigManager.ConfigurationFilePath) ?? AppContext.BaseDirectory,
            "CloudSyncState.txt");
    }

    public async Task<CloudSyncResult> TrySyncAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return CloudSyncResult.Skip("Une synchronisation est déjà en cours.");
        }

        try
        {
            if (!_cloudConfigManager.FileExists)
            {
                return CloudSyncResult.Skip("ServeurDonneesCloud.txt absent — sync cloud désactivée.");
            }

            CloudDatabaseConfiguration cloudConfig;
            try
            {
                cloudConfig = _cloudConfigManager.LoadConfiguration();
            }
            catch (Exception ex)
            {
                return CloudSyncResult.Fail($"Impossible de lire ServeurDonneesCloud.txt : {ex.Message}");
            }

            if (!cloudConfig.Actif)
            {
                return CloudSyncResult.Skip("Sync cloud désactivée (ACTIF=0).");
            }

            var validation = _cloudConfigManager.Validate(cloudConfig, cloudConfig.MotDePasse);
            if (!validation.IsValid)
            {
                return CloudSyncResult.Fail(string.Join(" ", validation.FieldErrors.Values));
            }

            if (!await IsRemoteReachableAsync(cloudConfig, cancellationToken))
            {
                WriteState(success: false, "Distant injoignable (pas Internet ou SQL cloud down).");
                return CloudSyncResult.Skip("Serveur cloud injoignable — nouvelle tentative plus tard.");
            }

            var remoteCs = _connectionFactory.BuildConnectionString(cloudConfig.ToDatabaseConfiguration());
            var sw = Stopwatch.StartNew();
            var tables = 0;
            var rows = 0;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var local = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();

            var remoteOptions = new DbContextOptionsBuilder<SchoolDbContext>()
                .UseSqlServer(remoteCs, sql => sql.EnableRetryOnFailure(2))
                .Options;

            await using var remote = new SchoolDbContext(remoteOptions);

            try
            {
                if (!await remote.Database.CanConnectAsync(cancellationToken))
                {
                    WriteState(success: false, "Connexion SQL cloud refusée.");
                    return CloudSyncResult.Fail("Impossible de se connecter à la base cloud.");
                }
            }
            catch (Exception ex)
            {
                WriteState(success: false, ex.Message);
                return CloudSyncResult.Fail($"Connexion cloud : {ex.Message}");
            }

            foreach (var syncAction in BuildSyncPipeline())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var upserted = await syncAction(local, remote, cancellationToken);
                tables++;
                rows += upserted;
            }

            sw.Stop();
            var result = CloudSyncResult.Ok(tables, rows, sw.Elapsed);
            WriteState(success: true, result.Message);
            _logger.LogInformation("Sync cloud terminée : {Message}", result.Message);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la synchronisation cloud.");
            WriteState(success: false, ex.Message);
            return CloudSyncResult.Fail(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<bool> IsRemoteReachableAsync(
        CloudDatabaseConfiguration config,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(config.Serveur.Trim(), config.Port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private delegate Task<int> SyncTableAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        CancellationToken cancellationToken);

    /// <summary>Ordre respectant les dépendances FK (parents avant enfants).</summary>
    private static IEnumerable<SyncTableAsync> BuildSyncPipeline()
    {
        // Paramétrage / sécurité
        yield return (l, r, ct) => UpsertAsync<School>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Permission>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Role>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<RolePermission>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<UserAccount>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<UserRoleAssignment>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<AcademicYear>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Section>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<StudyOption>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<PedagogicalClass>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<ClassRoom>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Course>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<AcademicPeriod>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<FeeType>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<FeeInstallment>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<FeePricingCategory>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<FeeTypeInstallment>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<ClassFeeAmount>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Bank>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<CashRegister>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<AppConfiguration>(l, r, ct);

        // Géographie
        yield return (l, r, ct) => UpsertAsync<Country>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Province>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<City>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Commune>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<PostalAddress>(l, r, ct);

        // Élèves / inscription
        yield return (l, r, ct) => UpsertAsync<Student>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Guardian>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<StudentGuardian>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<StudentDocument>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Enrollment>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<EnrollmentPricingCategoryHistory>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<StudentStatusHistory>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<StudentFeeBalance>(l, r, ct);

        // Académique
        yield return (l, r, ct) => UpsertAsync<Teacher>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<CourseAssignment>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<ScheduleSlot>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<StudentAttendance>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<TeacherAttendance>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<CalendarEvent>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<DisciplineRecord>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<MeritRecord>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Announcement>(l, r, ct);

        // Notes
        yield return (l, r, ct) => UpsertAsync<Evaluation>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<GradeEntry>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<PeriodResult>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<ReportCard>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<ReportCardDetail>(l, r, ct);

        // Finance
        yield return (l, r, ct) => UpsertAsync<WithholdingType>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<WithholdingConfiguration>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<RevenueAllocationDestination>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<RevenueAllocationKey>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<RevenueAllocationKeyDetail>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<Payment>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<PaymentLine>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<PaymentReversal>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<CashMovement>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<RevenueAllocationEntry>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<WithholdingApplication>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<ExpenseRequest>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<ExpensePayment>(l, r, ct);

        // Branding / audit (léger)
        yield return (l, r, ct) => UpsertAsync<SchoolLogo>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<SchoolDocumentHeader>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<SchoolSignature>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<SchoolStamp>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<SchoolDocumentFooter>(l, r, ct);
        yield return (l, r, ct) => UpsertAsync<AuditEntry>(l, r, ct);
    }

    private static async Task<int> UpsertAsync<TEntity>(
        SchoolDbContext local,
        SchoolDbContext remote,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var localRows = await local.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (localRows.Count == 0)
        {
            return 0;
        }

        var remoteMap = await remote.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var changed = 0;
        foreach (var localRow in localRows)
        {
            if (remoteMap.TryGetValue(localRow.Id, out var remoteRow))
            {
                var localStamp = localRow.UpdatedAt ?? localRow.CreatedAt;
                var remoteStamp = remoteRow.UpdatedAt ?? remoteRow.CreatedAt;
                if (localStamp < remoteStamp)
                {
                    continue;
                }

                remote.Set<TEntity>().Update(localRow);
            }
            else
            {
                remote.Set<TEntity>().Add(localRow);
            }

            changed++;
        }

        if (changed > 0)
        {
            await remote.SaveChangesAsync(cancellationToken);
            // Détacher pour éviter le tracking entre tables
            foreach (var entry in remote.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        return changed;
    }

    private void WriteState(bool success, string message)
    {
        try
        {
            var lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LAST_ATTEMPT_UTC"] = DateTime.UtcNow.ToString("O"),
                ["LAST_SUCCESS"] = success ? "1" : "0",
                ["LAST_MESSAGE"] = message.Replace('\r', ' ').Replace('\n', ' ')
            };
            if (success)
            {
                lines["LAST_SUCCESS_UTC"] = DateTime.UtcNow.ToString("O");
            }

            File.WriteAllText(
                _stateFilePath,
                TextConfigurationFileParser.Serialize(
                    lines,
                    "# État de la dernière synchronisation cloud"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'écrire CloudSyncState.txt.");
        }
    }
}
