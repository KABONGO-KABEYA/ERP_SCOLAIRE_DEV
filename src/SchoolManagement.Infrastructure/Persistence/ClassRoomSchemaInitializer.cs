using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Schools;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class ClassRoomSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<ClassRoomSchemaInitializer> _logger;

    public ClassRoomSchemaInitializer(string connectionString, ILogger<ClassRoomSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'ClassRooms')
                  AND name = N'Code'
                  AND max_length < {ClassLocalCodeBuilder.MaxCodeLength * 2})
            BEGIN
                ALTER TABLE [ClassRooms] ALTER COLUMN [Code] nvarchar({ClassLocalCodeBuilder.MaxCodeLength}) NOT NULL;
            END
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation(
            "Schéma ClassRooms vérifié (Code nvarchar({MaxLength})).",
            ClassLocalCodeBuilder.MaxCodeLength);
    }
}
