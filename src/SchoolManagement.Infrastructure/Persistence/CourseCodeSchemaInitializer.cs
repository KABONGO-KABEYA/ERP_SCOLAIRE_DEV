using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Schools;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class CourseCodeSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<CourseCodeSchemaInitializer> _logger;

    public CourseCodeSchemaInitializer(string connectionString, ILogger<CourseCodeSchemaInitializer> logger)
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
                WHERE object_id = OBJECT_ID(N'Courses')
                  AND name = N'Code'
                  AND max_length < {CourseCodeConstraints.MaxCodeLength * 2})
            BEGIN
                ALTER TABLE [Courses] ALTER COLUMN [Code] nvarchar({CourseCodeConstraints.MaxCodeLength}) NOT NULL;
            END

            IF OBJECT_ID(N'Branches') IS NOT NULL
               AND EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Branches')
                  AND name = N'Code'
                  AND max_length < {CourseCodeConstraints.MaxCodeLength * 2})
            BEGIN
                ALTER TABLE [Branches] ALTER COLUMN [Code] nvarchar({CourseCodeConstraints.MaxCodeLength}) NOT NULL;
            END
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation(
            "Schéma curriculum vérifié (Courses.Code et Branches.Code nvarchar({MaxLength})).",
            CourseCodeConstraints.MaxCodeLength);
    }
}
