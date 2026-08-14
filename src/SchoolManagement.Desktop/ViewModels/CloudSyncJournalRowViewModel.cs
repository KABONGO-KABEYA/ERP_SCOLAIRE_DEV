using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.CloudSync.DTOs;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.ViewModels;

public sealed class CloudSyncJournalRowViewModel
{
    public CloudSyncJournalRowViewModel(CloudSyncJournalLineDto source)
    {
        Source = source;
        var display = CloudSyncJournalElementsFormatter.Format(source);
        SyncElementsSummary = display.Summary;
        SyncElementsDetailLines = display.DetailLines;
        IsSummaryTruncated = display.IsTruncated;
        SyncElementsTooltip = display.IsTruncated || display.DetailLines.Count > 3
            ? null
            : string.Join(Environment.NewLine, display.DetailLines);
        ShowSyncElementsDetailCommand = new RelayCommand(ShowSyncElementsDetail);
    }

    public CloudSyncJournalLineDto Source { get; }

    public DateTime StartedAt => Source.StartedAt;

    public int DurationMs => Source.DurationMs;

    public bool Success => Source.Success;

    public int UnitsSucceeded => Source.UnitsSucceeded;

    public int UnitsFailed => Source.UnitsFailed;

    public string? ErrorSummary => Source.ErrorSummary;

    public string SyncElementsSummary { get; }

    public IReadOnlyList<string> SyncElementsDetailLines { get; }

    public bool IsSummaryTruncated { get; }

    public string? SyncElementsTooltip { get; }

    public ICommand ShowSyncElementsDetailCommand { get; }

    private void ShowSyncElementsDetail()
    {
        var window = new CloudSyncSyncElementsDetailWindow(
            StartedAt.ToLocalTime(),
            SyncElementsDetailLines)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }
}
