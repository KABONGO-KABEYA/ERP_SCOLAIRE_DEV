using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

internal sealed class UnconfiguredSchoolBackup : ISchoolDatabaseBackup, ISchoolDatabaseRestore
{
    public Task<SchoolDatabaseBackupResult> CreateVerifiedBackupAsync(CancellationToken cancellationToken = default) =>
        throw Unconfigured();

    public Task<SchoolDatabaseBackupResult> CreateVerifiedBackupAsync(
        string releaseVersion,
        int fromSchema,
        int toSchema,
        CancellationToken cancellationToken = default) =>
        throw Unconfigured();

    public Task RestoreQuiescedBackupAsync(
        SchoolDatabaseRestoreRequest request,
        CancellationToken cancellationToken = default) =>
        throw Unconfigured();

    private static MigrationException Unconfigured() =>
        new("SQL Update Agent non configuré (lot permissions SQL non activé).");
}

internal sealed class UnconfiguredMigrationEngine : IMigrationEngine
{
    public Task<int> GetCurrentSchemaVersionAsync(CancellationToken cancellationToken = default) =>
        throw new MigrationException("SQL Update Agent non configuré (lot permissions SQL non activé).");

    public Task<MigrationApplyResult> ApplyLocalPackageAsync(
        string packageDirectory,
        CancellationToken cancellationToken = default) =>
        throw new MigrationException("SQL Update Agent non configuré (lot permissions SQL non activé).");
}
