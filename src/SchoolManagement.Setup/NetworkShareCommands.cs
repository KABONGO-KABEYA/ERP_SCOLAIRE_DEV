using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace SchoolManagement.Setup;

/// <summary>Construction des commandes net share (chemin local uniquement, jamais /y).</summary>
internal static class NetworkShareCommands
{
    internal const string EveryoneSidValue = "S-1-1-0";

    internal static string BuildDeleteArguments(string shareName)
    {
        if (string.IsNullOrWhiteSpace(shareName))
            throw new ArgumentException("Nom de partage requis.", nameof(shareName));
        return $"share {shareName.Trim()} /delete";
    }

    internal static string BuildCreateArguments(string shareName, string localPath)
    {
        if (string.IsNullOrWhiteSpace(shareName))
            throw new ArgumentException("Nom de partage requis.", nameof(shareName));
        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("Chemin local requis.", nameof(localPath));
        if (IsUnc(localPath))
            throw new InvalidOperationException(
                $"net share exige un chemin local, pas un UNC : {localPath}");

        var everyone = ResolveEveryoneAccountName();
        return $"share {shareName.Trim()}=\"{localPath}\" /GRANT:{FormatGrantPrincipal(everyone)},FULL";
    }

    /// <summary>Nom localisé du groupe Everyone (SID S-1-1-0), ex. « Tout le monde » ou « Everyone ».</summary>
    internal static string ResolveEveryoneAccountName()
    {
        try
        {
            var sid = new SecurityIdentifier(EveryoneSidValue);
            var account = (NTAccount)sid.Translate(typeof(NTAccount));
            var name = account.Value?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    $"Le SID {EveryoneSidValue} s'est traduit vers un nom vide.");
            }

            return name;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Impossible de résoudre le SID {EveryoneSidValue} (groupe Everyone) vers un nom de compte Windows.",
                ex);
        }
    }

    internal static string FormatGrantPrincipal(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("Nom de compte requis.", nameof(accountName));
        return accountName.Contains(' ', StringComparison.Ordinal) ? $"\"{accountName}\"" : accountName;
    }

    internal static string BuildUncAccessPath(string shareName) =>
        $@"\\{Environment.MachineName}\{shareName.Trim()}";

    internal static bool IsUnc(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal);

    internal static bool TryResolveLocalPath(string storageRoot, out string localPath, out string error)
    {
        localPath = "";
        error = "";
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            error = "Chemin de stockage vide.";
            return false;
        }

        var root = storageRoot.Trim();
        if (!IsUnc(root))
        {
            localPath = Path.GetFullPath(root);
            return true;
        }

        var parts = root.TrimEnd('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = $"Chemin UNC invalide : {root}";
            return false;
        }

        if (!IsLocalHost(parts[0]))
        {
            error =
                $"Le dossier '{root}' est un UNC distant. " +
                "net share NAME=PATH doit recevoir le chemin LOCAL du dossier sur cette machine.";
            return false;
        }

        var shareLocalRoot = TryGetLanmanShareLocalPath(parts[1]);
        if (string.IsNullOrWhiteSpace(shareLocalRoot))
        {
            error =
                $"Impossible de résoudre le partage '{parts[1]}' vers un chemin local " +
                $"(UNC {root}).";
            return false;
        }

        localPath = parts.Length == 2
            ? shareLocalRoot
            : Path.GetFullPath(Path.Combine(new[] { shareLocalRoot }.Concat(parts.Skip(2)).ToArray()));
        return true;
    }

    internal static bool IsLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;
        if (host is "." or "localhost" or "127.0.0.1")
            return true;
        return host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? TryGetLanmanShareLocalPath(string shareName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\LanmanServer\Shares", writable: false);
            if (key?.GetValue(shareName) is not string[] lines)
                return null;

            foreach (var line in lines)
            {
                if (line.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                    return line["Path=".Length..];
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
