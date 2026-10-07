using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class ParentNoticeSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<ParentNoticeSchemaInitializer> _logger;

    public ParentNoticeSchemaInitializer(string connectionString, ILogger<ParentNoticeSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'ParentNotices', N'U') IS NULL
            BEGIN
                CREATE TABLE [ParentNotices] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [Title] nvarchar(200) NOT NULL,
                    [Subject] nvarchar(300) NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [FeeTypeId] uniqueidentifier NOT NULL,
                    [SectionId] uniqueidentifier NULL,
                    [ClassRoomId] uniqueidentifier NULL,
                    [TargetDescription] nvarchar(500) NOT NULL,
                    [ContentRtfBase64] nvarchar(max) NOT NULL,
                    [SelectedInstallmentIdsJson] nvarchar(max) NOT NULL CONSTRAINT [DF_ParentNotices_SelectedInstallments] DEFAULT N'[]',
                    [IncludeSchoolHeader] bit NOT NULL CONSTRAINT [DF_ParentNotices_IncludeHeader] DEFAULT 1,
                    [PageLayout] int NOT NULL CONSTRAINT [DF_ParentNotices_PageLayout] DEFAULT 1,
                    [PageOrientation] int NOT NULL CONSTRAINT [DF_ParentNotices_PageOrientation] DEFAULT 1,
                    [RecipientStudentIdsJson] nvarchar(max) NOT NULL CONSTRAINT [DF_ParentNotices_Recipients] DEFAULT N'[]',
                    [GeneratedSnapshotJson] nvarchar(max) NOT NULL CONSTRAINT [DF_ParentNotices_Snapshots] DEFAULT N'[]',
                    [RecipientCount] int NOT NULL CONSTRAINT [DF_ParentNotices_RecipientCount] DEFAULT 0,
                    [Status] int NOT NULL CONSTRAINT [DF_ParentNotices_Status] DEFAULT 1,
                    [GeneratedAtUtc] datetime2 NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_ParentNotices] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ParentNotices_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                    CONSTRAINT [FK_ParentNotices_AcademicYears] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
                    CONSTRAINT [FK_ParentNotices_FeeTypes] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id])
                );
                CREATE INDEX [IX_ParentNotices_IsDeleted] ON [ParentNotices] ([IsDeleted]);
                CREATE INDEX [IX_ParentNotices_SchoolId_CreatedAt] ON [ParentNotices] ([SchoolId], [CreatedAt]);
                CREATE INDEX [IX_ParentNotices_SchoolId_Status] ON [ParentNotices] ([SchoolId], [Status]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH(N'ParentNotices', N'GeneratedSnapshotJson') IS NULL
                    ALTER TABLE [ParentNotices] ADD [GeneratedSnapshotJson] nvarchar(max) NOT NULL
                        CONSTRAINT [DF_ParentNotices_Snapshots] DEFAULT N'[]';
                IF COL_LENGTH(N'ParentNotices', N'SelectedInstallmentIdsJson') IS NULL
                    ALTER TABLE [ParentNotices] ADD [SelectedInstallmentIdsJson] nvarchar(max) NOT NULL
                        CONSTRAINT [DF_ParentNotices_SelectedInstallments] DEFAULT N'[]';
                IF COL_LENGTH(N'ParentNotices', N'IncludeSchoolHeader') IS NULL
                    ALTER TABLE [ParentNotices] ADD [IncludeSchoolHeader] bit NOT NULL
                        CONSTRAINT [DF_ParentNotices_IncludeHeader] DEFAULT 1;
                IF COL_LENGTH(N'ParentNotices', N'PageLayout') IS NULL
                    ALTER TABLE [ParentNotices] ADD [PageLayout] int NOT NULL
                        CONSTRAINT [DF_ParentNotices_PageLayout] DEFAULT 1;
                IF COL_LENGTH(N'ParentNotices', N'PageOrientation') IS NULL
                    ALTER TABLE [ParentNotices] ADD [PageOrientation] int NOT NULL
                        CONSTRAINT [DF_ParentNotices_PageOrientation] DEFAULT 1;
            END
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Schéma des avis aux parents vérifié.");
    }
}
