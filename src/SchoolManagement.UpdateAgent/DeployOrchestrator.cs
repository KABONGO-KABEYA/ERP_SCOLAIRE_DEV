using Microsoft.Extensions.Logging;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public interface IDeployOrchestrator
{
    Task<AgentState> RunAsync(AgentState state, AgentCredential credential, CancellationToken cancellationToken);
}

public sealed class DeployOrchestrator : IDeployOrchestrator
{
    private readonly AgentPaths _paths;
    private readonly AgentStateStore _store;
    private readonly AgentOptions _options;
    private readonly ISchoolDatabaseBackup _backup;
    private readonly ISchoolDatabaseRestore _restore;
    private readonly IMigrationEngine _migrations;
    private readonly IApiWindowsService _apiService;
    private readonly IApiDirectorySwapper _swapper;
    private readonly IApiHealthProbe _health;
    private readonly IDiskSpaceChecker _disk;
    private readonly ILogger<DeployOrchestrator> _log;

    public DeployOrchestrator(
        AgentPaths paths,
        AgentStateStore store,
        AgentOptions options,
        ISchoolDatabaseBackup backup,
        ISchoolDatabaseRestore restore,
        IMigrationEngine migrations,
        IApiWindowsService apiService,
        IApiDirectorySwapper swapper,
        IApiHealthProbe health,
        IDiskSpaceChecker disk,
        ILogger<DeployOrchestrator> log)
    {
        _paths = paths;
        _store = store;
        _options = options;
        _backup = backup;
        _restore = restore;
        _migrations = migrations;
        _apiService = apiService;
        _swapper = swapper;
        _health = health;
        _disk = disk;
        _log = log;
    }

    public async Task<AgentState> RunAsync(AgentState state, AgentCredential credential, CancellationToken cancellationToken)
    {
        if (_apiService.ServiceName != AgentServiceNames.ApiWindowsServiceName)
        {
            throw new AgentException("Service API non autorisé.");
        }

        try
        {
            if (state.Phase is DeployPhases.Idle or null)
            {
                if (string.IsNullOrWhiteSpace(state.ExtractRoot) || string.IsNullOrWhiteSpace(state.TargetRelease))
                {
                    return Save(state);
                }

                state.Phase = DeployPhases.Verified;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (state.Phase)
                {
                    case DeployPhases.Verified:
                        await PreflightAsync(state, credential, cancellationToken);
                        Persist(state, DeployPhases.Preflight);
                        break;
                    case DeployPhases.Preflight:
                        await StopApiAsync(state, cancellationToken);
                        Persist(state, DeployPhases.ApiStopped);
                        break;
                    case DeployPhases.ApiStopped:
                        await BackupIfNeededAsync(state, cancellationToken);
                        Persist(state, DeployPhases.BackupCreated);
                        break;
                    case DeployPhases.BackupCreated:
                        await MigrateAsync(state, cancellationToken);
                        Persist(state, DeployPhases.MigrationSucceeded);
                        break;
                    case DeployPhases.MigrationSucceeded:
                        Swap(state);
                        Persist(state, DeployPhases.ApiStaged);
                        break;
                    case DeployPhases.ApiStaged:
                        await StartApiAsync(state, cancellationToken);
                        Persist(state, DeployPhases.ApiStarted);
                        break;
                    case DeployPhases.ApiStarted:
                        Persist(state, DeployPhases.HealthChecking);
                        break;
                    case DeployPhases.HealthChecking:
                        await HealthAsync(state, credential, cancellationToken);
                        Persist(state, DeployPhases.Completed);
                        Complete(state);
                        return Save(state);
                    case DeployPhases.RollbackRequired:
                        await RollbackAsync(state, cancellationToken);
                        return Save(state);
                    case DeployPhases.Completed:
                        state.Phase = DeployPhases.Idle;
                        return Save(state);
                    default:
                        throw new AgentException($"Phase non reprise : {state.Phase}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await HandleFailureAsync(state, ex, cancellationToken);
        }
    }

    private async Task PreflightAsync(AgentState state, AgentCredential credential, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.TargetRelease) || state.TargetSchemaVersion is null
            || state.FromSchemaVersion is null || state.ProtocolVersion is null)
        {
            throw new DeployStepException(DeployPhases.PreflightFailed, "État Verified incomplet.");
        }

        if (credential.ServerInstanceId is null || credential.ServerInstanceId == Guid.Empty)
        {
            throw new DeployStepException(DeployPhases.PreflightFailed, "ServerInstanceId credential requis.");
        }

        if (string.IsNullOrWhiteSpace(_paths.ApiDirectory) || !Directory.Exists(_paths.ApiDirectory))
        {
            throw new DeployStepException(DeployPhases.PreflightFailed, "Dossier Api introuvable.");
        }

        var extractApi = Path.Combine(state.ExtractRoot ?? "", "api");
        var extractMig = Path.Combine(state.ExtractRoot ?? "", "migration");
        if (!Directory.Exists(extractApi) || !Directory.Exists(extractMig))
        {
            throw new DeployStepException(DeployPhases.PreflightFailed, "Extract Verified introuvable.");
        }

        var available = _disk.GetAvailableBytes(_paths.Backups);
        if (available < _options.MinFreeDiskBytes)
        {
            throw new DeployStepException(DeployPhases.PreflightFailed, $"Disque insuffisant ({available}).");
        }

        var current = await _migrations.GetCurrentSchemaVersionAsync(cancellationToken);
        SchemaCompatibility.Ensure(current, state.FromSchemaVersion.Value, state.TargetSchemaVersion.Value);
        state.SchemaBefore = current;
        _swapper.PrepareIncoming(extractApi, state.TargetRelease);
    }

    private async Task StopApiAsync(AgentState state, CancellationToken cancellationToken)
    {
        try
        {
            await _apiService.StopAsync(TimeSpan.FromSeconds(Math.Max(5, _options.StopTimeoutSeconds)), cancellationToken);
        }
        catch (Exception ex)
        {
            throw new DeployStepException(DeployPhases.ApiStopFailed, "Arrêt ErpScolaireApi échoué.", ex);
        }
    }

    private async Task BackupIfNeededAsync(AgentState state, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(state.BackupFilePath) && File.Exists(state.BackupFilePath)
            && state.BackupBytes is > 0)
        {
            return;
        }

        try
        {
            var result = await _backup.CreateVerifiedBackupAsync(
                state.TargetRelease!,
                state.FromSchemaVersion!.Value,
                state.TargetSchemaVersion!.Value,
                cancellationToken);
            if (!result.IntegrityVerified || result.ByteSize < _options.MinBackupBytes)
            {
                throw new MigrationException("VERIFYONLY / taille backup invalide.");
            }

            state.BackupFilePath = result.BackupFilePath;
            state.BackupBytes = result.ByteSize;
            state.BackupTakenAtUtc = result.TakenAtUtc;
        }
        catch (Exception ex) when (ex is not DeployStepException)
        {
            throw new DeployStepException(DeployPhases.BackupFailed, ex.Message, ex);
        }
    }

