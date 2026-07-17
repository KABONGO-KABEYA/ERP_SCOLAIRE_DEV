using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Rend CashRegisterId / PaymentMethod nullables. La table CashRegisters est conservée
/// (historique) mais considérée comme dépréciée / non utilisée par le produit.
/// </summary>
public sealed class PaymentCashRegisterSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<PaymentCashRegisterSchemaInitializer> _logger;

    public PaymentCashRegisterSchemaInitializer(
        string connectionString,
        ILogger<PaymentCashRegisterSchemaInitializer> logger)
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
            "Schéma paiements/caisse vérifié (CashRegisterId et PaymentMethod nullables ; CashRegisters dépréciée).");
    }

    private static readonly string[] Scripts =
    [
        // Payments.CashRegisterId → NULL + FK nullable
        """
        IF OBJECT_ID(N'Payments', N'U') IS NOT NULL
           AND COL_LENGTH(N'Payments', N'CashRegisterId') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Payments_CashRegisters_CashRegisterId')
                ALTER TABLE [Payments] DROP CONSTRAINT [FK_Payments_CashRegisters_CashRegisterId];

            ALTER TABLE [Payments] ALTER COLUMN [CashRegisterId] uniqueidentifier NULL;

            IF OBJECT_ID(N'CashRegisters', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Payments_CashRegisters_CashRegisterId')
                ALTER TABLE [Payments] WITH CHECK
                ADD CONSTRAINT [FK_Payments_CashRegisters_CashRegisterId]
                    FOREIGN KEY ([CashRegisterId]) REFERENCES [CashRegisters] ([Id]);
        END
        """,
        // Payments.PaymentMethod → NULL
        """
        IF OBJECT_ID(N'Payments', N'U') IS NOT NULL
           AND COL_LENGTH(N'Payments', N'PaymentMethod') IS NOT NULL
            ALTER TABLE [Payments] ALTER COLUMN [PaymentMethod] nvarchar(max) NULL;
        """,
        // CashMovements.CashRegisterId → NULL + FK nullable
        """
        IF OBJECT_ID(N'CashMovements', N'U') IS NOT NULL
           AND COL_LENGTH(N'CashMovements', N'CashRegisterId') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CashMovements_CashRegisters_CashRegisterId')
                ALTER TABLE [CashMovements] DROP CONSTRAINT [FK_CashMovements_CashRegisters_CashRegisterId];

            ALTER TABLE [CashMovements] ALTER COLUMN [CashRegisterId] uniqueidentifier NULL;

            IF OBJECT_ID(N'CashRegisters', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CashMovements_CashRegisters_CashRegisterId')
                ALTER TABLE [CashMovements] WITH CHECK
                ADD CONSTRAINT [FK_CashMovements_CashRegisters_CashRegisterId]
                    FOREIGN KEY ([CashRegisterId]) REFERENCES [CashRegisters] ([Id]);
        END
        """
    ];
}
