using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class EnrollmentGuardianSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<EnrollmentGuardianSchemaInitializer> _logger;

    public EnrollmentGuardianSchemaInitializer(string connectionString, ILogger<EnrollmentGuardianSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureColumnAsync(
            connection,
            "Guardians",
            "Gender",
            "ALTER TABLE [Guardians] ADD [Gender] int NULL;",
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            "StudentGuardians",
            "UsesStudentAddress",
            "ALTER TABLE [StudentGuardians] ADD [UsesStudentAddress] bit NOT NULL CONSTRAINT [DF_StudentGuardians_UsesStudentAddress] DEFAULT 0;",
            cancellationToken);

        _logger.LogInformation("Schéma responsables / adresses vérifié (Guardians.Gender, StudentGuardians.UsesStudentAddress).");
    }

    private static async Task EnsureColumnAsync(
        SqlConnection connection,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @table AND COLUMN_NAME = @column
            """;
        check.Parameters.AddWithValue("@table", tableName);
        check.Parameters.AddWithValue("@column", columnName);

        var exists = await check.ExecuteScalarAsync(cancellationToken) is not null;
        if (exists)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = alterSql;
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }
}
