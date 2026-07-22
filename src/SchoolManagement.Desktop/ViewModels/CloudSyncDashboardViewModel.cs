using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.CloudSync.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Tableau de bord synchronisation Local → Cloud (Paramètres).</summary>
public partial class CloudSyncDashboardViewModel : ViewModelBase
{
    private readonly ICloudSyncApiService _api;

    public CloudSyncDashboardViewModel(ICloudSyncApiService api)
    {
        _api = api;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _cloudConfigured;

    [ObservableProperty]
    private bool _cloudEnabled;

    [ObservableProperty]
    private bool _cloudReachable;

    [ObservableProperty]
    private string? _cloudServer;

    [ObservableProperty]
    private string _lastSuccessText = "—";

    [ObservableProperty]
    private string _lastAttemptText = "—";

    [ObservableProperty]
    private string? _lastMessage;

    [ObservableProperty]
    private int _pendingUnits;

    [ObservableProperty]
    private int _pendingCriticalUnits;

    [ObservableProperty]
    private int _failedUnits;

    [ObservableProperty]
    private int _deadLetterUnits;

    [ObservableProperty]
    private string _averageDurationText = "—";

    [ObservableProperty]
    private string _connectionLabel = "Non configuré";

    public ObservableCollection<CloudSyncJournalLineDto> RecentJournal { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var status = await _api.GetStatusAsync();
            ApplyStatus(status);
            StatusMessage = "État actualisé.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SynchronizeNowAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Synchronisation en cours…";
        try
        {
            var result = await _api.SynchronizeNowAsync(criticalOnly: false);
            StatusMessage = result.Message;
            var status = await _api.GetStatusAsync();
            ApplyStatus(status);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyStatus(CloudSyncStatusDto status)
    {
        CloudConfigured = status.CloudConfigured;
        CloudEnabled = status.CloudEnabled;
        CloudReachable = status.CloudReachable;
        CloudServer = status.CloudServer;
        LastSuccessText = FormatUtc(status.LastSuccessUtc);
        LastAttemptText = FormatUtc(status.LastAttemptUtc);
        LastMessage = status.LastMessage;
        PendingUnits = status.PendingUnits;
        PendingCriticalUnits = status.PendingCriticalUnits;
        FailedUnits = status.FailedUnits;
        DeadLetterUnits = status.DeadLetterUnits;
        AverageDurationText = status.AverageDurationMs is null
            ? "—"
            : $"{status.AverageDurationMs:0} ms";

        ConnectionLabel = !status.CloudConfigured
            ? "Non configuré (ServeurDonneesCloud.txt absent)"
            : !status.CloudEnabled
                ? "Désactivé (ACTIF=0)"
                : status.CloudReachable
                    ? $"Connecté — {status.CloudServer}"
                    : $"Injoignable — {status.CloudServer}";

        RecentJournal.Clear();
        foreach (var line in status.RecentJournal)
        {
            RecentJournal.Add(line);
        }
    }

    private static string FormatUtc(DateTime? utc) =>
        utc is null ? "—" : utc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
}
