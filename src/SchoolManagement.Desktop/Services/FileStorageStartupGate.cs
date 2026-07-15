using SchoolManagement.Application.Configuration.FileStorage;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.Services;

/// <summary>Bloque le démarrage tant que le dossier partagé Dossier_Elève n'est pas accessible.</summary>
public static class FileStorageStartupGate
{
    public static bool EnsureConfigured()
    {
        var configurationManager = new FileStorageConfigurationManager(AppContext.BaseDirectory);
        var pathTester = new FileStoragePathTester();

        while (true)
        {
            configurationManager.EnsureDefaultFileExists();
            var configuration = configurationManager.LoadConfiguration();

            if (configurationManager.IsConfigured())
            {
                var configuredTest = pathTester.TestConfiguration(
                    configuration,
                    AppContext.BaseDirectory,
                    requireWriteAccess: false);
                if (configuredTest.IsSuccess)
                {
                    return true;
                }

                var failedViewModel = new FileStorageConfigViewModel(
                    configurationManager, pathTester, configuredTest.Message);
                var failedWindow = new FileStorageConfigWindow(failedViewModel);
                if (failedWindow.ShowDialog() != true)
                {
                    return false;
                }

                continue;
            }

            var viewModel = new FileStorageConfigViewModel(
                configurationManager,
                pathTester,
                "Configurez le dossier partagé Dossier_Elève pour stocker les photos et documents des élèves.");
            var window = new FileStorageConfigWindow(viewModel);
            if (window.ShowDialog() != true)
            {
                return false;
            }
        }
    }
}
