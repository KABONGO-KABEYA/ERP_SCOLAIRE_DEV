using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Infrastructure.Persistence;

static async Task PrintSchemaAsync(string label, string connectionString)
{
    Console.WriteLine($"=== {label} ===");
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = """
        SELECT t.name AS TableName,
               CASE WHEN COL_LENGTH(t.name, 'SchoolId') IS NULL THEN 'MISSING' ELSE 'EXISTS' END AS SchoolIdStatus
        FROM sys.tables t
        WHERE t.name IN ('DisciplineRecords', 'MeritRecords', 'PeriodResults')
        ORDER BY t.name;

        SELECT i.name AS IndexName, t.name AS TableName
        FROM sys.indexes i
        INNER JOIN sys.tables t ON t.object_id = i.object_id
        WHERE t.name IN ('DisciplineRecords', 'MeritRecords')
          AND i.name LIKE '%SchoolId%'
        ORDER BY t.name, i.name;
        """;

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"  {reader.GetString(0)}: SchoolId={reader.GetString(1)}");
    }

    if (await reader.NextResultAsync())
    {
        while (await reader.ReadAsync())
        {
            Console.WriteLine($"  Index {reader.GetString(0)} on {reader.GetString(1)}");
        }
    }
}

var apiDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Release", "net8.0"));

apiDir = Path.GetFullPath(apiDir);
Console.WriteLine($"API dir: {apiDir}");

var localMgr = new DatabaseConfigurationManager(apiDir, new EncryptionService());
var localConfig = localMgr.LoadConfiguration();
var factory = new DatabaseConnectionFactory();
var localCs = factory.BuildConnectionString(localConfig);

await PrintSchemaAsync("LOCAL (avant)", localCs);

var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, new EncryptionService());
var cloudConfig = cloudMgr.LoadConfiguration();
if (!string.IsNullOrWhiteSpace(cloudConfig.Serveur) && !string.IsNullOrWhiteSpace(cloudConfig.Base))
{
    var cloudCs = factory.BuildConnectionString(cloudConfig.ToDatabaseConfiguration());
    await PrintSchemaAsync("CLOUD (lecture seule)", cloudCs);
}
else
{
    Console.WriteLine("CLOUD: ServeurDonneesCloud.txt manquant ou incomplet — vérification ignorée.");
}

var applyLocal = args.Any(a => a.Equals("--apply-local", StringComparison.OrdinalIgnoreCase));
if (applyLocal)
{
    Console.WriteLine("Application schéma local (DisciplineRecords/MeritRecords)...");
    var init = new DisciplineMeritSchoolIdSchemaInitializer(
        localCs,
        NullLogger<DisciplineMeritSchoolIdSchemaInitializer>.Instance);
    await init.EnsureUpdatedAsync();
    await PrintSchemaAsync("LOCAL (après)", localCs);
    Console.WriteLine("OK — schéma local appliqué.");
}

return 0;
