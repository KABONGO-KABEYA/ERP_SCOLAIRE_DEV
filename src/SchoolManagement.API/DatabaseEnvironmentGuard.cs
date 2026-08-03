using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Empêche de pointer Development vers la BD Production (et inversement).
/// </summary>
public static class DatabaseEnvironmentGuard
{
    public const string DevelopmentDatabaseName = "SchoolManagementRDC_Development";
    public const string ProductionDatabaseName = "SchoolManagementRDC_Production";

    public static void EnsureSafe(IHostEnvironment environment, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var databaseName = ExtractDatabaseName(connectionString);
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "La chaîne SQL ne contient pas de nom de base (Database / Initial Catalog).");
        }

        var db = databaseName.Trim();
        var isDevEnv = environment.IsDevelopment();
        var isProdEnv = environment.IsProduction();

        if (isDevEnv &&
            db.Equals(ProductionDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Environnement Development interdit sur la base Production '{ProductionDatabaseName}'. " +
                $"Utilisez '{DevelopmentDatabaseName}'.");
        }

        if (isProdEnv &&
            db.Equals(DevelopmentDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Environnement Production interdit sur la base Development '{DevelopmentDatabaseName}'. " +
                $"Utilisez '{ProductionDatabaseName}'.");
        }

        // Legacy unique DB name — interdit en Development (évite d'écrire dans la base partagée).
        if (isDevEnv &&
            db.Equals("SchoolManagementRDC", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Environnement Development ne doit plus utiliser 'SchoolManagementRDC' (base partagée). " +
                $"Pointez vers '{DevelopmentDatabaseName}'.");
        }
    }

    public static bool IsProductionDatabase(string connectionString)
    {
        var db = ExtractDatabaseName(connectionString);
        return !string.IsNullOrWhiteSpace(db)
            && db.Equals(ProductionDatabaseName, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ExtractDatabaseName(string connectionString)
    {
        var match = Regex.Match(
            connectionString,
            @"(?:Initial Catalog|Database)\s*=\s*([^;]+)",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
