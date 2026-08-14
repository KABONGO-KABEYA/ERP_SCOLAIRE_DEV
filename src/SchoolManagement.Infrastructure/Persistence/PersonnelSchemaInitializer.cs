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

        // Dédoublonnage + index unique (SchoolId, Code) — évite les listes en double après seeds concurrents.
        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'dbo.HrDepartments', N'U') IS NOT NULL
            BEGIN
                ;WITH ranked AS (
                    SELECT
                        Id,
                        SchoolId,
                        Code,
                        ROW_NUMBER() OVER (
                            PARTITION BY SchoolId, UPPER(LTRIM(RTRIM(Code)))
                            ORDER BY CreatedAt ASC, Id ASC
                        ) AS rn
                    FROM dbo.HrDepartments
                    WHERE IsDeleted = 0
                ),
                keepers AS (
                    SELECT Id, SchoolId, Code
                    FROM ranked
                    WHERE rn = 1
                ),
                dupes AS (
                    SELECT r.Id AS DuplicateId, k.Id AS KeeperId
                    FROM ranked r
                    INNER JOIN keepers k
                        ON k.SchoolId = r.SchoolId
                       AND UPPER(LTRIM(RTRIM(k.Code))) = UPPER(LTRIM(RTRIM(r.Code)))
                    WHERE r.rn > 1
                )
                UPDATE p
                SET p.DepartmentId = d.KeeperId,
                    p.UpdatedAt = SYSUTCDATETIME()
                FROM dbo.PersonnelHrProfiles p
                INNER JOIN dupes d ON p.DepartmentId = d.DuplicateId
                WHERE p.IsDeleted = 0;

                ;WITH ranked AS (
                    SELECT
                        Id,
                        SchoolId,
                        Code,
                        ROW_NUMBER() OVER (
                            PARTITION BY SchoolId, UPPER(LTRIM(RTRIM(Code)))
                            ORDER BY CreatedAt ASC, Id ASC
                        ) AS rn
                    FROM dbo.HrDepartments
                    WHERE IsDeleted = 0
                ),
                keepers AS (
                    SELECT Id, SchoolId, Code
                    FROM ranked
                    WHERE rn = 1
                ),
                dupes AS (
                    SELECT r.Id AS DuplicateId, k.Id AS KeeperId
                    FROM ranked r
                    INNER JOIN keepers k
                        ON k.SchoolId = r.SchoolId
                       AND UPPER(LTRIM(RTRIM(k.Code))) = UPPER(LTRIM(RTRIM(r.Code)))
                    WHERE r.rn > 1
                )
                UPDATE f
                SET f.DepartmentId = d.KeeperId,
                    f.UpdatedAt = SYSUTCDATETIME()
                FROM dbo.HrJobFunctions f
                INNER JOIN dupes d ON f.DepartmentId = d.DuplicateId
                WHERE f.IsDeleted = 0;

                ;WITH ranked AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY SchoolId, UPPER(LTRIM(RTRIM(Code)))
                            ORDER BY CreatedAt ASC, Id ASC
                        ) AS rn
                    FROM dbo.HrDepartments
                    WHERE IsDeleted = 0
                )
                UPDATE d
                SET d.IsDeleted = 1,
                    d.DeletedAt = SYSUTCDATETIME(),
                    d.UpdatedAt = SYSUTCDATETIME()
                FROM dbo.HrDepartments d
                INNER JOIN ranked r ON r.Id = d.Id
                WHERE r.rn > 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_HrDepartments_SchoolId_Code'
                      AND object_id = OBJECT_ID(N'dbo.HrDepartments')
                )
                BEGIN
                    SET QUOTED_IDENTIFIER ON;
                    SET ANSI_NULLS ON;
                    CREATE UNIQUE INDEX [IX_HrDepartments_SchoolId_Code]
                    ON dbo.HrDepartments ([SchoolId], [Code])
                    WHERE [IsDeleted] = 0;
                END
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'dbo.HrJobFunctions', N'U') IS NOT NULL
            BEGIN
                ;WITH ranked AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY SchoolId, ISNULL(DepartmentId, '00000000-0000-0000-0000-000000000000'), UPPER(LTRIM(RTRIM(Name)))
                            ORDER BY CreatedAt ASC, Id ASC
                        ) AS rn
                    FROM dbo.HrJobFunctions
                    WHERE IsDeleted = 0
                )
                UPDATE f
                SET f.IsDeleted = 1,
                    f.DeletedAt = SYSUTCDATETIME(),
                    f.UpdatedAt = SYSUTCDATETIME()
                FROM dbo.HrJobFunctions f
                INNER JOIN ranked r ON r.Id = f.Id
                WHERE r.rn > 1;
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
