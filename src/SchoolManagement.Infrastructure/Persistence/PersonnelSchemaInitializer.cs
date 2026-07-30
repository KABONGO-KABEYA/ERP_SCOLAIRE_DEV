using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class PersonnelSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<PersonnelSchemaInitializer> _logger;

    public PersonnelSchemaInitializer(string connectionString, ILogger<PersonnelSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HrDepartments')
            BEGIN
                CREATE TABLE [HrDepartments] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [Code] nvarchar(20) NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_HrDepartments_IsActive] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_HrDepartments_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL
                );
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HrJobFunctions')
            BEGIN
                CREATE TABLE [HrJobFunctions] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [DepartmentId] uniqueidentifier NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_HrJobFunctions_IsActive] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_HrJobFunctions_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL
                );
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PersonnelHrProfiles')
            BEGIN
                CREATE TABLE [PersonnelHrProfiles] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [TeacherId] uniqueidentifier NOT NULL,
                    [Category] int NOT NULL CONSTRAINT [DF_PersonnelHrProfiles_Category] DEFAULT 1,
                    [MiddleName] nvarchar(100) NULL,
                    [Gender] int NULL,
                    [BirthDate] date NULL,
                    [BirthPlace] nvarchar(120) NULL,
                    [Nationality] nvarchar(80) NULL,
                    [MaritalStatus] nvarchar(40) NULL,
                    [ChildrenCount] int NULL,
                    [IdCardNumber] nvarchar(60) NULL,
                    [DepartmentId] uniqueidentifier NULL,
                    [JobFunctionId] uniqueidentifier NULL,
                    [Grade] nvarchar(80) NULL,
                    [Service] nvarchar(120) NULL,
                    [SupervisorName] nvarchar(160) NULL,
                    [WorkLocation] nvarchar(120) NULL,
                    [ContractType] int NULL,
                    [ContractStartDate] date NULL,
                    [ContractEndDate] date NULL,
                    [BaseSalary] decimal(18,2) NULL,
                    [CurrencyCode] nvarchar(10) NULL,
                    [PaymentMethod] int NULL,
                    [BankName] nvarchar(120) NULL,
                    [BankAccountNumber] nvarchar(60) NULL,
                    [BankAccountHolder] nvarchar(160) NULL,
                    [PayDay] int NULL,
                    [EmergencyContactName] nvarchar(160) NULL,
                    [EmergencyContactRelation] nvarchar(60) NULL,
                    [EmergencyContactPhone] nvarchar(40) NULL,
                    [EmergencyContactAddress] nvarchar(300) NULL,
                    [PhotoPath] nvarchar(500) NULL,
                    [Status] int NOT NULL CONSTRAINT [DF_PersonnelHrProfiles_Status] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_PersonnelHrProfiles_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL
                );
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma Personnel RH vérifié (HrDepartments, HrJobFunctions, PersonnelHrProfiles).");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
