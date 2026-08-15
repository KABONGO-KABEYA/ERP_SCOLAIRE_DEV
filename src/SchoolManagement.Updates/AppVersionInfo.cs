using System.Reflection;
using System.Text.Json;

namespace SchoolManagement.Updates;

/// <summary>
/// Résout la version produit affichée / envoyée au check de mise à jour.
/// <para>
/// Source de vérité unique : la version compilée dans l'assembly / csproj.
/// <c>version.json</c> n'est qu'un fallback si l'assembly n'expose aucune version utilisable.
/// Il ne doit jamais primer sur l'assembly (ex. Assembly=1.2.0 et version.json=1.1.0 → 1.2.0).
/// </para>
/// Priorité :
/// <list type="number">
/// <item>AssemblyInformationalVersion (csproj / InformationalVersion)</item>
/// <item>AssemblyFileVersion</item>
/// <item>version.json à côté de l'exécutable (fallback uniquement)</item>
/// <item>0.0.0</item>
/// </list>
/// <c>Updates:CurrentVersion</c> dans appsettings n'est pas une source de vérité.
/// </summary>
public static class AppVersionInfo
{
    public const string FallbackFileName = "version.json";

    public static string ResolveFromAssembly(Assembly assembly, string? appBaseDirectory = null)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                          ?? assembly.GetName().Version?.ToString();
        return Resolve(informational, fileVersion, appBaseDirectory);
    }

    public static string Resolve(
        string? informationalVersion,
        string? fileVersion = null,
        string? versionJsonDirectory = null)
    {
        if (TryNormalizeUsable(informationalVersion, out var fromInformational))
        {
            return fromInformational;
        }

        if (TryNormalizeUsable(fileVersion, out var fromFile))
        {
            return fromFile;
        }

        if (TryReadVersionJson(versionJsonDirectory, out var fromJson)
            && TryNormalizeUsable(fromJson, out var normalizedJson))
        {
            return normalizedJson;
        }

        return "0.0.0";
    }

    /// <summary>
    /// Écrase <see cref="UpdateSettings.CurrentVersion"/> avec la version assembly (jamais l'inverse).
    /// Corrige les anciens fichiers qui restaient bloqués sur 1.0.0 issu d'appsettings.
    /// </summary>
    public static void ApplyToSettings(UpdateSettings settings, string resolvedVersion)
    {
        settings.CurrentVersion = resolvedVersion;
    }

    public static bool TryNormalizeUsable(string? raw, out string normalized)
    {
        normalized = "0.0.0";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parsed = VersionManager.Parse(raw);
        if (parsed.IsZero)
        {
            return false;
        }

        normalized = parsed.ToNormalizedString();
        return true;
    }

    private static bool TryReadVersionJson(string? directory, out string? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var path = Path.Combine(directory, FallbackFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                version = doc.RootElement.GetString();
                return !string.IsNullOrWhiteSpace(version);
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (property.Name.Equals("version", StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.String)
                    {
                        version = property.Value.GetString();
                        return !string.IsNullOrWhiteSpace(version);
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
