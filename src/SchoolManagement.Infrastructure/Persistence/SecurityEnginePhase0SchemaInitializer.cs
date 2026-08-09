using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Phase 0 moteur de sécurité : colonnes + tables catalogue / exceptions / audit (idempotent).
/// Complète la migration EF <c>SecurityEnginePhase0Foundation</c> pour le démarrage API.
/// </summary>
public sealed class SecurityEnginePhase0SchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<SecurityEnginePhase0SchemaInitializer> _logger;

    public SecurityEnginePhase0SchemaInitializer(
        string connectionString,
        ILogger<SecurityEnginePhase0SchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecAsync(connection, """
            IF COL_LENGTH(N'dbo.UserAccounts', N'IsPlatformSuperAdmin') IS NULL
                ALTER TABLE dbo.UserAccounts ADD IsPlatformSuperAdmin BIT NOT NULL
                    CONSTRAINT DF_UserAccounts_IsPlatformSuperAdmin DEFAULT(0);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_UserAccounts_TeacherId'
                  AND object_id = OBJECT_ID(N'dbo.UserAccounts'))
            BEGIN
                CREATE UNIQUE INDEX IX_UserAccounts_TeacherId
                    ON dbo.UserAccounts(TeacherId)
                    WHERE TeacherId IS NOT NULL AND IsDeleted = 0;
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_UserAccounts_GuardianId'
                  AND object_id = OBJECT_ID(N'dbo.UserAccounts'))
            BEGIN
                CREATE UNIQUE INDEX IX_UserAccounts_GuardianId
                    ON dbo.UserAccounts(GuardianId)
                    WHERE GuardianId IS NOT NULL AND IsDeleted = 0;
            END

            IF COL_LENGTH(N'dbo.Roles', N'IsSystem') IS NULL
                ALTER TABLE dbo.Roles ADD IsSystem BIT NOT NULL
                    CONSTRAINT DF_Roles_IsSystem DEFAULT(0);

            IF COL_LENGTH(N'dbo.Roles', N'IsAssignable') IS NULL
                ALTER TABLE dbo.Roles ADD IsAssignable BIT NOT NULL
                    CONSTRAINT DF_Roles_IsAssignable DEFAULT(1);

            IF COL_LENGTH(N'dbo.Roles', N'SortOrder') IS NULL
                ALTER TABLE dbo.Roles ADD SortOrder INT NOT NULL
                    CONSTRAINT DF_Roles_SortOrder DEFAULT(0);

            IF COL_LENGTH(N'dbo.Permissions', N'DisplayName') IS NULL
                ALTER TABLE dbo.Permissions ADD DisplayName NVARCHAR(150) NOT NULL
                    CONSTRAINT DF_Permissions_DisplayName DEFAULT(N'');

            IF COL_LENGTH(N'dbo.Permissions', N'BusinessDescription') IS NULL
                ALTER TABLE dbo.Permissions ADD BusinessDescription NVARCHAR(MAX) NOT NULL
                    CONSTRAINT DF_Permissions_BusinessDescription DEFAULT(N'');

            IF COL_LENGTH(N'dbo.Permissions', N'HelpText') IS NULL
                ALTER TABLE dbo.Permissions ADD HelpText NVARCHAR(MAX) NULL;

            IF COL_LENGTH(N'dbo.Permissions', N'IsActive') IS NULL
                ALTER TABLE dbo.Permissions ADD IsActive BIT NOT NULL
                    CONSTRAINT DF_Permissions_IsActive DEFAULT(1);

            IF COL_LENGTH(N'dbo.Permissions', N'SecurityActionId') IS NULL
                ALTER TABLE dbo.Permissions ADD SecurityActionId UNIQUEIDENTIFIER NULL;
            """, cancellationToken);

        // Batch séparé : SQL Server compile le batch entier avant exécution.
        await ExecAsync(connection, """
            UPDATE dbo.Permissions
            SET DisplayName = CASE WHEN DisplayName = N'' THEN Code ELSE DisplayName END,
                BusinessDescription = CASE
                    WHEN BusinessDescription = N'' THEN ISNULL(Description, Code)
                    ELSE BusinessDescription END,
                HelpText = COALESCE(HelpText, Description, Code),
                IsActive = 1;
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SecurityModules', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SecurityModules
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SecurityModules PRIMARY KEY,
                    Code NVARCHAR(50) NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    Icon NVARCHAR(100) NULL,
                    SortOrder INT NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_SecurityModules_IsActive DEFAULT(1),
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SecurityModules_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL
                );
                CREATE UNIQUE INDEX IX_SecurityModules_Code ON dbo.SecurityModules(Code);
                CREATE INDEX IX_SecurityModules_IsDeleted ON dbo.SecurityModules(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SecurityFunctions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SecurityFunctions
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SecurityFunctions PRIMARY KEY,
                    ModuleId UNIQUEIDENTIFIER NOT NULL,
                    Code NVARCHAR(50) NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    Icon NVARCHAR(100) NULL,
                    SortOrder INT NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_SecurityFunctions_IsActive DEFAULT(1),
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SecurityFunctions_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_SecurityFunctions_SecurityModules_ModuleId
                        FOREIGN KEY (ModuleId) REFERENCES dbo.SecurityModules(Id)
                );
                CREATE UNIQUE INDEX IX_SecurityFunctions_ModuleId_Code ON dbo.SecurityFunctions(ModuleId, Code);
                CREATE INDEX IX_SecurityFunctions_IsDeleted ON dbo.SecurityFunctions(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SecurityPages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SecurityPages
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SecurityPages PRIMARY KEY,
                    FunctionId UNIQUEIDENTIFIER NOT NULL,
                    Code NVARCHAR(80) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    SortOrder INT NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_SecurityPages_IsActive DEFAULT(1),
                    RequiredPermissionCode NVARCHAR(100) NULL,
                    DesktopViewKey NVARCHAR(150) NULL,
                    WebRoute NVARCHAR(200) NULL,
                    MobileScreenKey NVARCHAR(150) NULL,
                    DeepLink NVARCHAR(300) NULL,
                    IsAvailableOnDesktop BIT NOT NULL,
                    IsAvailableOnWeb BIT NOT NULL,
                    IsAvailableOnMobile BIT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SecurityPages_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_SecurityPages_SecurityFunctions_FunctionId
                        FOREIGN KEY (FunctionId) REFERENCES dbo.SecurityFunctions(Id)
                );
                CREATE UNIQUE INDEX IX_SecurityPages_FunctionId_Code ON dbo.SecurityPages(FunctionId, Code);
                CREATE INDEX IX_SecurityPages_DesktopViewKey ON dbo.SecurityPages(DesktopViewKey);
                CREATE INDEX IX_SecurityPages_IsDeleted ON dbo.SecurityPages(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SecurityActions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SecurityActions
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SecurityActions PRIMARY KEY,
                    PageId UNIQUEIDENTIFIER NOT NULL,
                    Code NVARCHAR(80) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    SortOrder INT NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_SecurityActions_IsActive DEFAULT(1),
                    IsAvailableOnDesktop BIT NOT NULL,
                    IsAvailableOnWeb BIT NOT NULL,
                    IsAvailableOnMobile BIT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SecurityActions_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_SecurityActions_SecurityPages_PageId
                        FOREIGN KEY (PageId) REFERENCES dbo.SecurityPages(Id)
                );
                CREATE UNIQUE INDEX IX_SecurityActions_PageId_Code ON dbo.SecurityActions(PageId, Code);
                CREATE INDEX IX_SecurityActions_IsDeleted ON dbo.SecurityActions(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Permissions_SecurityActions_SecurityActionId')
               AND COL_LENGTH(N'dbo.Permissions', N'SecurityActionId') IS NOT NULL
               AND OBJECT_ID(N'dbo.SecurityActions', N'U') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.Permissions WITH CHECK
                ADD CONSTRAINT FK_Permissions_SecurityActions_SecurityActionId
                    FOREIGN KEY (SecurityActionId) REFERENCES dbo.SecurityActions(Id) ON DELETE SET NULL;
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Permissions_SecurityActionId' AND object_id = OBJECT_ID(N'dbo.Permissions'))
                CREATE INDEX IX_Permissions_SecurityActionId ON dbo.Permissions(SecurityActionId);
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.PermissionDependencies', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.PermissionDependencies
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PermissionDependencies PRIMARY KEY,
                    PermissionId UNIQUEIDENTIFIER NOT NULL,
                    RequiresPermissionId UNIQUEIDENTIFIER NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_PermissionDependencies_IsActive DEFAULT(1),
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_PermissionDependencies_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT CK_PermissionDependencies_NoSelf CHECK (PermissionId <> RequiresPermissionId),
                    CONSTRAINT FK_PermissionDependencies_Permissions_PermissionId
                        FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_PermissionDependencies_Permissions_RequiresPermissionId
                        FOREIGN KEY (RequiresPermissionId) REFERENCES dbo.Permissions(Id)
                );
                CREATE UNIQUE INDEX IX_PermissionDependencies_PermissionId_RequiresPermissionId
                    ON dbo.PermissionDependencies(PermissionId, RequiresPermissionId);
                CREATE INDEX IX_PermissionDependencies_RequiresPermissionId
                    ON dbo.PermissionDependencies(RequiresPermissionId);
                CREATE INDEX IX_PermissionDependencies_IsDeleted ON dbo.PermissionDependencies(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.UserPermissionExceptions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.UserPermissionExceptions
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserPermissionExceptions PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    PermissionId UNIQUEIDENTIFIER NOT NULL,
                    Effect INT NOT NULL,
                    ValidFrom DATETIME2 NOT NULL,
                    ValidTo DATETIME2 NULL,
                    Reason NVARCHAR(500) NULL,
                    GrantedByUserId UNIQUEIDENTIFIER NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_UserPermissionExceptions_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_UserPermissionExceptions_Schools_SchoolId
                        FOREIGN KEY (SchoolId) REFERENCES dbo.Schools(Id),
                    CONSTRAINT FK_UserPermissionExceptions_Permissions_PermissionId
                        FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(Id),
                    CONSTRAINT FK_UserPermissionExceptions_UserAccounts_UserId
                        FOREIGN KEY (UserId) REFERENCES dbo.UserAccounts(Id),
                    CONSTRAINT FK_UserPermissionExceptions_UserAccounts_GrantedByUserId
                        FOREIGN KEY (GrantedByUserId) REFERENCES dbo.UserAccounts(Id)
                );
                CREATE INDEX IX_UserPermissionExceptions_SchoolId_UserId
                    ON dbo.UserPermissionExceptions(SchoolId, UserId);
                CREATE INDEX IX_UserPermissionExceptions_UserId_PermissionId_Effect_ValidFrom_ValidTo
                    ON dbo.UserPermissionExceptions(UserId, PermissionId, Effect, ValidFrom, ValidTo);
                CREATE INDEX IX_UserPermissionExceptions_PermissionId ON dbo.UserPermissionExceptions(PermissionId);
                CREATE INDEX IX_UserPermissionExceptions_GrantedByUserId ON dbo.UserPermissionExceptions(GrantedByUserId);
                CREATE INDEX IX_UserPermissionExceptions_IsDeleted ON dbo.UserPermissionExceptions(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SecurityAuditLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SecurityAuditLogs
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SecurityAuditLogs PRIMARY KEY,
                    OccurredAtUtc DATETIME2 NOT NULL,
                    SchoolId UNIQUEIDENTIFIER NULL,
                    ActorUserId UNIQUEIDENTIFIER NULL,
                    ActorUserName NVARCHAR(100) NOT NULL,
                    ActorKind INT NOT NULL,
                    ActionType NVARCHAR(80) NOT NULL,
                    TargetEntityType NVARCHAR(100) NULL,
                    TargetEntityId UNIQUEIDENTIFIER NULL,
                    TargetUserName NVARCHAR(100) NULL,
                    Summary NVARCHAR(500) NOT NULL,
                    OldValuesJson NVARCHAR(MAX) NULL,
                    NewValuesJson NVARCHAR(MAX) NULL,
                    IpAddress NVARCHAR(64) NULL,
                    UserAgent NVARCHAR(500) NULL,
                    CorrelationId UNIQUEIDENTIFIER NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SecurityAuditLogs_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_SecurityAuditLogs_Schools_SchoolId
                        FOREIGN KEY (SchoolId) REFERENCES dbo.Schools(Id) ON DELETE SET NULL
                );
                CREATE INDEX IX_SecurityAuditLogs_SchoolId_OccurredAtUtc
                    ON dbo.SecurityAuditLogs(SchoolId, OccurredAtUtc);
                CREATE INDEX IX_SecurityAuditLogs_ActionType_OccurredAtUtc
                    ON dbo.SecurityAuditLogs(ActionType, OccurredAtUtc);
                CREATE INDEX IX_SecurityAuditLogs_IsDeleted ON dbo.SecurityAuditLogs(IsDeleted);
            END
            """, cancellationToken);

        // Enregistre la migration EF dans l'historique si absente (évite un double apply manuel).
        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM dbo.__EFMigrationsHistory
                    WHERE MigrationId = N'20260807081353_SecurityEnginePhase0Foundation')
            BEGIN
                INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
                VALUES (N'20260807081353_SecurityEnginePhase0Foundation', N'8.0.0');
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma sécurité Phase 0 vérifié.");
    }

    private static async Task ExecAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
