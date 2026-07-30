using System.Windows;
using SchoolManagement.Updates;
using WpfApp = System.Windows.Application;

namespace SchoolManagement.Desktop.Updates;

public partial class UpdateAvailableWindow : Window
{
    private readonly UpdateCheckOutcome _outcome;
    private readonly UpdateManager _updateManager;
    private CancellationTokenSource? _cts;
    private bool _allowClose;

    public UpdateAvailableWindow(UpdateCheckOutcome outcome, UpdateManager updateManager)
    {
        _outcome = outcome;
        _updateManager = updateManager;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var m = _outcome.Manifest!;
        TitleText.Text = _outcome.Availability == UpdateAvailability.Mandatory
            ? "Une nouvelle version est obligatoire pour continuer"
            : "Nouvelle version disponible";
        CurrentVersionText.Text = _outcome.CurrentVersion;
        LatestVersionText.Text = m.LatestVersion;
        NotesList.ItemsSource = m.ReleaseNotes;
        SizeText.Text = FormatSize(m.Size);
        LaterButton.Visibility = _outcome.Availability == UpdateAvailability.Mandatory
            ? Visibility.Collapsed
            : Visibility.Visible;
        CancelDownloadButton.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_outcome.Availability == UpdateAvailability.Mandatory && !_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    private async void UpdateButton_OnClick(object sender, RoutedEventArgs e)
    {
        var manifest = _outcome.Manifest!;
        _cts = new CancellationTokenSource();
        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        CancelDownloadButton.Visibility = _outcome.Availability == UpdateAvailability.Optional
            ? Visibility.Visible
            : Visibility.Collapsed;
        StatusText.Text = "Téléchargement…";

        var progress = new Progress<DownloadProgress>(p =>
        {
            ProgressBar.Value = p.Percent;
            SpeedText.Text = $"{FormatSize((long)p.BytesPerSecond)}/s";
            EtaText.Text = p.EstimatedRemaining is { } eta
                ? $"Restant : {eta:mm\\:ss}"
                : string.Empty;
            BytesText.Text = p.TotalBytes is { } total
                ? $"{FormatSize(p.BytesReceived)} / {FormatSize(total)}"
                : FormatSize(p.BytesReceived);
        });

        try
        {
            var path = await _updateManager.DownloadAndVerifyAsync(manifest, progress, _cts.Token);
            StatusText.Text = "Installation…";
            var exe = Environment.ProcessPath;
            _updateManager.LaunchDesktopInstaller(path, exe);
            _allowClose = true;
            WpfApp.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Téléchargement annulé.";
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message.Contains("invalide", StringComparison.OrdinalIgnoreCase)
                    ? "Le fichier téléchargé est invalide."
                    : ex.Message,
                "Mise à jour",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = _outcome.Availability != UpdateAvailability.Mandatory;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
        }
    }

    private void LaterButton_OnClick(object sender, RoutedEventArgs e)
    {
        var settings = _updateManager.Settings;
        _updateManager.SnoozeOptional(TimeSpan.FromHours(Math.Max(1, settings.CheckIntervalHours)));
        _allowClose = true;
        DialogResult = false;
        Close();
    }

    private void CancelDownloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private static string FormatSize(long? bytes)
    {
        if (bytes is null or < 0)
        {
            return "—";
        }

        double b = bytes.Value;
        string[] units = ["o", "Ko", "Mo", "Go"];
        var u = 0;
        while (b >= 1024 && u < units.Length - 1)
        {
            b /= 1024;
            u++;
        }

        return $"{b:0.##} {units[u]}";
    }
}
