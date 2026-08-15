namespace SchoolManagement.Updates;

/// <summary>
/// Backup vérifié (COPY_ONLY + CHECKSUM + VERIFYONLY). Restore : <see cref="ISchoolDatabaseRestore"/>.
/// </summary>
public interface ISchoolDatabaseBackup
{
    Task<SchoolDatabaseBackupResult> CreateVerifiedBackupAsync(CancellationToken cancellationToken = default);

    Task<SchoolDatabaseBackupResult> CreateVerifiedBackupAsync(
        string releaseVersion,
        int fromSchema,
        int toSchema,
        CancellationToken cancellationToken = default);
}

public sealed record SchoolDatabaseBackupResult(
    string BackupFilePath,
    DateTime TakenAtUtc,
    long ByteSize,
    bool IntegrityVerified);

/// <summary>
/// BACKUP / VERIFYONLY / RESTORE isolés (Lot 2B-4B).
/// Les GRANT SQL de production ne sont pas appliqués ici.
/// </summary>
public interface ISchoolDatabaseRestore
{
    Task RestoreQuiescedBackupAsync(SchoolDatabaseRestoreRequest request, CancellationToken cancellationToken = default);
}

public sealed record SchoolDatabaseRestoreRequest(
    string CandidatePath,
    string ExpectedPathFromState,
    string BackupsRoot,
    string ExpectedDatabaseName);

public interface ISqlBackupExecutor
{
    string DatabaseName { get; }

    Task BackupCopyOnlyAsync(string absoluteBakPath, CancellationToken cancellationToken);

    Task VerifyOnlyAsync(string absoluteBakPath, CancellationToken cancellationToken);

    Task RestoreReplaceAsync(string databaseName, string absoluteBakPath, CancellationToken cancellationToken);
}

public interface IMigrationEngine
{
    Task<int> GetCurrentSchemaVersionAsync(CancellationToken cancellationToken = default);

    Task<MigrationApplyResult> ApplyLocalPackageAsync(
        string packageDirectory,
        CancellationToken cancellationToken = default);
}

public interface IDiskSpaceChecker
{
    long GetAvailableBytes(string pathOnVolume);
}

public static class SqlBackupCommands
{
    public static string BackupCopyOnly(string databaseName, string diskPath)
    {
        var db = QuoteIdentifier(databaseName);
        var path = QuoteString(diskPath);
        return $"BACKUP DATABASE {db} TO DISK = {path} WITH COPY_ONLY, CHECKSUM, INIT, STATS = 10;";
    }

    public static string VerifyOnly(string diskPath)
    {
        var path = QuoteString(diskPath);
        return $"RESTORE VERIFYONLY FROM DISK = {path} WITH CHECKSUM;";
    }

    /// <summary>
    /// Restore labo/prod : procédure signée dans master, pas un RESTORE brut côté agent.
    /// </summary>
    public const string SignedRestoreProcedure = "dbo.ErpScolaire_RestoreSchoolDatabase";

    /// <summary>
    /// VERIFYONLY exige aussi un privilège restore — procédure signée (pas CREATE ANY DATABASE pour l'agent).
    /// </summary>
    public const string SignedVerifyProcedure = "dbo.ErpScolaire_VerifySchoolBackup";

    public static string QuoteIdentifier(string name)
    {
        SchoolBackupPathGuard.EnsureDatabaseName(name);
        return "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    public static string QuoteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new MigrationException("Chemin SQL vide.");
        }

        return "N'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