    private async Task MigrateAsync(AgentState state, CancellationToken cancellationToken)
    {
        var migDir = Path.Combine(state.ExtractRoot!, "migration");
        var current = await _migrations.GetCurrentSchemaVersionAsync(cancellationToken);
        SchemaCompatibility.Ensure(current, state.FromSchemaVersion!.Value, state.TargetSchemaVersion!.Value);
        if (current == state.TargetSchemaVersion)
        {
            state.SchemaAfter = current;
            return;
        }

        try
        {
            var applied = await _migrations.ApplyLocalPackageAsync(migDir, cancellationToken);
            state.SchemaAfter = applied.CurrentVersion;
            if (state.SchemaAfter != state.TargetSchemaVersion)
            {
                throw new MigrationException(
                    $"Schéma {state.SchemaAfter} ≠ cible {state.TargetSchemaVersion}.");
            }
        }
        catch (Exception ex)
        {
            throw new DeployStepException(DeployPhases.MigrationFailed, ex.Message, ex);
        }
    }

    private void Swap(AgentState state)
    {
        var layout = _swapper.Inspect(state.TargetRelease!);
        if (layout.Kind == ApiLayoutKind.Ambiguous)
        {
            throw new DeployStepException(
                DeployPhases.RollbackFailed,
                "Disposition Api/Previous/Incoming ambiguë : " + layout.Detail);
        }

        try
        {
            _swapper.SwapToIncoming(state.TargetRelease!);
        }
        catch (Exception ex) when (ex is not DeployStepException)
        {
            throw new DeployStepException(DeployPhases.ApiStageFailed, ex.Message, ex);
        }
    }

    private async Task StartApiAsync(AgentState state, CancellationToken cancellationToken)
    {
        try
        {
            await _apiService.StartAsync(TimeSpan.FromSeconds(Math.Max(5, _options.StartTimeoutSeconds)), cancellationToken);
        }
        catch (Exception ex)
        {
            throw new DeployStepException(DeployPhases.ApiStartFailed, "Démarrage ErpScolaireApi échoué.", ex);
        }
    }

    private async Task HealthAsync(AgentState state, AgentCredential credential, CancellationToken cancellationToken)
    {
        var budget = TimeSpan.FromSeconds(Math.Max(1, _options.HealthBudgetSeconds));
        var interval = TimeSpan.FromMilliseconds(Math.Max(1, _options.HealthIntervalMs));
        var need = Math.Max(1, _options.HealthSuccessRequired);
        var started = DateTime.UtcNow;
        var streak = 0;
        string? last = null;
        while (DateTime.UtcNow - started < budget)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var once = await _health.CheckOnceAsync(
                _options.HealthUrl,
                state.TargetRelease!,
                state.ProtocolVersion!.Value,
                state.TargetSchemaVersion!.Value,
                credential.ServerInstanceId!.Value,
                cancellationToken);
            if (once.Ok)
            {
                streak++;
                if (streak >= need)
                {
                    return;
                }
            }
            else
            {
                streak = 0;
                last = once.Error;
            }

            await Task.Delay(interval, cancellationToken);
        }

