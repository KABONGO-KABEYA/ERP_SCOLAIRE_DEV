using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class SecurityAuditViewModel : ViewModelBase
{
    private readonly ISecurityAdminApiService _security;

    public SecurityAuditViewModel(ISecurityAdminApiService security)
    {
        _security = security;
        FilterFromUtc = DateTime.UtcNow.Date.AddDays(-7);
        FilterToUtc = DateTime.UtcNow.Date.AddDays(1);
        _ = SearchAsync();
    }

    public ObservableCollection<SecurityAuditLogDto> Entries { get; } = [];

    [ObservableProperty] private SecurityAuditLogDto? _selectedEntry;
    [ObservableProperty] private DateTime? _filterFromUtc;
    [ObservableProperty] private DateTime? _filterToUtc;
    [ObservableProperty] private string? _filterActionType;
    [ObservableProperty] private string? _filterTargetUserName;
    [ObservableProperty] private string? _detailSummary;
    [ObservableProperty] private string? _detailJson;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    partial void OnSelectedEntryChanged(SecurityAuditLogDto? value) => _ = LoadDetailAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            var query = new SecurityAuditQuery(
                FromUtc: FilterFromUtc,
                ToUtc: FilterToUtc,
                ActionType: string.IsNullOrWhiteSpace(FilterActionType) ? null : FilterActionType.Trim(),
                TargetUserName: string.IsNullOrWhiteSpace(FilterTargetUserName) ? null : FilterTargetUserName.Trim(),
                Take: 200);

            var list = await _security.QueryAuditAsync(query);
            Entries.Clear();
            foreach (var e in list) Entries.Add(e);
            SelectedEntry = Entries.FirstOrDefault();
            StatusMessage = $"{Entries.Count} entrée(s).";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadDetailAsync()
    {
        DetailSummary = null;
        DetailJson = null;
        if (SelectedEntry is null) return;

        IsBusy = true;
        try
        {
            var detail = await _security.GetAuditAsync(SelectedEntry.Id);
            DetailSummary = detail.Summary;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(detail.OldValuesJson))
                parts.Add($"Ancien : {detail.OldValuesJson}");
            if (!string.IsNullOrWhiteSpace(detail.NewValuesJson))
                parts.Add($"Nouveau : {detail.NewValuesJson}");
            DetailJson = parts.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, parts) : "(aucune valeur JSON)";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
