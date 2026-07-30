using System.Windows;
using Microsoft.Extensions.Logging;
using SchoolManagement.Updates;
using WpfApp = System.Windows.Application;

namespace SchoolManagement.Desktop.Updates;

/// <summary>Planifie check au démarrage et toutes les N heures (défaut 6 h).</summary>
public sealed class DesktopUpdateCoordinator
{
    private readonly UpdateManager _updateManager;
    private readonly UpdateSettingsStore _settingsStore;
    private readonly ILogger<DesktopUpdateCoordinator>? _logger;
    private System.Windows.Threading.DispatcherTimer? _timer;
    private int _running;

    public DesktopUpdateCoordinator(
        UpdateManager updateManager,
        UpdateSettingsStore settingsStore,
        ILogger<DesktopUpdateCoordinator>? logger = null)
    {
        _updateManager = updateManager;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public void Start()
    {
        var settings = _settingsStore.Load();
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromHours(Math.Max(1, settings.CheckIntervalHours))
        };
        _timer.Tick += async (_, _) => await CheckAndPromptAsync(forceOptional: false);
        _timer.Start();
        _ = CheckAndPromptAsync(forceOptional: false);
    }

    public async Task CheckAndPromptAsync(bool forceOptional)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        try
        {
            if (forceOptional)
            {
                var settings = _settingsStore.Load();
                settings.SnoozeUntilUtc = null;
                _settingsStore.Save(settings);
            }

            var outcome = await _updateManager.CheckSilentlyAsync(CancellationToken.None);
            if (outcome?.Manifest is null)
            {
                if (forceOptional)
                {
                    await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show(
                            "Aucune mise à jour disponible (ou serveur injoignable).",
                            "Mises à jour",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information));
                }

                return;
            }

            await WpfApp.Current.Dispatcher.InvokeAsync(() =>
            {
                var window = new UpdateAvailableWindow(outcome, _updateManager)
                {
                    Owner = WpfApp.Current.MainWindow
                };
                window.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Vérification mise à jour silencieuse échouée.");
            if (forceOptional)
            {
                await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(
                        "Impossible de vérifier les mises à jour pour le moment.",
                        "Mises à jour",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
