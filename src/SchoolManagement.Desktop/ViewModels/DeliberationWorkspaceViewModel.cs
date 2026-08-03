using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Conseil de classe — une seule page : filtres, grille, observations, validation.
/// </summary>
public partial class DeliberationWorkspaceViewModel : ViewModelBase
{
    private readonly ISchoolApiService _schoolApi;
    private readonly IGradeApiService _gradeApi;
    private SchoolLookupsDto? _lookups;
    private PedagogicalSheetContextDto? _periodContext;
    private bool _filtersReady;
    private bool _suppressReload;

    public DeliberationWorkspaceViewModel(
        ISchoolApiService schoolApi,
        IGradeApiService gradeApi,
        DeliberationViewModel session)
    {
        _schoolApi = schoolApi;
        _gradeApi = gradeApi;
        Session = session;
        Session.ShowLocalFilters = false;
        Session.SheetChanged += OnSessionChanged;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
    }

    public DeliberationViewModel Session { get; }

    public ObservableCollection<ClassRoomLookupDto> ClassRooms { get; } = [];
    public ObservableCollection<PedagogicalSheetPeriodOptionDto> PeriodOptions { get; } = [];
    public ObservableCollection<DeliberationHistoryItemVm> HistoryItems { get; } = [];

    [ObservableProperty] private AcademicYearDto? _selectedYear;
    [ObservableProperty] private ClassRoomLookupDto? _selectedClassRoom;
    [ObservableProperty] private PedagogicalSheetPeriodOptionDto? _selectedPeriod;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _classDisplayName = "—";
    [ObservableProperty] private string _periodLabel = "—";
    [ObservableProperty] private string _validationStatusLabel = "Non validé";
    [ObservableProperty] private string _modeBannerText = string.Empty;

    public bool CanValidateClass => Session.CanValidateClass && Session.Rows.Count > 0;

    public bool CanCancelValidation => Session.CanCancelValidation;

    public bool ShowDecisionColumn => Session.ShowDecisionColumn;

    public bool CanEditSession => !Session.IsSessionReadOnly;

