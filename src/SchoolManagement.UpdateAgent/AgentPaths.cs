namespace SchoolManagement.UpdateAgent;

public sealed class AgentPaths
{
    public string Root { get; }

    public string Packages { get; }

    public string Staging { get; }

    public string StateDirectory { get; }

    public string Logs { get; }

    public string CredentialFile { get; }

    public string StateFile { get; }

    public string Backups { get; }

    public string? ApiDirectory { get; }

    public string? InstallParent { get; }

    public string? ForbiddenApiRoot { get; }

    public AgentPaths(string? dataRoot, string? apiInstallRoot = null, string? backupRoot = null)
    {
        Root = string.IsNullOrWhiteSpace(dataRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                AgentServiceNames.ProgramDataFolder,
                AgentServiceNames.DataFolderName)
            : Path.GetFullPath(dataRoot.Trim());

        Packages = Path.Combine(Root, "packages");
        Staging = Path.Combine(Root, "staging");
        StateDirectory = Path.Combine(Root, "state");
        Logs = Path.Combine(Root, "logs");
        CredentialFile = Path.Combine(Root, "credential", "agent-credential.json");
        StateFile = Path.Combine(StateDirectory, "agent-state.json");

        var erpRoot = Directory.GetParent(Root)?.FullName
                      ?? Path.Combine(
                          Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                          AgentServiceNames.ProgramDataFolder);
        Backups = string.IsNullOrWhiteSpace(backupRoot)
            ? Path.Combine(erpRoot, AgentServiceNames.BackupsFolderName)
            : Path.GetFullPath(backupRoot.Trim());

        ForbiddenApiRoot = string.IsNullOrWhiteSpace(apiInstallRoot)
            ? null
            : Path.GetFullPath(apiInstallRoot.Trim());
        ApiDirectory = ForbiddenApiRoot;
        InstallParent = ApiDirectory is null ? null : Directory.GetParent(ApiDirectory)?.FullName;
    }

    public string ApiPrevious() =>
        Path.Combine(RequireParent(), "Api.Previous");

    public string ApiIncoming(string version)
    {
        var safe = string.Join("-", version.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(RequireParent(), "Api.Incoming-" + safe);
    }

    public IReadOnlyList<string> IncomingDirectories()
    {
        var parent = InstallParent;
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            return [];
        }

        return Directory.GetDirectories(parent, "Api.Incoming-*");
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Packages);
        Directory.CreateDirectory(Staging);
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Path.GetDirectoryName(CredentialFile)!);
        Directory.CreateDirectory(Backups);
    }

    public void EnsureNotApiInstall(string path)
    {
        if (string.IsNullOrWhiteSpace(ForbiddenApiRoot))
        {
            return;
        }

        var full = Path.GetFullPath(path);
        var api = ForbiddenApiRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                  + Path.DirectorySeparatorChar;
        if (full.Equals(ForbiddenApiRoot, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(api, StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentException("L'Update Agent ne doit pas écrire dans le dossier API.");
        }
    }

    private string RequireParent() =>
        InstallParent ?? throw new AgentException("ApiInstallRoot parent introuvable.");
}
