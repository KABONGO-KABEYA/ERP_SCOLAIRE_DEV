using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Tables activation parent (architecture connexion v2 — idempotent au démarrage).</summary>
public sealed class ParentActivationSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<ParentActivationSchemaInitializer> _logger;

    public ParentActivationSchemaInitializer(
        string connectionString,
        ILogger<ParentActivationSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.ParentActivationTokens', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ParentActivationTokens
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ParentActivationTokens PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    ExpiresAtUtc DATETIME2 NOT NULL,
                    ConsumedAtUtc DATETIME2 NULL,
                    SuggestedUserName NVARCHAR(256) NULL,
                    IssuedByUserId UNIQUEIDENTIFIER NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_ParentActivationTokens_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL
                );
                CREATE INDEX IX_ParentActivationTokens_SchoolId ON dbo.ParentActivationTokens(SchoolId);
                CREATE INDEX IX_ParentActivationTokens_IsDeleted ON dbo.ParentActivationTokens(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.ParentActivationSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ParentActivationSessions
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ParentActivationSessions PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    ActivationTokenId UNIQUEIDENTIFIER NOT NULL,
                    DeviceId NVARCHAR(64) NOT NULL,
                    BootstrapSessionId UNIQUEIDENTIFIER NULL,
                    Status INT NOT NULL,
                    ExpiresAtUtc DATETIME2 NOT NULL,
                    CompletedAtUtc DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_ParentActivationSessions_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL
                );
                CREATE INDEX IX_ParentActivationSessions_ActivationTokenId ON dbo.ParentActivationSessions(ActivationTokenId);
                CREATE INDEX IX_ParentActivationSessions_SchoolId ON dbo.ParentActivationSessions(SchoolId);
                CREATE INDEX IX_ParentActivationSessions_IsDeleted ON dbo.ParentActivationSessions(IsDeleted);
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma activation parent vérifié.");
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
