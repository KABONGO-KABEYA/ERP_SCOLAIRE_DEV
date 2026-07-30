using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class ApplicationUpdateSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<ApplicationUpdateSchemaInitializer> _logger;

    public ApplicationUpdateSchemaInitializer(
        string connectionString,
        ILogger<ApplicationUpdateSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.ApplicationVersions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ApplicationVersions
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ApplicationVersions PRIMARY KEY,
                    Version NVARCHAR(32) NOT NULL,
                    MinimumVersion NVARCHAR(32) NOT NULL,
                    Mandatory BIT NOT NULL CONSTRAINT DF_ApplicationVersions_Mandatory DEFAULT(0),
                    ReleaseDate DATE NOT NULL,
                    ReleaseNotes NVARCHAR(MAX) NOT NULL,
                    DesktopUrl NVARCHAR(1000) NULL,
                    MobileUrl NVARCHAR(1000) NULL,
                    Sha256 NVARCHAR(128) NULL,
                    Size BIGINT NULL,
                    SchemaVersion INT NOT NULL CONSTRAINT DF_ApplicationVersions_SchemaVersion DEFAULT(0),
                    Active BIT NOT NULL CONSTRAINT DF_ApplicationVersions_Active DEFAULT(1),
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ApplicationVersions_CreatedAtUtc DEFAULT SYSUTCDATETIME()
                );
                CREATE INDEX IX_ApplicationVersions_Active ON dbo.ApplicationVersions(Active);
            END

            IF OBJECT_ID(N'dbo.AppSchemaVersion', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AppSchemaVersion
                (
                    Id INT NOT NULL CONSTRAINT PK_AppSchemaVersion PRIMARY KEY CHECK (Id = 1),
                    SchemaVersion INT NOT NULL,
                    UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_AppSchemaVersion_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
                );
                INSERT INTO dbo.AppSchemaVersion (Id, SchemaVersion) VALUES (1, 1);
            END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Schéma mises à jour vérifié (ApplicationVersions, AppSchemaVersion).");
    }
}
