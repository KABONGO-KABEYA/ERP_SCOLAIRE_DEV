using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class AttendanceSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<AttendanceSchemaInitializer> _logger;

    public AttendanceSchemaInitializer(string connectionString, ILogger<AttendanceSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureColumnAsync(
            connection,
            "StudentAttendances",
            "SchoolId",
            """
            ALTER TABLE [StudentAttendances]
            ADD [SchoolId] uniqueidentifier NOT NULL
                CONSTRAINT [DF_StudentAttendances_SchoolId] DEFAULT '00000000-0000-0000-0000-000000000000';
            """,
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            "TeacherAttendances",
            "SchoolId",
            """
            ALTER TABLE [TeacherAttendances]
            ADD [SchoolId] uniqueidentifier NOT NULL
                CONSTRAINT [DF_TeacherAttendances_SchoolId] DEFAULT '00000000-0000-0000-0000-000000000000';
            """,
            cancellationToken);

        await using (var backfillStudent = connection.CreateCommand())
        {
            backfillStudent.CommandText = """
                UPDATE sa
                SET sa.[SchoolId] = s.[SchoolId]
                FROM [StudentAttendances] sa
                INNER JOIN [Students] s ON s.[Id] = sa.[StudentId]
                WHERE sa.[SchoolId] = '00000000-0000-0000-0000-000000000000';
                """;
            await backfillStudent.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var backfillTeacher = connection.CreateCommand())
        {
            backfillTeacher.CommandText = """
                UPDATE ta
                SET ta.[SchoolId] = t.[SchoolId]
                FROM [TeacherAttendances] ta
                INNER JOIN [Teachers] t ON t.[Id] = ta.[TeacherId]
                WHERE ta.[SchoolId] = '00000000-0000-0000-0000-000000000000';
                """;
            await backfillTeacher.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Schéma présences vérifié (StudentAttendances.SchoolId, TeacherAttendances.SchoolId).");
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
