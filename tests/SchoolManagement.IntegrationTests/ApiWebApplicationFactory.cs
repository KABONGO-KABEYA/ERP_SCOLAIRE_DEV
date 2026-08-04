using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Hôte API pour tests d'intégration (Development + dossier fichiers temporaire).
/// SQL : chaîne d'environnement ou fichiers à côté de l'exe API (ServeurDonnees.txt).
/// </summary>
public class ApiWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _fileStorageRoot =
        Path.Combine(Path.GetTempPath(), "erp-integ-files-" + Guid.NewGuid().ToString("N"));

    private bool _disposed;

    public ApiWebApplicationFactory()
    {
        Directory.CreateDirectory(_fileStorageRoot);
        Environment.SetEnvironmentVariable("FILE_STORAGE_ROOT", _fileStorageRoot);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                if (Directory.Exists(_fileStorageRoot))
                {
                    Directory.Delete(_fileStorageRoot, recursive: true);
                }
            }
            catch
            {
                // ignore
            }
        }

        base.Dispose(disposing);
    }
}
