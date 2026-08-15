using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public interface IApiDirectorySwapper
{
    void PrepareIncoming(string extractApiDirectory, string version);

    void SwapToIncoming(string version);

    void RollbackToPrevious(string version);

    ApiLayoutStatus Inspect(string version);
}

public enum ApiLayoutKind
{
    ReadyToSwap,
    ResumeIncomingRename,
    AlreadyTarget,
    Ambiguous,
}

public sealed record ApiLayoutStatus(ApiLayoutKind Kind, string Detail);

public sealed class ApiDirectorySwapper : IApiDirectorySwapper
{
    private static readonly string[] PreserveFromLive =
    [
        ..AppSchemaContract.ApiPublishSecretFileNames,
        "ServerIdentity.json",
        "ServerIdentity.json.bak",
    ];

    private readonly AgentPaths _paths;

    public ApiDirectorySwapper(AgentPaths paths) => _paths = paths;

    public void PrepareIncoming(string extractApiDirectory, string version)
    {
        var api = RequireApi();
        var incoming = _paths.ApiIncoming(version);
        EnsureSameVolume(api, incoming);
        if (!Directory.Exists(extractApiDirectory))
        {
            throw new AgentException("Package API extrait introuvable.");
        }

        if (Directory.Exists(incoming))
        {
            Directory.Delete(incoming, recursive: true);
        }

        CopyDirectory(extractApiDirectory, incoming);
        foreach (var name in PreserveFromLive)
        {
            var source = Path.Combine(api, name);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(incoming, name), overwrite: true);
            }
        }
    }

    public void SwapToIncoming(string version)
    {
        var status = Inspect(version);
        if (status.Kind == ApiLayoutKind.Ambiguous)
        {
            throw new AgentException("Disposition Api/Previous/Incoming ambiguë : " + status.Detail);
        }

        if (status.Kind == ApiLayoutKind.AlreadyTarget)
        {
            return;
        }

        var api = RequireApi();
        var incoming = _paths.ApiIncoming(version);
        var previous = _paths.ApiPrevious();
        EnsureSameVolume(api, incoming);

        if (status.Kind == ApiLayoutKind.ResumeIncomingRename)
        {
            Directory.Move(incoming, api);
            return;
        }

        if (Directory.Exists(previous))
        {
            Directory.Delete(previous, recursive: true);
        }

        Directory.Move(api, previous);
        Directory.Move(incoming, api);
    }

    public void RollbackToPrevious(string version)
    {
        var api = _paths.ApiDirectory ?? throw new AgentException("ApiInstallRoot requis.");
        var previous = _paths.ApiPrevious();
        var failed = api + ".Failed-" + version.Replace('.', '-');
        if (!Directory.Exists(previous))
        {
            throw new AgentException("Api.Previous absent : rollback fichiers impossible.");
        }

        if (Directory.Exists(api))
        {
            if (Directory.Exists(failed))
            {
                Directory.Delete(failed, recursive: true);
            }

            Directory.Move(api, failed);
        }

        Directory.Move(previous, api);
    }

    public ApiLayoutStatus Inspect(string version)
    {
        var api = _paths.ApiDirectory;
        var incoming = _paths.ApiIncoming(version);
        var previous = _paths.ApiPrevious();
        var extras = _paths.IncomingDirectories()
            .Where(d => !d.Equals(incoming, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var apiExists = api is not null && Directory.Exists(api);
        var incomingExists = Directory.Exists(incoming);
        var previousExists = Directory.Exists(previous);

        if (extras.Count > 0 && (!apiExists || !incomingExists))
        {
            return new ApiLayoutStatus(ApiLayoutKind.Ambiguous, "plusieurs Incoming");
        }

        if (apiExists && incomingExists)
        {
            return new ApiLayoutStatus(ApiLayoutKind.ReadyToSwap, "pré-swap");
        }

        if (!apiExists && incomingExists && previousExists)
        {
            return new ApiLayoutStatus(ApiLayoutKind.ResumeIncomingRename, "Incoming→Api");
        }

        if (apiExists && !incomingExists && IsTargetApi(api!, version))
        {
            return new ApiLayoutStatus(ApiLayoutKind.AlreadyTarget, "déjà cible");
        }

        if (apiExists && !incomingExists && !IsTargetApi(api!, version))
        {
            return new ApiLayoutStatus(ApiLayoutKind.Ambiguous, "Api présent sans Incoming ni manifeste cible");
        }

        if (!apiExists && !incomingExists)
        {
            return new ApiLayoutStatus(ApiLayoutKind.Ambiguous, "Api et Incoming absents");
        }

        return new ApiLayoutStatus(ApiLayoutKind.Ambiguous, "combinaison de dossiers incohérente");
    }

    private string RequireApi() =>
        _paths.ApiDirectory ?? throw new AgentException("ApiInstallRoot requis.");

    private static bool IsTargetApi(string apiDir, string version)
    {
        var manifest = Path.Combine(apiDir, AppSchemaContract.ApiManifestFileName);
        if (!File.Exists(manifest))
        {
            return false;
        }

        try
        {
            var loaded = ApiArtifactManifest.Load(apiDir);
            return string.Equals(
                VersionManager.Parse(loaded.ReleaseVersion).ToNormalizedString(),
                VersionManager.Parse(version).ToNormalizedString(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSameVolume(string a, string b)
    {
        var ra = Path.GetPathRoot(Path.GetFullPath(a));
        var rb = Path.GetPathRoot(Path.GetFullPath(b));
        if (!string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentException("Incoming et Api doivent être sur le même volume NTFS.");
        }
    }

    internal static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(dest, rel));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
