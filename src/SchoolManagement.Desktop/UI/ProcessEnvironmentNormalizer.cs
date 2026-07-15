using System.IO;
using Microsoft.Win32;

namespace SchoolManagement.Desktop.UI;

/// <summary>
/// Corrige les variables d'environnement héritées d'outils de build (ex. Flutter build_home)
/// qui redirigent USERPROFILE vers un dossier incomplet et cassent les boîtes de dialogue fichiers.
/// </summary>
public static class ProcessEnvironmentNormalizer
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied)
        {
            return;
        }

        _applied = true;
        NormalizeUserProfile();
    }

    private static void NormalizeUserProfile()
    {
        var profile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (IsHealthyProfile(profile))
        {
            return;
        }

        var resolved = ResolveRealUserProfileDirectory();
        if (resolved is null)
        {
            return;
        }

        Environment.SetEnvironmentVariable("USERPROFILE", resolved);
        Environment.SetEnvironmentVariable("HOME", resolved);

        var root = Path.GetPathRoot(resolved);
        if (!string.IsNullOrWhiteSpace(root))
        {
            Environment.SetEnvironmentVariable("HOMEDRIVE", root.TrimEnd('\\', '/'));
            var homePath = resolved[root.Length..];
            Environment.SetEnvironmentVariable("HOMEPATH", string.IsNullOrWhiteSpace(homePath) ? "\\" : homePath);
        }
    }

    public static string? ResolveRealUserProfileDirectory()
    {
        if (!string.IsNullOrWhiteSpace(Environment.UserName))
        {
            var fromUserName = Path.Combine(@"C:\Users", Environment.UserName);
            if (Directory.Exists(fromUserName))
            {
                return fromUserName;
            }
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
            if (key is null)
            {
                return null;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(subKeyName);
                var imagePath = subKey?.GetValue("ProfileImagePath") as string;
                if (string.IsNullOrWhiteSpace(imagePath)
                    || !Directory.Exists(imagePath)
                    || imagePath.Contains("build_home", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(Environment.UserName)
                    && imagePath.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    return imagePath;
                }
            }
        }
        catch
        {
            // Ignore registry access issues.
        }

        return null;
    }

    private static bool IsHealthyProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return false;
        }

        if (profile.Contains("build_home", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(profile, "Desktop"))
               || Directory.Exists(Path.Combine(profile, "Documents"))
               || Directory.Exists(Path.Combine(profile, "Pictures"));
    }
}
