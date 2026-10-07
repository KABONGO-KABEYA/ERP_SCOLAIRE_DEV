using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.CloudSync;

internal static class CloudSyncDestinationSchema
{
    /// <summary>Crée le schéma courant seulement si la destination ne contient aucune table.</summary>
    internal static async Task<bool> EnsureEmptyCloudSchemaAsync(
        SchoolDbContext remote, CancellationToken cancellationToken = default)
    {
        var connection = remote.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 120;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'ERP_CloudSchemaInitialization',
                @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=60000;
            IF @result < 0 THROW 51002, 'Initialisation du schéma cloud déjà en cours.', 1;
            SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0;
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        // La sync utilise des suppressions logiques. Aucune cascade physique sur la copie cloud :
        // SQL Server refuse les chemins multiples présents dans certaines conventions du modèle EF.
        var script = remote.Database.GenerateCreateScript()
            .Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.Ordinal)
            .Replace("ON DELETE SET NULL", "ON DELETE NO ACTION", StringComparison.Ordinal);
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
