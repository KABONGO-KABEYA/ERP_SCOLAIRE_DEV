using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Schéma module cartes élèves :
/// CarteModele, CarteParametres, Carte, CarteHistorique, CarteImpression.
/// </summary>
public sealed class StudentCardSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<StudentCardSchemaInitializer> _logger;

    public StudentCardSchemaInitializer(string connectionString, ILogger<StudentCardSchemaInitializer> logger)
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
            "Schéma cartes élèves vérifié (CarteModele, CarteParametres, Carte, CarteHistorique, CarteImpression).");
    }

    private static readonly string[] Scripts =
    [
        """
        IF OBJECT_ID(N'CarteModele', N'U') IS NULL
        BEGIN
            CREATE TABLE [CarteModele] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Name] nvarchar(120) NOT NULL,
                [Description] nvarchar(500) NULL,
                [WidthMm] decimal(8,2) NOT NULL CONSTRAINT [DF_CarteModele_WidthMm] DEFAULT(85.60),
                [HeightMm] decimal(8,2) NOT NULL CONSTRAINT [DF_CarteModele_HeightMm] DEFAULT(53.98),
                [Orientation] int NOT NULL CONSTRAINT [DF_CarteModele_Orientation] DEFAULT(2),
                [Kind] int NOT NULL CONSTRAINT [DF_CarteModele_Kind] DEFAULT(1),
                [LayoutJsonFront] nvarchar(max) NULL,
                [LayoutJsonBack] nvarchar(max) NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_CarteModele_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_CarteModele_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_CarteModele] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_CarteModele_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools]([Id])
            );
            CREATE UNIQUE INDEX [IX_CarteModele_School_Name]
                ON [CarteModele]([SchoolId], [Name]) WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_CarteModele_School_Active]
                ON [CarteModele]([SchoolId], [IsActive]);
        END
        """,
        """
        IF OBJECT_ID(N'CarteParametres', N'U') IS NULL
        BEGIN
            CREATE TABLE [CarteParametres] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [CardNumberPrefix] nvarchar(20) NOT NULL CONSTRAINT [DF_CarteParametres_Prefix] DEFAULT(N'CARD'),
                [DefaultValidityMonths] int NOT NULL CONSTRAINT [DF_CarteParametres_Validity] DEFAULT(12),
                [KeepQrOnRenewal] bit NOT NULL CONSTRAINT [DF_CarteParametres_KeepQr] DEFAULT(0),
                [NextSequence] int NOT NULL CONSTRAINT [DF_CarteParametres_Seq] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_CarteParametres_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_CarteParametres] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_CarteParametres_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools]([Id])
            );
            CREATE UNIQUE INDEX [IX_CarteParametres_School]
                ON [CarteParametres]([SchoolId]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'Carte', N'U') IS NULL
        BEGIN
            CREATE TABLE [Carte] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [StudentId] uniqueidentifier NOT NULL,
                [AcademicYearId] uniqueidentifier NOT NULL,
                [TemplateId] uniqueidentifier NOT NULL,
                [CardNumber] nvarchar(40) NOT NULL,
                [QrToken] nvarchar(64) NOT NULL,
                [IssuedAt] datetime2 NOT NULL,
                [PrintedAt] datetime2 NULL,
                [ExpiresAt] datetime2 NULL,
                [Status] int NOT NULL CONSTRAINT [DF_Carte_Status] DEFAULT(1),
                [DeactivationReason] nvarchar(500) NULL,
                [PrintCount] int NOT NULL CONSTRAINT [DF_Carte_PrintCount] DEFAULT(0),
                [Version] int NOT NULL CONSTRAINT [DF_Carte_Version] DEFAULT(1),
                [ReplacesCardId] uniqueidentifier NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_Carte_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_Carte] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Carte_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools]([Id]),
                CONSTRAINT [FK_Carte_Students] FOREIGN KEY ([StudentId]) REFERENCES [Students]([Id]),
                CONSTRAINT [FK_Carte_AcademicYears] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears]([Id]),
                CONSTRAINT [FK_Carte_Template] FOREIGN KEY ([TemplateId]) REFERENCES [CarteModele]([Id]),
                CONSTRAINT [FK_Carte_Replaces] FOREIGN KEY ([ReplacesCardId]) REFERENCES [Carte]([Id])
            );
            CREATE UNIQUE INDEX [IX_Carte_School_CardNumber]
                ON [Carte]([SchoolId], [CardNumber]) WHERE [IsDeleted] = 0;
            CREATE UNIQUE INDEX [IX_Carte_School_QrToken]
                ON [Carte]([SchoolId], [QrToken]) WHERE [IsDeleted] = 0;
            CREATE UNIQUE INDEX [IX_Carte_OneActivePerStudentYear]
                ON [Carte]([SchoolId], [StudentId], [AcademicYearId])
                WHERE [IsDeleted] = 0 AND [Status] = 2;
            CREATE INDEX [IX_Carte_School_Status_Expires]
                ON [Carte]([SchoolId], [Status], [ExpiresAt]);
            CREATE INDEX [IX_Carte_Student]
                ON [Carte]([StudentId], [AcademicYearId]);
        END
        """,
        """
        IF OBJECT_ID(N'CarteHistorique', N'U') IS NULL
        BEGIN
            CREATE TABLE [CarteHistorique] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [CardId] uniqueidentifier NOT NULL,
                [Action] int NOT NULL,
                [UserId] uniqueidentifier NULL,
                [OccurredAt] datetime2 NOT NULL,
                [OldValue] nvarchar(2000) NULL,
                [NewValue] nvarchar(2000) NULL,
                [Notes] nvarchar(500) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_CarteHistorique_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_CarteHistorique] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_CarteHistorique_Carte] FOREIGN KEY ([CardId]) REFERENCES [Carte]([Id])
            );
            CREATE INDEX [IX_CarteHistorique_Card_Occurred]
                ON [CarteHistorique]([CardId], [OccurredAt]);
            CREATE INDEX [IX_CarteHistorique_School_Occurred]
                ON [CarteHistorique]([SchoolId], [OccurredAt]);
        END
        """,
        """
        IF OBJECT_ID(N'CarteImpression', N'U') IS NULL
        BEGIN
            CREATE TABLE [CarteImpression] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [CardId] uniqueidentifier NOT NULL,
                [PrintedAt] datetime2 NOT NULL,
                [PrintedBy] uniqueidentifier NULL,
                [Reason] nvarchar(500) NULL,
                [IsReprint] bit NOT NULL CONSTRAINT [DF_CarteImpression_IsReprint] DEFAULT(0),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_CarteImpression_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_CarteImpression] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_CarteImpression_Carte] FOREIGN KEY ([CardId]) REFERENCES [Carte]([Id])
            );
            CREATE INDEX [IX_CarteImpression_Card_Printed]
                ON [CarteImpression]([CardId], [PrintedAt]);
        END
        """
    ];
}
