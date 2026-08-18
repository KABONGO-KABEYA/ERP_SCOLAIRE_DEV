using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Schools;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class CurriculumSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<CurriculumSchemaInitializer> _logger;

    public CurriculumSchemaInitializer(string connectionString, ILogger<CurriculumSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Idempotent. Utilisé au démarrage API, au Setup, et par le drain Cloud
    /// (<c>EnsureRemoteCurriculumSchemaAsync</c>) : DROP <c>IX_Courses_Code</c>,
    /// unique tenant <c>IX_Courses_SchoolId_Code_ClassRoomId</c>, tables/colonnes pédagogiques.
    /// Aucune donnée n'est supprimée.
    /// </summary>
    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Branches')
            BEGIN
                CREATE TABLE [Branches] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [Code] nvarchar(100) NOT NULL,
                    [Name] nvarchar(150) NOT NULL,
                    [Program] int NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Branches_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_Branches_SchoolId_Code] ON [Branches] ([SchoolId], [Code]) WHERE [IsDeleted] = 0;
                CREATE INDEX [IX_Branches_IsDeleted] ON [Branches] ([IsDeleted]);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Courses') AND name = 'BranchId')
            BEGIN
                ALTER TABLE [Courses] ADD [BranchId] uniqueidentifier NULL;
            END

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Courses_Branches_BranchId')
            BEGIN
                ALTER TABLE [Courses] ADD CONSTRAINT [FK_Courses_Branches_BranchId]
                    FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE SET NULL;
            END

            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Courses_Code' AND object_id = OBJECT_ID('Courses'))
            BEGIN
                DROP INDEX [IX_Courses_Code] ON [Courses];
            END

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_Courses_SchoolId_Code_ClassRoomId'
                  AND object_id = OBJECT_ID('Courses')
                  AND (
                      is_unique = 0
                      OR has_filter = 0
                      OR filter_definition NOT LIKE '%ClassRoomId%IS NULL%'
                      OR filter_definition NOT LIKE '%IsDeleted%'
                  )
            )
            BEGIN
                DROP INDEX [IX_Courses_SchoolId_Code_ClassRoomId] ON [Courses];
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Courses_SchoolId_Code_ClassRoomId' AND object_id = OBJECT_ID('Courses'))
            BEGIN
                CREATE UNIQUE INDEX [IX_Courses_SchoolId_Code_ClassRoomId]
                    ON [Courses] ([SchoolId], [Code], [ClassRoomId])
                    WHERE [ClassRoomId] IS NULL AND [IsDeleted] = 0;
            END

            -- Migration EF AddPedagogicalStructure : absente de 001_InitialCreate_EF.sql (install vierge Setup).
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
            BEGIN
                CREATE TABLE [PedagogicalClasses] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [TemplateCode] nvarchar(50) NOT NULL,
                    [Program] int NOT NULL,
                    [LevelOrder] int NOT NULL,
                    [DisplayName] nvarchar(200) NOT NULL,
                    [HumanitiesSection] nvarchar(100) NULL,
                    [StudyOption] nvarchar(100) NULL,
                    [MinAge] int NULL,
                    [MaxAge] int NULL,
                    [IsEnabled] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_PedagogicalClasses] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PedagogicalClasses_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
                );
                CREATE INDEX [IX_PedagogicalClasses_IsDeleted] ON [PedagogicalClasses] ([IsDeleted]);
                CREATE INDEX [IX_PedagogicalClasses_SchoolId_IsEnabled] ON [PedagogicalClasses] ([SchoolId], [IsEnabled]);
                CREATE UNIQUE INDEX [IX_PedagogicalClasses_SchoolId_TemplateCode] ON [PedagogicalClasses] ([SchoolId], [TemplateCode]);
            END

            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassRooms')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'PedagogicalClassId')
                    ALTER TABLE [ClassRooms] ADD [PedagogicalClassId] uniqueidentifier NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'IsActive')
                    ALTER TABLE [ClassRooms] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_ClassRooms_IsActive] DEFAULT 0;

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'Observations')
                    ALTER TABLE [ClassRooms] ADD [Observations] nvarchar(500) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ClassRooms_PedagogicalClasses_PedagogicalClassId')
                   AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
                   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'PedagogicalClassId')
                BEGIN
                    ALTER TABLE [ClassRooms] ADD CONSTRAINT [FK_ClassRooms_PedagogicalClasses_PedagogicalClassId]
                        FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]) ON DELETE NO ACTION;
                END
            END

            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClassCourses')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
            BEGIN
                CREATE TABLE [PedagogicalClassCourses] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [PedagogicalClassId] uniqueidentifier NOT NULL,
                    [CourseId] uniqueidentifier NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_PedagogicalClassCourses] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PedagogicalClassCourses_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PedagogicalClassCourses_PedagogicalClasses_PedagogicalClassId] FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_PedagogicalClassCourses_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_PedagogicalClassCourses_PedagogicalClassId_CourseId]
                    ON [PedagogicalClassCourses] ([PedagogicalClassId], [CourseId]) WHERE [IsDeleted] = 0;
                CREATE INDEX [IX_PedagogicalClassCourses_CourseId] ON [PedagogicalClassCourses] ([CourseId]);
                CREATE INDEX [IX_PedagogicalClassCourses_IsDeleted] ON [PedagogicalClassCourses] ([IsDeleted]);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PedagogicalClassCourses') AND name = 'MaxScore')
            BEGIN
                ALTER TABLE [PedagogicalClassCourses] ADD [MaxScore] int NOT NULL CONSTRAINT [DF_PedagogicalClassCourses_MaxScore] DEFAULT 20;
            END
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureHumanitiesColumnsAsync(connection, cancellationToken);
        _logger.LogInformation("Schéma curriculum (Branches, StudyOptions.HumanitiesSection, PedagogicalClassCourses) vérifié.");
    }

    /// <summary>
    /// Colonne absente des installs 001_InitialCreate (locaux Humanité + sync cloud).
    /// Sûr à exécuter sur la base locale et sur le SQL cloud (IF NOT EXISTS).
    /// </summary>
    public async Task EnsureHumanitiesColumnsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureHumanitiesColumnsAsync(connection, cancellationToken);
    }

    private static async Task EnsureHumanitiesColumnsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StudyOptions')
               AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudyOptions') AND name = 'HumanitiesSection')
            BEGIN
                ALTER TABLE [StudyOptions] ADD [HumanitiesSection] nvarchar(100) NULL;
            END

            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
               AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PedagogicalClasses') AND name = 'HumanitiesSection')
            BEGIN
                ALTER TABLE [PedagogicalClasses] ADD [HumanitiesSection] nvarchar(100) NULL;
            END

            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
               AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PedagogicalClasses') AND name = 'StudyOption')
            BEGIN
                ALTER TABLE [PedagogicalClasses] ADD [StudyOption] nvarchar(100) NULL;
            END
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
