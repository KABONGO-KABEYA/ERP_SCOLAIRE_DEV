using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Applique le schéma sécurité Phase 0 (idempotent) sur la BD cloud
/// configurée dans ServeurDonneesCloud.txt — débloque le sync locale→cloud.
/// </summary>
var apiDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Debug", "net8.0"));

apiDir = Path.GetFullPath(apiDir);
Console.WriteLine($"API dir: {apiDir}");

var encryption = new EncryptionService();
var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, encryption);
var cloudConfig = cloudMgr.LoadConfiguration();
if (string.IsNullOrWhiteSpace(cloudConfig.Serveur) || string.IsNullOrWhiteSpace(cloudConfig.Base))
{
    Console.Error.WriteLine("ServeurDonneesCloud.txt manquant ou incomplet.");
    return 1;
}

var factory = new DatabaseConnectionFactory();
var cs = factory.BuildConnectionString(cloudConfig.ToDatabaseConfiguration());
Console.WriteLine($"Cloud: {cloudConfig.Serveur} / {cloudConfig.Base} (user={cloudConfig.Utilisateur})");
Console.WriteLine("Application schéma Phase 0...");

var init = new SecurityEnginePhase0SchemaInitializer(
    cs,
    NullLogger<SecurityEnginePhase0SchemaInitializer>.Instance);
await init.EnsureCreatedAsync();

Console.WriteLine("OK — schéma Phase 0 appliqué sur le cloud.");
return 0;
