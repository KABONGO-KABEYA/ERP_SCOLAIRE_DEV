using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Ajoute FeeInstallmentId et PhysicalReceiptNumber sur PaymentLines si absents.</summary>
public sealed class PaymentLineSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<PaymentLineSchemaInitializer> _logger;

    public PaymentLineSchemaInitializer(string connectionString, ILogger<PaymentLineSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var script in Scripts)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Schéma PaymentLines vérifié (FeeInstallmentId, PhysicalReceiptNumber).");
    }

    private static readonly string[] Scripts =
    [
        """
        IF OBJECT_ID(N'PaymentLines', N'U') IS NOT NULL
           AND COL_LENGTH(N'PaymentLines', N'FeeInstallmentId') IS NULL
            ALTER TABLE [PaymentLines] ADD [FeeInstallmentId] uniqueidentifier NULL;
        """,
        """
        IF OBJECT_ID(N'PaymentLines', N'U') IS NOT NULL
           AND COL_LENGTH(N'PaymentLines', N'FeeInstallmentId') IS NOT NULL
           AND OBJECT_ID(N'FeeInstallments', N'U') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PaymentLines_FeeInstallments')
            ALTER TABLE [PaymentLines] WITH CHECK
            ADD CONSTRAINT [FK_PaymentLines_FeeInstallments]
                FOREIGN KEY ([FeeInstallmentId]) REFERENCES [FeeInstallments] ([Id]);
        """,
        """
        IF OBJECT_ID(N'PaymentLines', N'U') IS NOT NULL
           AND COL_LENGTH(N'PaymentLines', N'PhysicalReceiptNumber') IS NULL
            ALTER TABLE [PaymentLines] ADD [PhysicalReceiptNumber] nvarchar(50) NULL;
        """,
        """
        IF OBJECT_ID(N'PaymentLines', N'U') IS NOT NULL
           AND COL_LENGTH(N'PaymentLines', N'FeeInstallmentId') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM sys.indexes
               WHERE name = N'IX_PaymentLines_PaymentId_FeeInstallmentId'
                 AND object_id = OBJECT_ID(N'PaymentLines'))
            CREATE INDEX [IX_PaymentLines_PaymentId_FeeInstallmentId]
                ON [PaymentLines] ([PaymentId], [FeeInstallmentId]);
        """
    ];
}