    public async Task EnsureLoadedAsync()
    {
        if (_lookups is not null)
        {
            return;
        }

        await LoadFiltersAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await ReloadAllAsync();

    [RelayCommand]
    private async Task ValidateClassAsync()
    {
        if (!CanValidateClass)
        {
            StatusMessage = "Validation impossible : vérifiez la conduite" +
                            (Session.IsYearEnd ? " et les décisions finales" : "") + ".";
            return;
        }

        if (Session.ValidateClassCommand.CanExecute(null))
        {
            Session.ValidateClassCommand.Execute(null);
            await Task.Delay(100);
            SyncFromSession();
            StatusMessage = Session.StatusMessage;
        }
    }

    [RelayCommand]
    private async Task CancelClassValidationAsync()
    {
        if (!CanCancelValidation)
        {
            StatusMessage = "Annulation impossible : période clôturée ou classe non validée.";
            return;
        }

        if (Session.CancelClassValidationCommand.CanExecute(null))
        {
            Session.CancelClassValidationCommand.Execute(null);
            await Task.Delay(100);
            SyncFromSession();
            StatusMessage = Session.StatusMessage;
        }
    }

    [RelayCommand]
    private void OpenSelectedDecision()
    {
        if (Session.SelectedRow is null)
        {
            StatusMessage = "Sélectionnez un élève.";
            return;
        }

        if (Session.OpenDecisionCommand.CanExecute(Session.SelectedRow))
        {
            Session.OpenDecisionCommand.Execute(Session.SelectedRow);
        }
    }

    [RelayCommand]
    private void OpenBonusForSelected()
    {
        if (Session.SelectedRow is null)
        {
            StatusMessage = "Sélectionnez un élève.";
            return;
        }

        if (Session.OpenBonusCommand.CanExecute(null))
        {
            Session.OpenBonusCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void OpenHistory()
    {
        RebuildHistory();
        var window = new Views.DeliberationHistoryWindow
        {
            Owner = System.Windows.Application.Current.MainWindow,
            DataContext = this
        };
        window.ShowDialog();
    }

    partial void OnSelectedClassRoomChanged(ClassRoomLookupDto? value)
    {
        if (!_filtersReady || _suppressReload)
        {
            return;
        }

        _ = ReloadPeriodsAndAllAsync();
    }

    partial void OnSelectedPeriodChanged(PedagogicalSheetPeriodOptionDto? value)
    {
        if (!_filtersReady || _suppressReload)
        {
            return;
        }

        _ = ReloadAllAsync();
    }

    private void OnSessionChanged() => SyncFromSession();

    private void SyncFromSession()
    {
        ClassDisplayName = Session.ClassDisplayName;
        PeriodLabel = Session.PeriodLabel;
        ValidationStatusLabel = Session.ValidationStatusLabel;
        ModeBannerText = Session.PeriodModeLabel;
        OnPropertyChanged(nameof(CanValidateClass));
        OnPropertyChanged(nameof(CanCancelValidation));
        OnPropertyChanged(nameof(ShowDecisionColumn));
        OnPropertyChanged(nameof(CanEditSession));
        RebuildHistory();
    }

    private void RebuildHistory()
    {
        HistoryItems.Clear();
        foreach (var row in Session.Rows.Where(r => r.FinalDecisionLabel is not ("—" or "")))
        {
            HistoryItems.Add(new DeliberationHistoryItemVm(
                "—",
                "Conseil",
                $"Décision : {row.FinalDecisionLabel}",
                row.FullName,
                "Décision"));
        }

        if (!string.IsNullOrWhiteSpace(Session.ValidatedAtDisplay) && Session.ValidatedAtDisplay != "—")
        {
            HistoryItems.Add(new DeliberationHistoryItemVm(
                Session.ValidatedAtDisplay,
                Session.ValidatedByDisplay,
                "Validation de la classe",
                string.Empty,
                "Validation"));
        }

        if (Session.PvExists)
        {
            HistoryItems.Add(new DeliberationHistoryItemVm(
                Session.PvRecordedAtDisplay,
                Session.PvRecordedByDisplay,
                "Procès-verbal généré",
                string.Empty,
                "PV"));
        }
    }

    private async Task LoadFiltersAsync()
    {
        IsBusy = true;
        try
        {
            _lookups = await _schoolApi.GetLookupsAsync();
            _filtersReady = false;
            SyncYearFromTitleBar();
            RefreshClassRooms();
            _filtersReady = true;
            await ReloadPeriodsAndAllAsync();
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

    private void SyncYearFromTitleBar()
    {
        var bridgeYear = AcademicYearRefreshBridge.SelectedYear;
        SelectedYear = bridgeYear
            ?? _lookups?.AcademicYears.FirstOrDefault(y => y.IsCurrent)
            ?? _lookups?.AcademicYears.OrderByDescending(y => y.Label).FirstOrDefault();
    }

    private void RefreshClassRooms()
    {
        ClassRooms.Clear();
        if (_lookups is null || SelectedYear is null)
        {
            SelectedClassRoom = null;
            return;
        }

        foreach (var room in _lookups.ClassRooms
                     .Where(c => c.AcademicYearId == SelectedYear.Id)
                     .OrderBy(c => c.Name))
        {
            ClassRooms.Add(room);
        }

        SelectedClassRoom = ClassRooms.FirstOrDefault(c => c.Id == SelectedClassRoom?.Id)
            ?? ClassRooms.FirstOrDefault();
    }

    private async Task ReloadPeriodsAndAllAsync()
    {
        if (SelectedYear is null || SelectedClassRoom is null)
        {
            PeriodOptions.Clear();
            SelectedPeriod = null;
            return;
        }

        IsBusy = true;
        try
        {
            _periodContext = await _gradeApi.GetPedagogicalSheetContextAsync(
                SelectedYear.Id, SelectedClassRoom.Id);
            ClassDisplayName = _periodContext.ClassDisplayName;

            _suppressReload = true;
            PeriodOptions.Clear();
            foreach (var option in _periodContext.SubPeriods
                         .OrderBy(o => o.OrderIndex).ThenBy(o => o.Name))
            {
                PeriodOptions.Add(option);
            }

            SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == SelectedPeriod?.Id)
                ?? PeriodOptions.FirstOrDefault(p => p.Id == _periodContext.DefaultSubPeriodId)
                ?? PeriodOptions.FirstOrDefault();
            _suppressReload = false;

            await ReloadAllAsync();
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

    private async Task ReloadAllAsync()
    {
        if (SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await Session.SyncSelectionFromParentAsync(SelectedYear, SelectedClassRoom, SelectedPeriod);
        }
        finally
        {
            IsBusy = false;
        }

        SyncFromSession();
    }

    private void OnGlobalAcademicYearChanged()
    {
        if (_lookups is null)
        {
            return;
        }

        _suppressReload = true;
        SyncYearFromTitleBar();
        RefreshClassRooms();
        _suppressReload = false;
        _ = ReloadPeriodsAndAllAsync();
    }
}

public sealed record DeliberationHistoryItemVm(
    string WhenDisplay,
    string UserName,
    string Action,
    string Observation,
    string Category);
