using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class RegistrationNumberCounterSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<RegistrationNumberCounterSchemaInitializer> _logger;

    public RegistrationNumberCounterSchemaInitializer(
        string connectionString,
        ILogger<RegistrationNumberCounterSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.RegistrationNumberCounters', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RegistrationNumberCounters
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RegistrationNumberCounters PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    [Year] INT NOT NULL,
                    NextValue INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_RegistrationNumberCounters_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_RegistrationNumberCounters_Schools_SchoolId
                        FOREIGN KEY (SchoolId) REFERENCES dbo.Schools(Id)
                );
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_RegistrationNumberCounters_IsDeleted'
                  AND object_id = OBJECT_ID(N'dbo.RegistrationNumberCounters'))
            BEGIN
                CREATE INDEX IX_RegistrationNumberCounters_IsDeleted
                    ON dbo.RegistrationNumberCounters(IsDeleted);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_RegistrationNumberCounters_SchoolId_Year'
                  AND object_id = OBJECT_ID(N'dbo.RegistrationNumberCounters'))
            BEGIN
                CREATE UNIQUE INDEX IX_RegistrationNumberCounters_SchoolId_Year
                    ON dbo.RegistrationNumberCounters(SchoolId, [Year]);
            END;

            INSERT INTO dbo.RegistrationNumberCounters
                (Id, SchoolId, [Year], NextValue, CreatedAt, IsDeleted)
            SELECT
                NEWID(),
                parsed.SchoolId,
                parsed.Yr,
                MAX(parsed.Seq) + 1,
                SYSUTCDATETIME(),
                0
            FROM (
                SELECT
                    s.SchoolId,
                    TRY_CAST(SUBSTRING(s.RegistrationNumber, 5, 4) AS int) AS Yr,
                    TRY_CAST(SUBSTRING(s.RegistrationNumber, 10, LEN(s.RegistrationNumber) - 9) AS int) AS Seq
                FROM dbo.Students s
                WHERE s.RegistrationNumber LIKE 'ELV-[0-9][0-9][0-9][0-9]-%'
            ) parsed
            WHERE parsed.Yr IS NOT NULL
              AND parsed.Seq IS NOT NULL
              AND parsed.Seq > 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.RegistrationNumberCounters existing
                  WHERE existing.SchoolId = parsed.SchoolId
                    AND existing.[Year] = parsed.Yr)
            GROUP BY parsed.SchoolId, parsed.Yr;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Schéma RegistrationNumberCounters vérifié.");
    }
}
