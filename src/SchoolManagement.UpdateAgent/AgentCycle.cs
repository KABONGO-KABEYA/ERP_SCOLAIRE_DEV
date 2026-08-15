using Microsoft.Extensions.Logging;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public sealed class AgentCycle
{
    private readonly AgentPaths _paths;
    private readonly AgentCredentialStore _credentials;
    private readonly AgentStateStore _state;
    private readonly IBootstrapAgentClient _bootstrap;
    private readonly IPackageAcquire _packages;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentCycle> _log;
    private readonly IDeployOrchestrator? _deploy;

    public AgentCycle(
        AgentPaths paths,
        AgentCredentialStore credentials,
        AgentStateStore state,
        IBootstrapAgentClient bootstrap,
        IPackageAcquire packages,
        AgentOptions options,
        ILogger<AgentCycle> log,
        IDeployOrchestrator? deploy = null)
    {
        _paths = paths;
        _credentials = credentials;
        _state = state;
        _bootstrap = bootstrap;
        _packages = packages;
        _options = options;
        _log = log;
        _deploy = deploy;
    }

    public async Task<AgentState> RunAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        var state = _state.Load();
        state.LastCheckUtc = DateTime.UtcNow;
        AgentCredential? credential = null;
        try
        {
            if (_deploy is not null && ShouldStartDeploy(state))
            {
                credential = _credentials.Load();
                return await _deploy.RunAsync(state, credential, cancellationToken);
            }

            BootstrapUrlPolicy.EnsureAllowed(_options.BootstrapBaseUrl, _options.AllowedHosts);
            credential = _credentials.Load();
            _log.LogInformation(
                "Cycle agent schoolId={SchoolId} clientId={ClientId} credentialVersion={Version}",
                credential.SchoolId,
                credential.ClientId,
                credential.CredentialVersion);

            var token = await _bootstrap.GetTokenAsync(
                credential.ClientId,
                credential.ClientSecret,
                cancellationToken);
            if (token.SchoolId != credential.SchoolId)
            {
                throw new AgentException("SchoolId JWT ≠ credential local.");
            }

            var check = await _bootstrap.CheckReleaseAsync(
                token.AccessToken,
                string.IsNullOrWhiteSpace(_options.Channel) ? "PROD" : _options.Channel.Trim(),
                cancellationToken);

            AgentReleasePlan? plan;
            try
            {
                plan = ReleaseCheckGuard.Accept(check);
            }
            catch (AgentException ex) when (ex.Message.Contains("Desktop", StringComparison.OrdinalIgnoreCase))
            {
                state.LastResult = AgentResults.IgnoredDesktopOnly;
                state.LastError = ex.Message;
                _state.Save(state);
                _log.LogInformation("Release Desktop seule ignorée.");
                return state;
            }

            if (plan is null)
            {
                state.LastResult = AgentResults.NoRelease;
                state.LastError = null;
                _state.Save(state);
                return state;
            }

            state.TargetRelease = plan.Version;
            state.TargetReleaseId = plan.ReleaseId;
            state.TargetSchemaVersion = plan.SchemaVersion;

            var acquired = await _packages.AcquireAsync(plan, cancellationToken);
            state.LastDownloadUtc = DateTime.UtcNow;
            state.LastResult = acquired.ReusedExisting ? AgentResults.SkippedIdempotent : AgentResults.Downloaded;
            state.LastError = null;
            state.Phase = DeployPhases.Verified;
            state.ExtractRoot = acquired.ExtractRoot;
            state.FromSchemaVersion = plan.FromSchemaVersion;
            state.ProtocolVersion = plan.ProtocolVersion;
            _state.Save(state);
            _log.LogInformation(
                "Packages prêts release={Version} reused={Reused} extract={Extract}",
                plan.Version,
                acquired.ReusedExisting,
                acquired.ExtractRoot);
            if (_deploy is not null && _options.AutoDeploy)
            {
                return await _deploy.RunAsync(state, credential, cancellationToken);
            }

            return state;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Cycle agent échoué (sans secret).");
            state.LastResult = AgentResults.Failed;
            state.Phase = DeployPhases.DownloadFailed;
            state.LastError = Sanitize(ex.Message, credential?.ClientSecret);
            try
            {
                CleanupStagingTemp();
            }
            catch (Exception cleanupEx)
            {
                _log.LogWarning(cleanupEx, "Nettoyage staging.");
            }

            _state.Save(state);
            return state;
        }
    }

    private bool ShouldStartDeploy(AgentState state)
    {
        if (state.Phase == DeployPhases.Verified)
        {
            return _options.AutoDeploy;
        }

        return DeployPhases.ShouldResume(state.Phase);
    }

    private void CleanupStagingTemp()
    {
        var staging = _paths.Staging;
        if (!Directory.Exists(staging))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(staging, "tmp-*.zip"))
        {
            File.Delete(file);
        }
    }

    internal static string Sanitize(string message, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(message))
        {
            return message;
        }

        return message.Replace(secret, "[redacted]", StringComparison.Ordinal);
    }
}
