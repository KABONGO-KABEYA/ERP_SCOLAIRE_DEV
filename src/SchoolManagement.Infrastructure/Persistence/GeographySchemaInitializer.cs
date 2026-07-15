using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class GeographySchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<GeographySchemaInitializer> _logger;

    public GeographySchemaInitializer(string connectionString, ILogger<GeographySchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'[Pays]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Pays] (
                    [IdPays] uniqueidentifier NOT NULL,
                    [CodePays] nvarchar(10) NOT NULL,
                    [NomPays] nvarchar(150) NOT NULL,
                    [Actif] bit NOT NULL CONSTRAINT [DF_Pays_Actif] DEFAULT 1,
                    [DateCreation] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [DateModification] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_Pays_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_Pays] PRIMARY KEY ([IdPays])
                );
                CREATE UNIQUE INDEX [IX_Pays_CodePays] ON [Pays]([CodePays]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'[Province]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Province] (
                    [IdProvince] uniqueidentifier NOT NULL,
                    [IdPays] uniqueidentifier NOT NULL,
                    [CodeProvince] nvarchar(10) NOT NULL,
                    [NomProvince] nvarchar(150) NOT NULL,
                    [Actif] bit NOT NULL CONSTRAINT [DF_Province_Actif] DEFAULT 1,
                    [DateCreation] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [DateModification] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_Province_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_Province] PRIMARY KEY ([IdProvince]),
                    CONSTRAINT [FK_Province_Pays] FOREIGN KEY ([IdPays]) REFERENCES [Pays]([IdPays])
                );
                CREATE UNIQUE INDEX [IX_Province_IdPays_CodeProvince] ON [Province]([IdPays], [CodeProvince]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'[Ville]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Ville] (
                    [IdVille] uniqueidentifier NOT NULL,
                    [IdProvince] uniqueidentifier NOT NULL,
                    [CodeVille] nvarchar(10) NOT NULL,
                    [NomVille] nvarchar(150) NOT NULL,
                    [Actif] bit NOT NULL CONSTRAINT [DF_Ville_Actif] DEFAULT 1,
                    [DateCreation] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [DateModification] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_Ville_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_Ville] PRIMARY KEY ([IdVille]),
                    CONSTRAINT [FK_Ville_Province] FOREIGN KEY ([IdProvince]) REFERENCES [Province]([IdProvince])
                );
                CREATE UNIQUE INDEX [IX_Ville_IdProvince_CodeVille] ON [Ville]([IdProvince], [CodeVille]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'[Commune]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Commune] (
                    [IdCommune] uniqueidentifier NOT NULL,
                    [IdVille] uniqueidentifier NOT NULL,
                    [CodeCommune] nvarchar(10) NOT NULL,
                    [NomCommune] nvarchar(150) NOT NULL,
                    [Actif] bit NOT NULL CONSTRAINT [DF_Commune_Actif] DEFAULT 1,
                    [DateCreation] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [DateModification] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_Commune_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_Commune] PRIMARY KEY ([IdCommune]),
                    CONSTRAINT [FK_Commune_Ville] FOREIGN KEY ([IdVille]) REFERENCES [Ville]([IdVille])
                );
                CREATE UNIQUE INDEX [IX_Commune_IdVille_CodeCommune] ON [Commune]([IdVille], [CodeCommune]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'[Adresse]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Adresse] (
                    [IdAdresse] uniqueidentifier NOT NULL,
                    [IdPays] uniqueidentifier NULL,
                    [IdProvince] uniqueidentifier NULL,
                    [IdVille] uniqueidentifier NULL,
                    [IdCommune] uniqueidentifier NULL,
                    [Quartier] nvarchar(150) NULL,
                    [Avenue] nvarchar(200) NULL,
                    [NumeroMaison] nvarchar(30) NULL,
                    [DateCreation] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [DateModification] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_Adresse_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_Adresse] PRIMARY KEY ([IdAdresse]),
                    CONSTRAINT [FK_Adresse_Pays] FOREIGN KEY ([IdPays]) REFERENCES [Pays]([IdPays]),
                    CONSTRAINT [FK_Adresse_Province] FOREIGN KEY ([IdProvince]) REFERENCES [Province]([IdProvince]),
                    CONSTRAINT [FK_Adresse_Ville] FOREIGN KEY ([IdVille]) REFERENCES [Ville]([IdVille]),
                    CONSTRAINT [FK_Adresse_Commune] FOREIGN KEY ([IdCommune]) REFERENCES [Commune]([IdCommune])
                );
            END
            """, cancellationToken);

        await EnsureColumnAsync(connection, "Students", "AddressId", """
            ALTER TABLE [Students] ADD [AddressId] uniqueidentifier NULL;
            """, cancellationToken);

        await EnsureColumnAsync(connection, "Guardians", "AddressId", """
            ALTER TABLE [Guardians] ADD [AddressId] uniqueidentifier NULL;
            """, cancellationToken);

        await EnsureColumnAsync(connection, "UserAccounts", "AddressId", """
            ALTER TABLE [UserAccounts] ADD [AddressId] uniqueidentifier NULL;
            """, cancellationToken);

        await EnsureColumnAsync(connection, "Teachers", "AddressId", """
            ALTER TABLE [Teachers] ADD [AddressId] uniqueidentifier NULL;
            """, cancellationToken);

        await SeedAsync(connection, cancellationToken);
        _logger.LogInformation("Schéma géographique et adresses vérifié (Pays, Province, Ville, Commune, Adresse).");
    }

    private static async Task SeedAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(1) FROM [Pays]";
        var count = (int)(await check.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (count > 0)
        {
            return;
        }

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var sql = $"""
            INSERT INTO [Pays] ([IdPays],[CodePays],[NomPays],[Actif],[DateCreation],[IsDeleted])
            VALUES ('{GeographySeedData.RdcCountryId}','RDC',N'République Démocratique du Congo',1,'{now}',0);

            INSERT INTO [Province] ([IdProvince],[IdPays],[CodeProvince],[NomProvince],[Actif],[DateCreation],[IsDeleted]) VALUES
            ('{GeographySeedData.KinshasaProvinceId}','{GeographySeedData.RdcCountryId}','KIN',N'Kinshasa',1,'{now}',0),
            ('{GeographySeedData.KongoCentralProvinceId}','{GeographySeedData.RdcCountryId}','KOC',N'Kongo Central',1,'{now}',0),
            ('{GeographySeedData.HautKatangaProvinceId}','{GeographySeedData.RdcCountryId}','HKA',N'Haut-Katanga',1,'{now}',0);

            INSERT INTO [Ville] ([IdVille],[IdProvince],[CodeVille],[NomVille],[Actif],[DateCreation],[IsDeleted]) VALUES
            ('{GeographySeedData.KinshasaCityId}','{GeographySeedData.KinshasaProvinceId}','KIN',N'Kinshasa',1,'{now}',0),
            ('{GeographySeedData.MatadiCityId}','{GeographySeedData.KongoCentralProvinceId}','MAT',N'Matadi',1,'{now}',0),
            ('{GeographySeedData.LubumbashiCityId}','{GeographySeedData.HautKatangaProvinceId}','LUB',N'Lubumbashi',1,'{now}',0);

            INSERT INTO [Commune] ([IdCommune],[IdVille],[CodeCommune],[NomCommune],[Actif],[DateCreation],[IsDeleted]) VALUES
            ('{GeographySeedData.GombeCommuneId}','{GeographySeedData.KinshasaCityId}','GOM',N'Gombe',1,'{now}',0),
            ('{GeographySeedData.LimeteCommuneId}','{GeographySeedData.KinshasaCityId}','LIM',N'Limete',1,'{now}',0),
            ('{GeographySeedData.NgaliemaCommuneId}','{GeographySeedData.KinshasaCityId}','NGA',N'Ngaliema',1,'{now}',0),
            ('{GeographySeedData.MatadiCommuneId}','{GeographySeedData.MatadiCityId}','MAT',N'Matadi',1,'{now}',0),
            ('{GeographySeedData.LubumbashiCommuneId}','{GeographySeedData.LubumbashiCityId}','LUB',N'Lubumbashi',1,'{now}',0);
            """;
        await ExecuteAsync(connection, sql, cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqlConnection connection,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @table AND COLUMN_NAME = @column
            """;
        check.Parameters.AddWithValue("@table", tableName);
        check.Parameters.AddWithValue("@column", columnName);

        if (await check.ExecuteScalarAsync(cancellationToken) is not null)
        {
            return;
        }

        await ExecuteAsync(connection, alterSql, cancellationToken);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
