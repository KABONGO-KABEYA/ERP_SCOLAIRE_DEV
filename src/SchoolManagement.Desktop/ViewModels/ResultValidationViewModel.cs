using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.ResultValidation.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class ResultValidationViewModel : ViewModelBase
{
    private readonly ISchoolApiService _schoolApi;
    private readonly IGradeApiService _gradeApi;
    private readonly IResultValidationApiService _validationApi;

    private SchoolLookupsDto? _lookups;
    private PedagogicalSheetContextDto? _periodContext;
    private bool _filtersReady;
    private bool _suppressReload;
    private ResultValidationStatus? _loadedStatus;
    private ResultValidationSheetDto? _lastSheet;

    public ResultValidationViewModel(
        ISchoolApiService schoolApi,
        IGradeApiService gradeApi,
        IResultValidationApiService validationApi)
    {
        _schoolApi = schoolApi;
        _gradeApi = gradeApi;
        _validationApi = validationApi;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        _ = EnsureLoadedAsync();
    }

    public ObservableCollection<ClassRoomLookupDto> ClassRooms { get; } = [];
    public ObservableCollection<PedagogicalSheetPeriodOptionDto> PeriodOptions { get; } = [];
    public ObservableCollection<ResultValidationRowVm> Rows { get; } = [];
    public ObservableCollection<ResultValidationEventVm> Events { get; } = [];
    public ObservableCollection<string> ReadinessIssues { get; } = [];

    [ObservableProperty] private AcademicYearDto? _selectedYear;
    [ObservableProperty] private ClassRoomLookupDto? _selectedClassRoom;
    [ObservableProperty] private PedagogicalSheetPeriodOptionDto? _selectedPeriod;
    [ObservableProperty] private ResultValidationStatus _statusFilter = ResultValidationStatus.NonValide;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _classDisplayName = "—";
    [ObservableProperty] private string _periodLabel = "—";
    [ObservableProperty] private string _statusLabel = "Non validé";
    [ObservableProperty] private string _summaryStudentCount = "0";
    [ObservableProperty] private string _summaryAdmitted = "0";
    [ObservableProperty] private string _summaryDeferred = "0";
    [ObservableProperty] private string _summaryExcluded = "0";
    [ObservableProperty] private string _summaryClassAverage = "—";
    [ObservableProperty] private string _summarySuccessRate = "—";
    [ObservableProperty] private string _summaryCalculatedAt = "—";
    [ObservableProperty] private string _summaryLastUpdated = "—";
    [ObservableProperty] private bool _canValidate;
    [ObservableProperty] private bool _canCancelValidation;
    [ObservableProperty] private bool _canLock;
    [ObservableProperty] private bool _canUnlock;
    [ObservableProperty] private bool _hasReadinessIssues;
    [ObservableProperty] private bool _showLocalFilters = true;
    [ObservableProperty] private bool _showHeaderKpi = true;
    /// <summary>Masqué dans l'espace Conseil : la validation passe par « Valider la classe ».</summary>
    [ObservableProperty] private bool _showLifecycleActions = true;
    [ObservableProperty] private string? _observations;

    /// <summary>Statut réel chargé (indépendant du filtre d'affichage).</summary>
    public ResultValidationStatus? CurrentStatus => _loadedStatus;

    public event Action? SheetChanged;

    public bool IsStatusNonValide
    {
        get => StatusFilter == ResultValidationStatus.NonValide;
        set { if (value) StatusFilter = ResultValidationStatus.NonValide; }
    }

    public bool IsStatusValide
    {
        get => StatusFilter == ResultValidationStatus.Valide;
        set { if (value) StatusFilter = ResultValidationStatus.Valide; }
    }

    public bool IsStatusVerrouille
    {
        get => StatusFilter == ResultValidationStatus.Verrouille;
        set { if (value) StatusFilter = ResultValidationStatus.Verrouille; }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_lookups is not null)
        {
            return;
        }

        await LoadFiltersAsync();
    }

    /// <summary>Alignement des filtres depuis l'espace Délibération unifié.</summary>
    public async Task SyncSelectionFromParentAsync(
        AcademicYearDto? year,
        ClassRoomLookupDto? classRoom,
        PedagogicalSheetPeriodOptionDto? period)
    {
        await EnsureLoadedAsync();
        _suppressReload = true;
        try
        {
            if (year is not null && SelectedYear?.Id != year.Id)
            {
                SelectedYear = year;
                RefreshClassRooms();
            }

            if (classRoom is not null)
            {
                SelectedClassRoom = ClassRooms.FirstOrDefault(c => c.Id == classRoom.Id) ?? classRoom;
            }

            if (period is not null && SelectedClassRoom is not null)
            {
                if (_periodContext is null || PeriodOptions.All(p => p.Id != period.Id))
                {
                    _suppressReload = false;
                    await LoadPeriodContextAndSheetAsync();
                    _suppressReload = true;
                    SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == period.Id)
                        ?? PeriodOptions.FirstOrDefault();
                }
                else
                {
                    SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == period.Id) ?? period;
                }
            }
        }
        finally
        {
            _suppressReload = false;
        }

        await LoadSheetAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadSheetAsync();

    [RelayCommand]
    private async Task ValidateAsync() => await RunActionAsync(
        "Valider les résultats de cette classe ?",
        req => _validationApi.ValidateAsync(req));

    [RelayCommand]
    private async Task CancelValidationAsync() => await RunActionAsync(
        "Annuler la validation ?",
        req => _validationApi.CancelAsync(req));

    [RelayCommand]
    private async Task LockAsync() => await RunActionAsync(
        "Verrouiller définitivement ces résultats ?",
        req => _validationApi.LockAsync(req));

    [RelayCommand]
    private async Task UnlockAsync() => await RunActionAsync(
        "Déverrouiller ces résultats (admin) ?",
        req => _validationApi.UnlockAsync(req));

    [RelayCommand]
    private void ExportExcel()
    {
        if (Rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV Excel (*.csv)|*.csv",
            FileName = $"Validation_{ClassDisplayName}_{PeriodLabel}.csv".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Rang;Matricule;Nom complet;Moyenne générale;Pourcentage;Mention;Décision;Statut");
        foreach (var row in Rows)
        {
            sb.Append(EscapeCsv(row.RankDisplay)).Append(';')
                .Append(EscapeCsv(row.RegistrationNumber)).Append(';')
                .Append(EscapeCsv(row.FullName)).Append(';')
                .Append(EscapeCsv(row.AverageDisplay)).Append(';')
                .Append(EscapeCsv(row.PercentageDisplay)).Append(';')
                .Append(EscapeCsv(row.Mention ?? string.Empty)).Append(';')
                .Append(EscapeCsv(row.DecisionLabel)).Append(';')
                .Append(EscapeCsv(row.ValidationStatusLabel))
                .AppendLine();
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        StatusMessage = "Export Excel (CSV) terminé.";
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (Rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "HTML (*.html)|*.html",
            FileName = $"Validation_{ClassDisplayName}_{PeriodLabel}.html".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        var html = BuildHtmlDocument();
        File.WriteAllText(dialog.FileName, html, Encoding.UTF8);
        StatusMessage = "Export PDF (HTML) terminé.";
    }

    [RelayCommand]
    private void Print()
    {
        if (Rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à imprimer.";
            return;
        }

        try
        {
            var document = BuildPrintDocument();
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            printDialog.PrintDocument(
                ((IDocumentPaginatorSource)document).DocumentPaginator,
                "Validation des résultats");
            StatusMessage = "Impression envoyée.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedClassRoomChanged(ClassRoomLookupDto? value)
    {
        if (!_filtersReady || _suppressReload)
        {
            return;
        }

        _ = LoadPeriodContextAndSheetAsync();
    }

    partial void OnSelectedPeriodChanged(PedagogicalSheetPeriodOptionDto? value)
    {
        if (!_filtersReady || _suppressReload)
        {
            return;
        }

        _ = LoadSheetAsync();
    }

    partial void OnStatusFilterChanged(ResultValidationStatus value)
    {
        OnPropertyChanged(nameof(IsStatusNonValide));
        OnPropertyChanged(nameof(IsStatusValide));
        OnPropertyChanged(nameof(IsStatusVerrouille));
        if (_lastSheet is not null)
        {
            ApplySheet(_lastSheet);
        }
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
        _ = LoadPeriodContextAndSheetAsync();
    }

    private async Task LoadFiltersAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            _lookups = await _schoolApi.GetLookupsAsync();
            _filtersReady = false;
            SyncYearFromTitleBar();
            RefreshClassRooms();
            _filtersReady = true;
            await LoadPeriodContextAndSheetAsync();
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
        if (bridgeYear is not null)
        {
            SelectedYear = bridgeYear;
            return;
        }

        SelectedYear = _lookups?.AcademicYears.FirstOrDefault(y => y.IsCurrent)
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

    private async Task LoadPeriodContextAndSheetAsync()
    {
        if (SelectedYear is null || SelectedClassRoom is null)
        {
            ClearSheet();
            StatusMessage = SelectedYear is null
                ? "Aucune année scolaire sélectionnée dans la barre de titre."
                : "Sélectionnez une classe.";
            return;
        }

        IsBusy = true;
        try
        {
            _periodContext = await _gradeApi.GetPedagogicalSheetContextAsync(
                SelectedYear.Id, SelectedClassRoom.Id);
            ClassDisplayName = _periodContext.ClassDisplayName;
            RebuildPeriodOptions();
            await LoadSheetAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClearSheet();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildPeriodOptions()
    {
        _suppressReload = true;
        try
        {
            PeriodOptions.Clear();
            if (_periodContext is null)
            {
                SelectedPeriod = null;
                return;
            }

            foreach (var option in _periodContext.SubPeriods
                         .OrderBy(o => o.OrderIndex).ThenBy(o => o.Name))
            {
                PeriodOptions.Add(option);
            }

            SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == _periodContext.DefaultSubPeriodId)
                ?? PeriodOptions.FirstOrDefault();
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private async Task LoadSheetAsync()
    {
        if (SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            ClearSheet();
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var sheet = await _validationApi.GetSheetAsync(
                SelectedYear.Id, SelectedClassRoom.Id, SelectedPeriod.Id);
            ApplySheet(sheet);
            SheetChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClearSheet();
            SheetChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplySheet(ResultValidationSheetDto sheet)
    {
        _lastSheet = sheet;
        _loadedStatus = sheet.Status;
        ClassDisplayName = sheet.ClassDisplayName;
        PeriodLabel = sheet.PeriodLabel;
        StatusLabel = sheet.StatusLabel;
        SummaryStudentCount = sheet.Summary.StudentCount.ToString(CultureInfo.InvariantCulture);
        SummaryAdmitted = sheet.Summary.AdmittedCount.ToString(CultureInfo.InvariantCulture);
        SummaryDeferred = sheet.Summary.DeferredCount.ToString(CultureInfo.InvariantCulture);
        SummaryExcluded = sheet.Summary.ExcludedCount.ToString(CultureInfo.InvariantCulture);
        SummaryClassAverage = sheet.Summary.ClassAverageDisplay;
        SummarySuccessRate = sheet.Summary.SuccessRateDisplay;
        SummaryCalculatedAt = FormatDateTime(sheet.Summary.CalculatedAtUtc);
        SummaryLastUpdated = FormatDateTime(sheet.Summary.LastUpdatedAtUtc);

        Events.Clear();
        foreach (var ev in sheet.Events)
        {
            Events.Add(new ResultValidationEventVm(
                ev.OperationLabel,
                ev.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
                ev.UserName,
                ev.Observations));
        }

        ReadinessIssues.Clear();
        foreach (var issue in sheet.Readiness.Issues)
        {
            ReadinessIssues.Add($"[{issue.Severity}] {issue.Message}");
        }

        HasReadinessIssues = ReadinessIssues.Count > 0;

        if (sheet.Status != StatusFilter)
        {
            Rows.Clear();
            CanValidate = CanCancelValidation = CanLock = CanUnlock = false;
            StatusMessage =
                $"Aucun résultat au statut « {FormatStatus(StatusFilter)} » pour cette sélection " +
                $"(statut actuel : {sheet.StatusLabel}).";
            return;
        }

        CanValidate = sheet.CanValidate;
        CanCancelValidation = sheet.CanCancelValidation;
        CanLock = sheet.CanLock;
        CanUnlock = sheet.CanUnlock;

        Rows.Clear();
        foreach (var row in sheet.Students)
        {
            Rows.Add(new ResultValidationRowVm(
                row.Rank <= 0 ? "—" : row.Rank.ToString(CultureInfo.InvariantCulture),
                row.RegistrationNumber,
                row.FullName,
                row.AverageDisplay,
                row.PercentageDisplay,
                row.Mention,
                row.DecisionLabel,
                row.ValidationStatusLabel));
        }

        if (!sheet.Readiness.HasCalculatedResults)
        {
            StatusMessage = "Aucun résultat calculé. Calculez les résultats avant validation.";
        }
        else if (!sheet.Readiness.IsReady)
        {
            StatusMessage = "Des contrôles bloquent la validation — voir le rapport.";
        }
        else
        {
            StatusMessage = $"{sheet.Students.Count} élève(s) — {sheet.StatusLabel}";
        }
    }

    private void ClearSheet()
    {
        _loadedStatus = null;
        _lastSheet = null;
        Rows.Clear();
        Events.Clear();
        ReadinessIssues.Clear();
        HasReadinessIssues = false;
        CanValidate = CanCancelValidation = CanLock = CanUnlock = false;
        StatusLabel = "Non validé";
        SummaryStudentCount = SummaryAdmitted = SummaryDeferred = SummaryExcluded = "0";
        SummaryClassAverage = SummarySuccessRate = SummaryCalculatedAt = SummaryLastUpdated = "—";
    }

    private async Task RunActionAsync(
        string confirmMessage,
        Func<ResultValidationActionRequest, Task<ResultValidationSheetDto>> action)
    {
        if (SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            StatusMessage = "Sélectionnez une classe et une sous-période.";
            return;
        }

        if (MessageBox.Show(confirmMessage, "Validation des résultats",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var request = new ResultValidationActionRequest(
                SelectedYear.Id,
                SelectedClassRoom.Id,
                SelectedPeriod.Id,
                Observations);
            var sheet = await action(request);
            Observations = null;
            StatusFilter = sheet.Status;
            ApplySheet(sheet);
            StatusMessage = "Opération enregistrée.";
            SheetChanged?.Invoke();
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

    private string BuildHtmlDocument()
    {
        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset='utf-8'><title>Validation des résultats</title>")
            .Append("<style>body{font-family:Segoe UI,sans-serif}table{border-collapse:collapse;width:100%}")
            .Append("th,td{border:1px solid #cbd5e1;padding:6px;font-size:12px}th{background:#0B1F47;color:#fff}</style></head><body>");
        sb.Append("<h2>Validation des résultats — ").Append(System.Net.WebUtility.HtmlEncode(ClassDisplayName))
            .Append(" / ").Append(System.Net.WebUtility.HtmlEncode(PeriodLabel)).Append("</h2>");
        sb.Append("<table><tr><th>Rang</th><th>Matricule</th><th>Nom</th><th>Moyenne</th><th>%</th><th>Mention</th><th>Décision</th><th>Statut</th></tr>");
        foreach (var row in Rows)
        {
            sb.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(row.RankDisplay))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.RegistrationNumber))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.FullName))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.AverageDisplay))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.PercentageDisplay))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.Mention ?? ""))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.DecisionLabel))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.ValidationStatusLabel))
                .Append("</td></tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private FlowDocument BuildPrintDocument()
    {
        var doc = new FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12,
            PagePadding = new Thickness(40)
        };
        doc.Blocks.Add(new Paragraph(new Run($"Validation des résultats — {ClassDisplayName} / {PeriodLabel}"))
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        });

        var table = new Table();
        for (var i = 0; i < 8; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var header = new TableRowGroup();
        var headerRow = new TableRow();
        foreach (var h in new[] { "Rang", "Matricule", "Nom", "Moyenne", "%", "Mention", "Décision", "Statut" })
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { FontWeight = FontWeights.Bold });
        }

        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var row in Rows)
        {
            var tr = new TableRow();
            foreach (var cell in new[]
                     {
                         row.RankDisplay, row.RegistrationNumber, row.FullName, row.AverageDisplay,
                         row.PercentageDisplay, row.Mention ?? "", row.DecisionLabel, row.ValidationStatusLabel
                     })
            {
                tr.Cells.Add(new TableCell(new Paragraph(new Run(cell))));
            }

            body.Rows.Add(tr);
        }

        table.RowGroups.Add(body);
        doc.Blocks.Add(table);
        return doc;
    }

    private static string FormatDateTime(DateTime? utc) =>
        utc is null
            ? "—"
            : utc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

    private static string FormatStatus(ResultValidationStatus status) =>
        status switch
        {
            ResultValidationStatus.Valide => "Validé",
            ResultValidationStatus.Verrouille => "Verrouillé",
            _ => "Non validé"
        };

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public sealed record ResultValidationRowVm(
    string RankDisplay,
    string RegistrationNumber,
    string FullName,
    string AverageDisplay,
    string PercentageDisplay,
    string? Mention,
    string DecisionLabel,
    string ValidationStatusLabel);

public sealed record ResultValidationEventVm(
    string OperationLabel,
    string OccurredAtDisplay,
    string UserName,
    string? Observations);
