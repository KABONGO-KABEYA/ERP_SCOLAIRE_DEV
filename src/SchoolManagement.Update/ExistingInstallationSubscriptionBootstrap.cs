using Microsoft.Data.SqlClient;
using SchoolManagement.Application.Configuration.Database;

namespace SchoolManagement.Update;

internal sealed record SubscriptionBootstrapResult(
    bool TableExists,
    bool SubscriptionAlreadyExisted,
    bool SubscriptionCreated,
    string Detail);

/// <summary>
/// Active l'abonnement initial pour les installations existantes sans ligne SchoolSubscriptions.
/// Ne modifie jamais un abonnement déjà présent.
/// </summary>
internal static class ExistingInstallationSubscriptionBootstrap
{
    internal static readonly DateTime DefaultInstallationDate = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    internal const int DefaultDurationMonths = 1;

    internal static async Task<SubscriptionBootstrapResult> EnsureAsync(
        string apiDirectory,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        var bootstrap = new DatabaseConnectionBootstrap(apiDirectory);
        var (_, connectionString, testResult) = await bootstrap.LoadValidateAndTestAsync(cancellationToken);
        if (!testResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Connexion SQL impossible pour l'activation abonnement : {testResult.Message}");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
        {
            return new SubscriptionBootstrapResult(
                TableExists: false,
                SubscriptionAlreadyExisted: false,
                SubscriptionCreated: false,
                Detail: "Table SchoolSubscriptions absente — attendre le redémarrage API / SchemaInitializer.");
        }

        var schools = await LoadActiveSchoolsAsync(connection, cancellationToken);
        if (schools.Count == 0)
        {
            return new SubscriptionBootstrapResult(
                TableExists: true,
                SubscriptionAlreadyExisted: false,
                SubscriptionCreated: false,
                Detail: "Aucune école active en base — activation abonnement ignorée.");
        }

        var existingCount = await CountActiveSubscriptionsAsync(connection, cancellationToken);
        if (existingCount > 0)
        {
            log("[ABONNEMENT] Abonnement existant détecté — aucune modification.");
            return new SubscriptionBootstrapResult(
                TableExists: true,
                SubscriptionAlreadyExisted: true,
                SubscriptionCreated: false,
                Detail: "Abonnement existant détecté. Aucune modification.");
        }

        if (schools.Count > 1)
        {
            log($"[ABONNEMENT] {schools.Count} écoles actives — activation sur la première uniquement.");
        }

        var school = schools[0];
        var expiration = DefaultInstallationDate.AddMonths(DefaultDurationMonths);
        await InsertSubscriptionAsync(connection, school.Id, cancellationToken);

        var detail =
            $"{DefaultInstallationDate:dd/MM/yyyy} → {expiration:dd/MM/yyyy} ({school.Name})";
        log($"[ABONNEMENT] Abonnement initial créé : {detail}");
        return new SubscriptionBootstrapResult(
            TableExists: true,
            SubscriptionAlreadyExisted: false,
            SubscriptionCreated: true,
            Detail: detail);
    }

    internal static async Task<int> CountActiveSchoolsAsync(
        string apiDirectory,
        CancellationToken cancellationToken = default)
    {
        var bootstrap = new DatabaseConnectionBootstrap(apiDirectory);
        var (_, connectionString, testResult) = await bootstrap.LoadValidateAndTestAsync(cancellationToken);
        if (!testResult.IsSuccess)
            return -1;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await CountActiveSchoolsOnConnectionAsync(connection, cancellationToken);
    }

    internal static async Task<bool> VerifySchemaAsync(
        string apiDirectory,
        CancellationToken cancellationToken = default)
    {
        var bootstrap = new DatabaseConnectionBootstrap(apiDirectory);
        var (_, connectionString, testResult) = await bootstrap.LoadValidateAndTestAsync(cancellationToken);
        if (!testResult.IsSuccess)
            return false;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!await TableExistsAsync(connection, cancellationToken)) return false;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN OBJECT_ID(N'dbo.SyncEntityIdentity', N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.SyncDestination', N'U') IS NOT NULL
                AND (SELECT COUNT(*) FROM sys.columns
                    WHERE object_id=OBJECT_ID(N'dbo.EnrollmentPricingCategoryHistory')
                      AND name IN (N'CreatedBy', N'UpdatedBy', N'DeletedBy')
                      AND system_type_id=TYPE_ID(N'uniqueidentifier')) = 3
                THEN 1 ELSE 0 END;
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.SchoolSubscriptions', N'U') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<int> CountActiveSchoolsOnConnectionAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.Schools WHERE IsDeleted = 0";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> CountActiveSubscriptionsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.SchoolSubscriptions WHERE IsDeleted = 0";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<List<(Guid Id, string Name)>> LoadActiveSchoolsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var list = new List<(Guid Id, string Name)>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM dbo.Schools WHERE IsDeleted = 0 ORDER BY CreatedAt";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add((reader.GetGuid(0), reader.GetString(1)));
        }

        return list;
    }

    private static async Task InsertSubscriptionAsync(
        SqlConnection connection,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var expiration = DefaultInstallationDate.AddMonths(DefaultDurationMonths);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            IF NOT EXISTS (
                SELECT 1 FROM dbo.SchoolSubscriptions
                WHERE SchoolId = @SchoolId AND IsDeleted = 0)
            BEGIN
                INSERT INTO dbo.SchoolSubscriptions
                (
                    Id, SchoolId, InstallationDate, DurationMonths, ExpirationDate, IsActive,
                    CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, DeletedBy
                )
                VALUES
                (
                    @Id, @SchoolId, @InstallationDate, @DurationMonths, @ExpirationDate, 1,
                    @CreatedAt, NULL, NULL, NULL, 0, NULL, NULL
                );
            END
            """;
        cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@SchoolId", schoolId);
        cmd.Parameters.AddWithValue("@InstallationDate", DefaultInstallationDate);
        cmd.Parameters.AddWithValue("@DurationMonths", DefaultDurationMonths);
        cmd.Parameters.AddWithValue("@ExpirationDate", expiration);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
