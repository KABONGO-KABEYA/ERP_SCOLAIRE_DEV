using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Copie complète Local → Cloud (legacy upsert) via ServeurDonneesCloud.txt.
/// Usage : dotnet run --project tools/RunCloudFullSync [chemin-api-bin]
/// </summary>
var apiDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Release", "net8.0"));

Console.WriteLine($"API dir: {apiDir}");

var encryption = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var localMgr = new DatabaseConfigurationManager(apiDir, encryption);
var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, encryption);

var localConfig = localMgr.LoadConfiguration();
var cloudConfig = cloudMgr.LoadConfiguration();

Console.WriteLine($"Local  : {localConfig.Serveur}/{localConfig.Base}");
Console.WriteLine($"Cloud  : {cloudConfig.Serveur}/{cloudConfig.Base} (ACTIF={cloudConfig.Actif})");

if (!cloudConfig.Actif)
{
    Console.Error.WriteLine("ACTIF=0 — activez la sync cloud ou passez ACTIF=1 temporairement.");
    return 2;
}

var localCs = factory.BuildConnectionString(localConfig);

var services = new ServiceCollection();
services.AddLogging(b => { b.AddConsole(); b.SetMinimumLevel(LogLevel.Information); });
services.AddSingleton<IEncryptionService>(encryption);
services.AddSingleton(factory);
services.AddSingleton(cloudMgr);
services.AddDbContext<SchoolDbContext>(opts =>
    opts.UseSqlServer(localCs, sql => sql.CommandTimeout(120)));
services.AddScoped<ICloudDatabaseSyncService, CloudDatabaseSyncService>();

await using var provider = services.BuildServiceProvider();
var sync = provider.GetRequiredService<ICloudDatabaseSyncService>();

Console.WriteLine("Démarrage copie complète Local → Cloud...");
var result = await sync.TrySyncAsync();

if (result.Skipped)
{
    Console.WriteLine($"Ignoré : {result.Message}");
    return 3;
}

if (!result.Success)
{
    Console.Error.WriteLine($"Échec : {result.Message}");
    return 1;
}

Console.WriteLine($"OK : {result.Message}");
return 0;
