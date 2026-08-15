using System.Data;
using Microsoft.Data.SqlClient;

namespace SchoolManagement.Updates;

public sealed class SqlCommandBackupExecutor : ISqlBackupExecutor
{
    private readonly string _connectionString;

    public SqlCommandBackupExecutor(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Chaîne de connexion SQL requise.", nameof(connectionString));
        }

        _connectionString = connectionString;
        var builder = new SqlConnectionStringBuilder(connectionString);
        DatabaseName = builder.InitialCatalog;
        SchoolBackupPathGuard.EnsureDatabaseName(DatabaseName);
    }

    public string DatabaseName { get; }

    public Task BackupCopyOnlyAsync(string absoluteBakPath, CancellationToken cancellationToken) =>
        ExecuteAsync(SqlBackupCommands.BackupCopyOnly(DatabaseName, absoluteBakPath), TimeSpan.FromMinutes(15), cancellationToken);

    public Task VerifyOnlyAsync(string absoluteBakPath, CancellationToken cancellationToken)
    {
        if (absoluteBakPath.StartsWith(@"\\", StringComparison.Ordinal)
            || absoluteBakPath.StartsWith("//", StringComparison.Ordinal))
        {
            throw new MigrationException("Chemin de backup UNC refusé.");
        }

        return ExecuteSignedVerifyAsync(absoluteBakPath, cancellationToken);
    }

    public Task RestoreReplaceAsync(string databaseName, string absoluteBakPath, CancellationToken cancellationToken)
    {
        if (!string.Equals(databaseName, DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("Base cible ≠ base ERP de la chaîne de connexion.");
        }

        SchoolBackupPathGuard.EnsureDatabaseName(databaseName);
        if (absoluteBakPath.StartsWith(@"\\", StringComparison.Ordinal)
            || absoluteBakPath.StartsWith("//", StringComparison.Ordinal))
        {
            throw new MigrationException("Chemin de backup UNC refusé.");
        }

        return ExecuteSignedRestoreAsync(databaseName, absoluteBakPath, cancellationToken);
    }

    private async Task ExecuteSignedVerifyAsync(string absoluteBakPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(SqlRestoreConnection.ToMaster(_connectionString));
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(SqlBackupCommands.SignedVerifyProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 300,
        };
        cmd.Parameters.Add("@DatabaseName", SqlDbType.NVarChar, 128).Value = DatabaseName;
        cmd.Parameters.Add("@BackupPath", SqlDbType.NVarChar, 512).Value = absoluteBakPath;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ExecuteSignedRestoreAsync(
        string databaseName,
        string absoluteBakPath,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(SqlRestoreConnection.ToMaster(_connectionString));
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(SqlBackupCommands.SignedRestoreProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 1800,
        };
        cmd.Parameters.Add("@DatabaseName", SqlDbType.NVarChar, 128).Value = databaseName;
        cmd.Parameters.Add("@BackupPath", SqlDbType.NVarChar, 512).Value = absoluteBakPath;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ExecuteAsync(
        string sql,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var batch in MigrationManager.SplitBatches(sql))
        {
            await using var cmd = new SqlCommand(batch, connection)
            {
                CommandTimeout = (int)Math.Clamp(timeout.TotalSeconds, 30, 3600),
            };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Restore : toujours <c>InitialCatalog=master</c>. Le nom de base cible vient de la
/// chaîne locale (ExpectedDatabaseName / InitialCatalog école), jamais de Bootstrap.
/// </summary>
public static class SqlRestoreConnection
{
    public static string ToMaster(string schoolConnectionString)
    {
        if (string.IsNullOrWhiteSpace(schoolConnectionString))
        {
            throw new MigrationException("Chaîne de connexion école requise pour restore.");
        }

        var builder = new SqlConnectionStringBuilder(schoolConnectionString);
        SchoolBackupPathGuard.EnsureDatabaseName(builder.InitialCatalog);
        builder.InitialCatalog = "master";
        return builder.ConnectionString;
    }
}

public sealed class SqlSchoolDatabaseBackup : ISchoolDatabaseBackup, ISchoolDatabaseRestore
{
    private readonly ISqlBackupExecutor _executor;
    private readonly IDiskSpaceChecker _disk;
    private readonly string _backupsRoot;
    private readonly string _expectedDatabaseName;
    private readonly long _minFreeBytes;
    private readonly long _minBackupBytes;
    private readonly Func<string, string, int, int, string> _fileNameFactory;

    public SqlSchoolDatabaseBackup(
        ISqlBackupExecutor executor,
        IDiskSpaceChecker disk,
        string backupsRoot,
        string expectedDatabaseName,
        long minFreeBytes = 500_000_000,
        long minBackupBytes = 1,
        Func<string, string, int, int, string>? fileNameFactory = null)
    {
        _executor = executor;
        _disk = disk;
        _backupsRoot = Path.GetFullPath(backupsRoot);
        _expectedDatabaseName = expectedDatabaseName.Trim();
        _minFreeBytes = minFreeBytes;
        _minBackupBytes = Math.Max(1, minBackupBytes);
        _fileNameFactory = fileNameFactory ?? SchoolBackupPathGuard.BuildFileName;
        SchoolBackupPathGuard.EnsureDatabaseName(_expectedDatabaseName);
        if (!string.Equals(_executor.DatabaseName, _expectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("Base SQL de connexion ≠ base ERP attendue.");
        }
    }

    public async Task<SchoolDatabaseBackupResult> CreateVerifiedBackupAsync(
        string releaseVersion,
        int fromSchema,
        int toSchema,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_backupsRoot);
        var available = _disk.GetAvailableBytes(_backupsRoot);
        if (available < _minFreeBytes)
        {
            throw new MigrationException($"Disque insuffisant ({available} < {_minFreeBytes} octets).");
        }

        var name = _fileNameFactory(_expectedDatabaseName, releaseVersion, fromSchema, toSchema);
        var path = Path.GetFullPath(Path.Combine(_backupsRoot, name));
        SchoolBackupPathGuard.EnsureAllowed(path, _backupsRoot, path);

        try
        {
            await _executor.BackupCopyOnlyAsync(path, cancellationToken);
            if (!File.Exists(path))
            {
                throw new MigrationException("Backup absent après BACKUP DATABASE.");
            }

            var size = new FileInfo(path).Length;
            if (size < _minBackupBytes)
            {
                throw new MigrationException($"Taille de backup invalide ({size}).");
            }

            await _executor.VerifyOnlyAsync(path, cancellationToken);
            return new SchoolDatabaseBackupResult(path, DateTime.UtcNow, size, IntegrityVerified: true);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    Task<SchoolDatabaseBackupResult> ISchoolDatabaseBackup.CreateVerifiedBackupAsync(CancellationToken cancellationToken) =>
        CreateVerifiedBackupAsync("0.0.0", 1, 1, cancellationToken);

    public async Task RestoreQuiescedBackupAsync(
        SchoolDatabaseRestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ExpectedDatabaseName, _expectedDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("Restore : base cible ≠ base ERP attendue.");
        }

        var path = SchoolBackupPathGuard.EnsureAllowed(
            request.CandidatePath,
            request.BackupsRoot,
            request.ExpectedPathFromState);
        if (!File.Exists(path))
        {
            throw new MigrationException("Backup à restaurer introuvable.");
        }

        await _executor.RestoreReplaceAsync(_expectedDatabaseName, path, cancellationToken);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort
        }
    }
}

public sealed class DriveDiskSpaceChecker : IDiskSpaceChecker
{
    public long GetAvailableBytes(string pathOnVolume)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(pathOnVolume));
        if (string.IsNullOrEmpty(root))
        {
            throw new MigrationException("Volume disque introuvable.");
        }

        var drive = new DriveInfo(root);
        return drive.AvailableFreeSpace;
    }
}

public sealed class SqlMigrationEngine : IMigrationEngine
{
    private readonly MigrationManager _manager;

    public SqlMigrationEngine(string connectionString, Action<string>? log = null)
    {
        _manager = new MigrationManager(connectionString, log);
    }

    public Task<int> GetCurrentSchemaVersionAsync(CancellationToken cancellationToken = default) =>
        _manager.GetSchemaVersionAsync(cancellationToken);

    public Task<MigrationApplyResult> ApplyLocalPackageAsync(
        string packageDirectory,
        CancellationToken cancellationToken = default) =>
        _manager.ApplyPackageAsync(packageDirectory, cancellationToken);
}
