namespace SchoolManagement.Updates;

/// <summary>
/// Version produit d'un DLL (InformationalVersion / ProductVersion), sans métadonnée <c>+git</c>.
/// </summary>
public static class ReleaseVersionStamp
{
    public static string FromInformational(string? informationalVersion)
    {
        var parsed = VersionManager.Parse(informationalVersion);
        if (parsed.IsZero)
        {
            throw new MigrationException("Version API introuvable dans l'assembly.");
        }

        return parsed.ToNormalizedString();
    }

    public static void EnsureMatchesRelease(string? informationalVersion, string releaseVersion)
    {
        var actual = FromInformational(informationalVersion);
        var expected = VersionManager.Parse(releaseVersion);
        if (expected.IsZero)
        {
            throw new MigrationException("Version de release SemVer invalide.");
        }

        if (!string.Equals(actual, expected.ToNormalizedString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException($"Version API {actual} ≠ release {expected.ToNormalizedString()}.");
        }
    }
}
