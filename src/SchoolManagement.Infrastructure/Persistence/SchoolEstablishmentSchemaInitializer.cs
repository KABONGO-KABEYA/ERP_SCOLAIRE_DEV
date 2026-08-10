using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Table credentials QR établissement (Phase 4) — idempotent au démarrage.</summary>
public sealed class SchoolEstablishmentSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<SchoolEstablishmentSchemaInitializer> _logger;

    public SchoolEstablishmentSchemaInitializer(
        string connectionString,
        ILogger<SchoolEstablishmentSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SchoolEstablishmentCredentials', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchoolEstablishmentCredentials
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SchoolEstablishmentCredentials PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    CredentialVersion INT NOT NULL,
                    TokenType NVARCHAR(64) NOT NULL,
                    SecretHash NVARCHAR(128) NOT NULL,
                    Status NVARCHAR(32) NOT NULL,
                    RevokedAtUtc DATETIME2 NULL,
                    RevokedReason NVARCHAR(500) NULL,
                    CreatedByUserId UNIQUEIDENTIFIER NULL,
                    BootstrapSyncPending BIT NOT NULL CONSTRAINT DF_SchoolEstablishmentCredentials_SyncPending DEFAULT(1),
                    BootstrapSyncStatus NVARCHAR(32) NOT NULL,
                    LastBootstrapSyncError NVARCHAR(1000) NULL,
                    LastBootstrapSyncAttemptUtc DATETIME2 NULL,
                    BootstrapSyncedAtUtc DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SchoolEstablishmentCredentials_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL
                );
                CREATE UNIQUE INDEX UX_SchoolEstablishmentCredential_SchoolId_Version
                    ON dbo.SchoolEstablishmentCredentials(SchoolId, CredentialVersion);
                CREATE UNIQUE INDEX UX_SchoolEstablishmentCredential_Active
                    ON dbo.SchoolEstablishmentCredentials(SchoolId)
                    WHERE Status = N'Active';
                CREATE INDEX IX_SchoolEstablishmentCredentials_SchoolId
                    ON dbo.SchoolEstablishmentCredentials(SchoolId);
                CREATE INDEX IX_SchoolEstablishmentCredentials_BootstrapSyncPending
                    ON dbo.SchoolEstablishmentCredentials(BootstrapSyncPending);
                CREATE INDEX IX_SchoolEstablishmentCredentials_IsDeleted
                    ON dbo.SchoolEstablishmentCredentials(IsDeleted);
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma SchoolEstablishmentCredentials vérifié.");
    }

    private static async Task ExecAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
