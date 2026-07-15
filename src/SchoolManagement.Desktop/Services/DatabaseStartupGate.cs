using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.Services;

/// <summary>Bloque le démarrage du Desktop tant que la connexion SQL n'est pas valide.</summary>
public static class DatabaseStartupGate
{
    public static async Task<bool> EnsureConnectedAsync()
    {
        var bootstrap = new DatabaseConnectionBootstrap(AppContext.BaseDirectory);

        while (true)
        {
            bootstrap.ConfigurationManager.EnsureDefaultFileExists();
            var (_, _, testResult) = await bootstrap.LoadValidateAndTestAsync().ConfigureAwait(true);

            if (testResult.IsSuccess)
            {
                return true;
            }

            var viewModel = new DatabaseServerConfigViewModel(bootstrap, testResult.Message);
            var window = new DatabaseServerConfigWindow(viewModel);
            if (window.ShowDialog() != true)
            {
                return false;
            }
        }
    }
}
