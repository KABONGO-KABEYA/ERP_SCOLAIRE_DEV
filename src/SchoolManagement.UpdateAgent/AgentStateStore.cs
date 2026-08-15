namespace SchoolManagement.UpdateAgent;

public static class DeployPhases
{
    public const string Idle = "Idle";
    public const string ReleaseDetected = "ReleaseDetected";
    public const string Downloaded = "Downloaded";
    public const string Verified = "Verified";
    public const string Preflight = "Preflight";
    public const string ApiStopped = "ApiStopped";
    public const string BackupCreated = "BackupCreated";
    public const string MigrationSucceeded = "MigrationSucceeded";
    public const string ApiStaged = "ApiStaged";
    public const string ApiStarted = "ApiStarted";
    public const string HealthChecking = "HealthChecking";
    public const string Completed = "Completed";

    public const string DownloadFailed = "DownloadFailed";
    public const string VerificationFailed = "VerificationFailed";
    public const string PreflightFailed = "PreflightFailed";
    public const string ApiStopFailed = "ApiStopFailed";
    public const string BackupFailed = "BackupFailed";
    public const string MigrationFailed = "MigrationFailed";
    public const string ApiStageFailed = "ApiStageFailed";
    public const string ApiStartFailed = "ApiStartFailed";
    public const string HealthCheckFailed = "HealthCheckFailed";
    public const string RollbackRequired = "RollbackRequired";
    public const string RollbackSucceeded = "RollbackSucceeded";
    public const string RollbackFailed = "RollbackFailed";

    public const string Failed = "Failed";

    /// <summary>
    /// Reprise d'un déploiement déjà engagé. Verified n'est repris que si AutoDeploy=true
    /// (sinon production reste en check/staging).
    /// </summary>
    public static bool ShouldResume(string? phase) =>
        phase is Preflight or ApiStopped or BackupCreated or MigrationSucceeded
            or ApiStaged or ApiStarted or HealthChecking or RollbackRequired or Completed;
}

public static class AgentResults
{
    public const string Downloaded = "Downloaded";
    public const string SkippedIdempotent = "SkippedIdempotent";
    public const string NoRelease = "NoRelease";
    public const string IgnoredDesktopOnly = "IgnoredDesktopOnly";
    public const string Failed = "Failed";
    public const string Completed = "Completed";
}

public sealed class AgentState
{
    public string? Phase { get; set; }

    public string? CurrentRelease { get; set; }

    public string? TargetRelease { get; set; }

    public Guid? TargetReleaseId { get; set; }

    public int? FromSchemaVersion { get; set; }

    public int? TargetSchemaVersion { get; set; }

    public int? ProtocolVersion { get; set; }

    public int? SchemaBefore { get; set; }

    public int? SchemaAfter { get; set; }

    public string? ExtractRoot { get; set; }

    public string? BackupFilePath { get; set; }

    public long? BackupBytes { get; set; }

    public DateTime? BackupTakenAtUtc { get; set; }

    public List<string> CompletedBackupPaths { get; set; } = [];

    public DateTime? LastCheckUtc { get; set; }

    public DateTime? LastDownloadUtc { get; set; }

    public string? LastResult { get; set; }

    public string? LastError { get; set; }
}

public sealed class AgentStateStore
{
    private readonly AgentPaths _paths;

    public AgentStateStore(AgentPaths paths) => _paths = paths;

    public AgentState Load()
    {
        if (!File.Exists(_paths.StateFile))
        {
            return new AgentState { Phase = DeployPhases.Idle };
        }

        return System.Text.Json.JsonSerializer.Deserialize<AgentState>(
                   File.ReadAllText(_paths.StateFile), JsonOpts.File)
               ?? new AgentState { Phase = DeployPhases.Idle };
    }

    public void Save(AgentState state)
    {
        _paths.EnsureDirectories();
        _paths.EnsureNotApiInstall(_paths.StateFile);
        var json = System.Text.Json.JsonSerializer.Serialize(state, JsonOpts.File);
        if (json.Contains("clientSecret", StringComparison.OrdinalIgnoreCase)
            || json.Contains("ClientSecret", StringComparison.Ordinal))
        {
            throw new AgentException("L'état ne doit pas contenir le secret.");
        }

        File.WriteAllText(_paths.StateFile, json);
    }
}
