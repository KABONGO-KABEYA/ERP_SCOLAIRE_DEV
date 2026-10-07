using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using SchoolManagement.Setup;

namespace SchoolManagement.Update;

internal static class InstalledVersionReader
{
    internal const string LastSetupReferenceVersion = "1.0.2";
    internal static string UpdatePackageVersion => Normalize(
        typeof(InstalledVersionReader).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Version inconnue");
    internal const string ReferenceSetupCommit = "bf2750c";

    internal static string ReadFromDesktop(string desktopDirectory)
    {
        var exe = Path.Combine(desktopDirectory, "SchoolManagement.Desktop.exe");
        if (!File.Exists(exe))
            return LastSetupReferenceVersion;

        try
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                return Normalize(info.ProductVersion);
            if (!string.IsNullOrWhiteSpace(info.FileVersion))
                return Normalize(info.FileVersion);
        }
        catch
        {
            // ignore
        }

        var versionJson = Path.Combine(desktopDirectory, "version.json");
        if (File.Exists(versionJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(versionJson));
                if (doc.RootElement.TryGetProperty("version", out var v))
                {
                    var text = v.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return Normalize(text);
                }
            }
            catch
            {
                // ignore
            }
        }

        return LastSetupReferenceVersion;
    }

    internal static string ReadFromPayloadDesktop(string payloadDesktopDirectory) =>
        ReadFromDesktop(payloadDesktopDirectory);

    private static string Normalize(string version)
    {
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version.Trim();
    }
}

internal static class PayloadFilePreserver
{
    internal static readonly string[] ApiPreservedFileNames =
    [
        "ServeurDonnees.txt",
        "ServeurDonneesCloud.txt",
        "ServeurFichiers.txt",
        "appsettings.json",
        "appsettings.Production.json",
        "appsettings.Development.json",
        "appsettings.Local.json",
        "secrets.json",
        "ServerIdentity.json",
    ];

    internal static readonly string[] DesktopPreservedFileNames =
    [
        "appsettings.json",
        "ServeurDonnees.txt",
        "appsettings.Local.json",
        "secrets.json",
    ];

    internal static readonly string[] PreservedDirectoryNames = ["logs"];

    internal static Dictionary<string, byte[]> SnapshotFiles(string root, IReadOnlyList<string> fileNames)
    {
        var snapshot = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in fileNames)
        {
            var path = Path.Combine(root, name);
            if (File.Exists(path))
                snapshot[name] = File.ReadAllBytes(path);
        }

        return snapshot;
    }

    internal static void RestoreFiles(string root, IReadOnlyDictionary<string, byte[]> snapshot)
    {
        foreach (var (name, bytes) in snapshot)
        {
            var path = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
    }

    internal static void CopyPayloadDirectory(
        string source,
        string target,
        IReadOnlyList<string> preservedFileNames,
        IReadOnlyDictionary<string, byte[]> preservedSnapshot)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, dir);
            if (IsUnderPreservedDirectory(relative))
                continue;

            Directory.CreateDirectory(Path.Combine(target, relative));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (IsUnderPreservedDirectory(relative))
                continue;

            var fileName = Path.GetFileName(file);
            if (preservedFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase)
                && preservedSnapshot.ContainsKey(fileName))
            {
                continue;
            }

            var dest = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        RestoreFiles(target, preservedSnapshot);
    }

    private static bool IsUnderPreservedDirectory(string relativePath)
    {
        foreach (var dir in PreservedDirectoryNames)
        {
            if (relativePath.Equals(dir, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(dir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static void UnblockFiles(string root)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                var zone = file + ":Zone.Identifier";
                if (File.Exists(zone))
                    File.Delete(zone);
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -LiteralPath '{root.Replace("'", "''")}' -Recurse -File | Unblock-File\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(120_000);
        }
        catch
        {
            // ignore
        }
    }
}
