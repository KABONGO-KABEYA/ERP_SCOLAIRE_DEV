using System.Text.RegularExpressions;

namespace SchoolManagement.Updates;

/// <summary>
/// Whitelist stricte des fichiers .bak restaurables.
/// Aucun chemin UNC / Bootstrap / hors ProgramData Backups.
/// </summary>
public static class SchoolBackupPathGuard
{
    private static readonly Regex DatabaseName = new(
        @"^[A-Za-z_][A-Za-z0-9_]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SystemDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "master", "msdb", "model", "tempdb",
    };

    public static string EnsureAllowed(
        string candidatePath,
        string backupsRoot,
        string expectedPathFromState)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(expectedPathFromState))
        {
            throw new MigrationException("Chemin de backup manquant dans l'état.");
        }

        var full = Path.GetFullPath(candidatePath.Trim());
        var expected = Path.GetFullPath(expectedPathFromState.Trim());
        var root = Path.GetFullPath(backupsRoot.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (full.StartsWith(@"\\", StringComparison.Ordinal) || expected.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new MigrationException("Chemin de backup UNC refusé.");
        }

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !expected.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("Backup hors de %ProgramData%\\ERP_SCOLAIRE\\Backups\\.");
        }

        if (!full.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
            || !expected.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("Le backup doit avoir l'extension .bak.");
        }

        if (!string.Equals(full, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("Backup d'une autre release / hors état du déploiement.");
        }

        return full;
    }

    public static void EnsureDatabaseName(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName) || !DatabaseName.IsMatch(databaseName.Trim()))
        {
            throw new MigrationException("Nom de base ERP invalide.");
        }

        if (SystemDatabaseNames.Contains(databaseName.Trim()))
        {
            throw new MigrationException("Base système SQL refusée.");
        }
    }

    public static string BuildFileName(string databaseName, string releaseVersion, int fromSchema, int toSchema)
    {
        EnsureDatabaseName(databaseName);
        var safeVersion = string.Join("-", (releaseVersion ?? "0.0.0").Split(Path.GetInvalidFileNameChars()));
        return $"{databaseName}_{DateTime.UtcNow:yyyyMMddTHHmmss}Z_rel-{safeVersion}_schema-{fromSchema}-to-{toSchema}.bak";
    }
}
