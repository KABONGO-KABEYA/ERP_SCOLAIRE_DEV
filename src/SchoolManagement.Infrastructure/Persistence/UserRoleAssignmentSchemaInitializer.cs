using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class UserRoleAssignmentSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<UserRoleAssignmentSchemaInitializer> _logger;

    public UserRoleAssignmentSchemaInitializer(
        string connectionString,
        ILogger<UserRoleAssignmentSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_UserRoleAssignments_UserId_RoleId'
                  AND object_id = OBJECT_ID(N'dbo.UserRoleAssignments')
                  AND (
                      is_unique = 0
                      OR has_filter = 0
                      OR filter_definition NOT LIKE '%IsDeleted%'
                  ))
            BEGIN
                DROP INDEX IX_UserRoleAssignments_UserId_RoleId ON dbo.UserRoleAssignments;
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = N'IX_UserRoleAssignments_UserId_RoleId'
                  AND object_id = OBJECT_ID(N'dbo.UserRoleAssignments')
                  AND is_unique = 1
                  AND has_filter = 1
                  AND filter_definition LIKE '%IsDeleted%')
            BEGIN
                CREATE UNIQUE INDEX IX_UserRoleAssignments_UserId_RoleId
                    ON dbo.UserRoleAssignments(UserId, RoleId)
                    WHERE [IsDeleted] = 0;
            END;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Index UserRoleAssignments vérifié.");
    }
}
