using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Table SchoolSubscriptions — idempotente au démarrage API (installations existantes).</summary>
public sealed class SchoolSubscriptionSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<SchoolSubscriptionSchemaInitializer> _logger;

    public SchoolSubscriptionSchemaInitializer(
        string connectionString,
        ILogger<SchoolSubscriptionSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SchoolSubscriptions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchoolSubscriptions
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SchoolSubscriptions PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    InstallationDate DATETIME2 NOT NULL,
                    DurationMonths INT NOT NULL,
                    ExpirationDate DATETIME2 NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_SchoolSubscriptions_IsActive DEFAULT(1),
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SchoolSubscriptions_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_SchoolSubscriptions_Schools
                        FOREIGN KEY (SchoolId) REFERENCES dbo.Schools(Id)
                );

                CREATE UNIQUE INDEX UX_SchoolSubscriptions_SchoolId
                    ON dbo.SchoolSubscriptions(SchoolId)
                    WHERE IsDeleted = 0;

                CREATE INDEX IX_SchoolSubscriptions_IsDeleted
                    ON dbo.SchoolSubscriptions(IsDeleted);
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma SchoolSubscriptions vérifié.");
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
