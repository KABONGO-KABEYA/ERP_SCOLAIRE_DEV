using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class DocumentBrandingSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DocumentBrandingSchemaInitializer> _logger;

    public DocumentBrandingSchemaInitializer(string connectionString, ILogger<DocumentBrandingSchemaInitializer> logger)
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

        _logger.LogInformation("Schéma document branding vérifié (EcoleLogo, EcoleEntete, EcoleSignature, EcoleCachet, EcolePiedPage).");
    }

    private static readonly string[] Scripts =
    [
        """
        IF OBJECT_ID(N'EcoleLogo', N'U') IS NULL
        BEGIN
            CREATE TABLE [EcoleLogo] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Name] nvarchar(150) NOT NULL,
                [ImagePath] nvarchar(500) NOT NULL,
                [IsPrimary] bit NOT NULL,
                [IsActive] bit NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_EcoleLogo] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_EcoleLogo_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE INDEX [IX_EcoleLogo_IsDeleted] ON [EcoleLogo] ([IsDeleted]);
            CREATE INDEX [IX_EcoleLogo_SchoolId_IsPrimary] ON [EcoleLogo] ([SchoolId], [IsPrimary]);
            CREATE INDEX [IX_EcoleLogo_SchoolId_Name] ON [EcoleLogo] ([SchoolId], [Name]);
        END
        """,
        """
        IF OBJECT_ID(N'EcoleEntete', N'U') IS NULL
        BEGIN
            CREATE TABLE [EcoleEntete] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Name] nvarchar(150) NOT NULL,
                [DocumentType] int NOT NULL,
                [PrintMode] int NOT NULL,
                [ImagePath] nvarchar(500) NULL,
                [WidthPx] int NULL,
                [HeightPx] int NULL,
                [ResolutionDpi] int NULL,
                [IsActive] bit NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_EcoleEntete] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_EcoleEntete_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE INDEX [IX_EcoleEntete_IsDeleted] ON [EcoleEntete] ([IsDeleted]);
            CREATE INDEX [IX_EcoleEntete_SchoolId_DocumentType_Name] ON [EcoleEntete] ([SchoolId], [DocumentType], [Name]);
        END
        """,
        """
        IF COL_LENGTH(N'EcoleEntete', N'ApplicableDocumentTypes') IS NULL
        BEGIN
            ALTER TABLE [EcoleEntete] ADD [ApplicableDocumentTypes] nvarchar(200) NULL;
        END
        """,
        """
        IF COL_LENGTH(N'EcoleEntete', N'MarginLeftMm') IS NULL
        BEGIN
            ALTER TABLE [EcoleEntete] ADD [MarginLeftMm] decimal(9,2) NOT NULL CONSTRAINT [DF_EcoleEntete_MarginLeftMm] DEFAULT 0;
        END
        """,
        """
        IF COL_LENGTH(N'EcoleEntete', N'MarginRightMm') IS NULL
        BEGIN
            ALTER TABLE [EcoleEntete] ADD [MarginRightMm] decimal(9,2) NOT NULL CONSTRAINT [DF_EcoleEntete_MarginRightMm] DEFAULT 0;
        END
        """,
        """
        IF COL_LENGTH(N'EcoleEntete', N'MaxHeightMm') IS NULL
        BEGIN
            ALTER TABLE [EcoleEntete] ADD [MaxHeightMm] decimal(9,2) NULL;
        END
        """,
        """
        IF OBJECT_ID(N'EcoleSignature', N'U') IS NULL
        BEGIN
            CREATE TABLE [EcoleSignature] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [SignatoryName] nvarchar(150) NOT NULL,
                [Function] nvarchar(150) NOT NULL,
                [ImagePath] nvarchar(500) NOT NULL,
                [IsActive] bit NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_EcoleSignature] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_EcoleSignature_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE INDEX [IX_EcoleSignature_IsDeleted] ON [EcoleSignature] ([IsDeleted]);
            CREATE INDEX [IX_EcoleSignature_SchoolId_Function] ON [EcoleSignature] ([SchoolId], [Function]);
        END
        """,
        """
        IF COL_LENGTH(N'EcoleSignature', N'DocumentType') IS NULL
        BEGIN
            ALTER TABLE [EcoleSignature] ADD [DocumentType] int NOT NULL CONSTRAINT [DF_EcoleSignature_DocumentType] DEFAULT 99;
        END
        """,
        """
        IF COL_LENGTH(N'EcoleSignature', N'ApplicableDocumentTypes') IS NULL
        BEGIN
            ALTER TABLE [EcoleSignature] ADD [ApplicableDocumentTypes] nvarchar(200) NULL;
        END
        """,
        """
        IF OBJECT_ID(N'EcoleCachet', N'U') IS NULL
        BEGIN
            CREATE TABLE [EcoleCachet] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Name] nvarchar(150) NOT NULL,
                [ImagePath] nvarchar(500) NOT NULL,
                [IsActive] bit NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_EcoleCachet] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_EcoleCachet_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE INDEX [IX_EcoleCachet_IsDeleted] ON [EcoleCachet] ([IsDeleted]);
            CREATE INDEX [IX_EcoleCachet_SchoolId_Name] ON [EcoleCachet] ([SchoolId], [Name]);
        END
        """,
        """
        IF OBJECT_ID(N'EcolePiedPage', N'U') IS NULL
        BEGIN
            CREATE TABLE [EcolePiedPage] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Address] nvarchar(300) NULL,
                [Phone] nvarchar(50) NULL,
                [Email] nvarchar(150) NULL,
                [Website] nvarchar(200) NULL,
                [PoBox] nvarchar(50) NULL,
                [SchoolMotto] nvarchar(200) NULL,
                [FreeText] nvarchar(2000) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_EcolePiedPage] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_EcolePiedPage_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE INDEX [IX_EcolePiedPage_IsDeleted] ON [EcolePiedPage] ([IsDeleted]);
            CREATE UNIQUE INDEX [IX_EcolePiedPage_SchoolId] ON [EcolePiedPage] ([SchoolId]);
        END
        """
    ];
}
