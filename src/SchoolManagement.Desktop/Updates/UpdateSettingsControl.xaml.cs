using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Updates.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Updates;

namespace SchoolManagement.Desktop.Updates;

public partial class UpdateSettingsControl : UserControl
{
    private UpdateSettingsStore? _settingsStore;
    private UpdateHistoryStore? _historyStore;
    private DesktopUpdateCoordinator? _coordinator;
    private IUpdateAdminApiService? _updateAdminApi;
    private UpdateSettings _settings = new();

    public UpdateSettingsControl()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (App.Services is null)
            {
                return;
            }

            _settingsStore = App.Services.GetRequiredService<UpdateSettingsStore>();
            _historyStore = App.Services.GetRequiredService<UpdateHistoryStore>();
            _coordinator = App.Services.GetRequiredService<DesktopUpdateCoordinator>();
            _updateAdminApi = App.Services.GetRequiredService<IUpdateAdminApiService>();
            Reload();
            await RefreshVersionsAsync();
        };
    }

    private void Reload()
    {
        if (_settingsStore is null || _historyStore is null)
        {
            return;
        }

        _settings = _settingsStore.Load();
        AutoCheckBox.IsChecked = _settings.AutoCheckEnabled;
        AutoDownloadBox.IsChecked = _settings.AutoDownloadOptional;
        AutoInstallBox.IsChecked = _settings.AutoInstallOnNextRestart;
        CurrentVersionText.Text = _settings.CurrentVersion;
        LastCheckText.Text = FormatUtc(_settings.LastCheckUtc);
        LastUpdateText.Text = FormatUtc(_settings.LastUpdateUtc);
        HistoryList.ItemsSource = _historyStore.Load().Take(20)
            .Select(h => $"{h.Utc:g} — {h.Result} — {h.VersionFound} {h.Detail}".Trim())
            .ToList();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (_settingsStore is null)
        {
            return;
        }

        _settings.AutoCheckEnabled = AutoCheckBox.IsChecked == true;
        _settings.AutoDownloadOptional = AutoDownloadBox.IsChecked == true;
        _settings.AutoInstallOnNextRestart = AutoInstallBox.IsChecked == true;
        _settingsStore.Save(_settings);
        MessageBox.Show("Préférences de mise à jour enregistrées.", "Mises à jour",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void CheckNow_OnClick(object sender, RoutedEventArgs e)
    {
        if (_coordinator is null)
        {
            return;
        }

        await _coordinator.CheckAndPromptAsync(forceOptional: true);
        Reload();
    }

    private async void Publish_OnClick(object sender, RoutedEventArgs e)
    {
        if (_updateAdminApi is null)
        {
            return;
        }

        try
        {
            long? size = null;
            if (long.TryParse(PubSizeBox.Text?.Trim(), out var parsedSize))
            {
                size = parsedSize;
            }

            var notes = (PubNotesBox.Text ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var request = new PublishApplicationVersionRequest
            {
                Version = PubVersionBox.Text?.Trim() ?? string.Empty,
                MinimumVersion = PubMinVersionBox.Text?.Trim() ?? "1.0.0",
                Mandatory = PubMandatoryBox.IsChecked == true,
                Active = PubActiveBox.IsChecked == true,
                DeactivateOthers = true,
                DesktopUrl = PubDesktopUrlBox.Text,
                MobileUrl = PubMobileUrlBox.Text,
                Sha256 = PubShaBox.Text,
                Size = size,
                ReleaseNotes = notes,
                SchemaVersion = 1
            };

            var published = await _updateAdminApi.PublishAsync(request);
            MessageBox.Show(
                $"Version {published.Version} enregistrée (Active={published.Active}).",
                "Publication",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await RefreshVersionsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Publication", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RefreshVersions_OnClick(object sender, RoutedEventArgs e) =>
        await RefreshVersionsAsync();

    private async void ActivateVersion_OnClick(object sender, RoutedEventArgs e)
    {
        if (_updateAdminApi is null || sender is not Button { Tag: Guid id })
        {
            return;
        }

        try
        {
            await _updateAdminApi.SetActiveAsync(id, active: true, deactivateOthers: true);
            await RefreshVersionsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Activation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshVersionsAsync()
    {
        if (_updateAdminApi is null)
        {
            return;
        }

        try
        {
            var items = await _updateAdminApi.ListVersionsAsync();
            VersionsGrid.ItemsSource = items;
        }
        catch
        {
            // Pas admin / API down : section publication reste utilisable après login admin.
            VersionsGrid.ItemsSource = null;
        }
    }

    private static string FormatUtc(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return "Jamais";
        }

        return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime().ToString("g")
            : iso;
    }
}