        throw new DeployStepException(DeployPhases.HealthCheckFailed, last ?? "Health timeout.");
    }

    private void Complete(AgentState state)
    {
        state.Phase = DeployPhases.Idle;
        state.LastResult = AgentResults.Completed;
        state.LastError = null;
        state.CurrentRelease = state.TargetRelease;
        if (!string.IsNullOrWhiteSpace(state.BackupFilePath))
        {
            var path = Path.GetFullPath(state.BackupFilePath);
            if (!state.CompletedBackupPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                state.CompletedBackupPaths.Add(path);
            }
        }

        BackupRetention.Prune(_paths.Backups, state);
    }

    private async Task RollbackAsync(AgentState state, CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await _apiService.StopAsync(TimeSpan.FromSeconds(_options.StopTimeoutSeconds), cancellationToken);
            }
            catch
            {
                // best-effort stop before restore
            }

            var schemaMoved = state.SchemaAfter is int after
                              && state.SchemaBefore is int before
                              && after > before;
            if (schemaMoved)
            {
                await _restore.RestoreQuiescedBackupAsync(
                    new SchoolDatabaseRestoreRequest(
                        state.BackupFilePath ?? "",
                        state.BackupFilePath ?? "",
                        _paths.Backups,
                        _options.ExpectedDatabaseName),
                    cancellationToken);
                state.SchemaAfter = state.SchemaBefore;
            }

            _swapper.RollbackToPrevious(state.TargetRelease ?? "unknown");
            await _apiService.StartAsync(TimeSpan.FromSeconds(_options.StartTimeoutSeconds), cancellationToken);
            Persist(state, DeployPhases.RollbackSucceeded);
            state.LastResult = DeployPhases.RollbackSucceeded;
            state.LastError = null;
        }
        catch (Exception ex)
        {
            throw new DeployStepException(DeployPhases.RollbackFailed, ex.Message, ex);
        }
    }

    private async Task<AgentState> HandleFailureAsync(AgentState state, Exception ex, CancellationToken cancellationToken)
    {
        var step = (ex as DeployStepException)?.Phase ?? DeployPhases.Failed;
        var message = AgentCycle.Sanitize(ex.Message, null);
        _log.LogError(ex, "Déploiement échoué phase={Phase}", step);
        state.LastError = message;
        state.LastResult = step;

        if (step is DeployPhases.MigrationFailed)
        {
            state.Phase = DeployPhases.MigrationFailed;
            try
            {
                await _apiService.StartAsync(TimeSpan.FromSeconds(_options.StartTimeoutSeconds), cancellationToken);
            }
            catch (Exception startEx)
            {
                state.LastError = message + " ; restart ancienne API : " + startEx.Message;
            }

            return Save(state);
        }

        if (step is DeployPhases.ApiStartFailed or DeployPhases.HealthCheckFailed or DeployPhases.ApiStageFailed)
        {
            state.Phase = DeployPhases.RollbackRequired;
            Save(state);
            try
            {
                await RollbackAsync(state, cancellationToken);
                return Save(state);
            }
            catch (Exception rollbackEx)
            {
                state.Phase = DeployPhases.RollbackFailed;
                state.LastResult = DeployPhases.RollbackFailed;
                state.LastError = AgentCycle.Sanitize(rollbackEx.Message, null);
                return Save(state);
            }
        }

        if (step is DeployPhases.RollbackFailed)
        {
            state.Phase = DeployPhases.RollbackFailed;
            return Save(state);
        }

        if (step is DeployPhases.BackupFailed or DeployPhases.ApiStopFailed or DeployPhases.PreflightFailed)
        {
            state.Phase = step;
            if (step is DeployPhases.BackupFailed or DeployPhases.PreflightFailed)
            {
                try
                {
                    await _apiService.StartAsync(TimeSpan.FromSeconds(_options.StartTimeoutSeconds), cancellationToken);
                }
                catch
                {
                    // already logged via LastError
                }
            }

            return Save(state);
        }

        state.Phase = step;
        return Save(state);
    }

    private void Persist(AgentState state, string phase)
    {
        state.Phase = phase;
        state.LastError = null;
        Save(state);
    }

    private AgentState Save(AgentState state)
    {
        _store.Save(state);
        return state;
    }
}

internal sealed class DeployStepException : Exception
{
    public string Phase { get; }

    public DeployStepException(string phase, string message, Exception? inner = null)
        : base(message, inner)
    {
        Phase = phase;
    }
}
