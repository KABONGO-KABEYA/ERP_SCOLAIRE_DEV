namespace SchoolManagement.UpdateAgent;

public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}

/// <summary>DPAPI CurrentUser — le secret n'est lisible que par le compte du service.</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    public const string Prefix = "DPAPI:";

    private static readonly byte[] Entropy =
        System.Text.Encoding.UTF8.GetBytes("SchoolManagement.UpdateAgent.v1");

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new AgentException("Secret vide.");
        }

        var bytes = System.Security.Cryptography.ProtectedData.Protect(
            System.Text.Encoding.UTF8.GetBytes(plaintext),
            Entropy,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)
            || !protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new AgentException("Credential DPAPI invalide.");
        }

        var bytes = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        var plain = System.Security.Cryptography.ProtectedData.Unprotect(
            bytes,
            Entropy,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(plain);
    }
}

public sealed class AgentCredentialFile
{
    public Guid ClientId { get; set; }

    public int CredentialVersion { get; set; }

    public Guid SchoolId { get; set; }

    public Guid? ServerInstanceId { get; set; }

    public string ClientSecretProtected { get; set; } = string.Empty;
}

public sealed class AgentCredential
{
    public Guid ClientId { get; init; }

    public int CredentialVersion { get; init; }

    public Guid SchoolId { get; init; }

    public Guid? ServerInstanceId { get; init; }

    public string ClientSecret { get; init; } = string.Empty;
}

public sealed class AgentCredentialStore
{
    private readonly AgentPaths _paths;
    private readonly ISecretProtector _protector;

    public AgentCredentialStore(AgentPaths paths, ISecretProtector protector)
    {
        _paths = paths;
        _protector = protector;
    }

    public void Save(AgentCredential credential)
    {
        if (credential.ClientId == Guid.Empty || string.IsNullOrWhiteSpace(credential.ClientSecret))
        {
            throw new AgentException("Credential incomplet.");
        }

        _paths.EnsureDirectories();
        _paths.EnsureNotApiInstall(_paths.CredentialFile);
        var file = new AgentCredentialFile
        {
            ClientId = credential.ClientId,
            CredentialVersion = credential.CredentialVersion,
            SchoolId = credential.SchoolId,
            ServerInstanceId = credential.ServerInstanceId,
            ClientSecretProtected = _protector.Protect(credential.ClientSecret),
        };
        var json = System.Text.Json.JsonSerializer.Serialize(file, JsonOpts.File);
        File.WriteAllText(_paths.CredentialFile, json);
        if (File.ReadAllText(_paths.CredentialFile).Contains(credential.ClientSecret, StringComparison.Ordinal))
        {
            File.Delete(_paths.CredentialFile);
            throw new AgentException("Refus d'écrire le secret en clair.");
        }
    }

    public AgentCredential Load()
    {
        if (!File.Exists(_paths.CredentialFile))
        {
            throw new AgentException($"Credential introuvable : {_paths.CredentialFile}");
        }

        var file = System.Text.Json.JsonSerializer.Deserialize<AgentCredentialFile>(
                       File.ReadAllText(_paths.CredentialFile), JsonOpts.File)
                   ?? throw new AgentException("Credential JSON vide.");
        if (!file.ClientSecretProtected.StartsWith(DpapiSecretProtector.Prefix, StringComparison.Ordinal))
        {
            throw new AgentException("Le secret doit être protégé par DPAPI.");
        }

        return new AgentCredential
        {
            ClientId = file.ClientId,
            CredentialVersion = file.CredentialVersion,
            SchoolId = file.SchoolId,
            ServerInstanceId = file.ServerInstanceId,
            ClientSecret = _protector.Unprotect(file.ClientSecretProtected),
        };
    }
}

internal static class JsonOpts
{
    internal static readonly System.Text.Json.JsonSerializerOptions File = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    internal static readonly System.Text.Json.JsonSerializerOptions Http = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
