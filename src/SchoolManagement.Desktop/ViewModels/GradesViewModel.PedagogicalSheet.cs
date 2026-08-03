using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class GradesViewModel
{
    [ObservableProperty] private bool _isPedagogicalSheetLoading;
    [ObservableProperty] private string? _pedagogicalSheetError;
    [ObservableProperty] private string _pedagogicalSheetSearchText = string.Empty;
    [ObservableProperty] private string _pedagogicalSheetClassName = "—";
    [ObservableProperty] private string _pedagogicalSheetPeriodLabel = "—";
    [ObservableProperty] private bool _isPedagogicalSheetSubPeriodMode = true;
    [ObservableProperty] private PedagogicalSheetPeriodOptionDto? _selectedPedagogicalSheetPeriod;
    [ObservableProperty] private PedagogicalSheetSummaryDto _pedagogicalSheetSummary =
        new("—", "—", "—", "—", "—");

    private PedagogicalSheetContextDto? _pedagogicalSheetContext;
    private bool _suppressPedagogicalSheetReload;

    public ObservableCollection<PedagogicalSheetPeriodOptionDto> PedagogicalSheetPeriodOptions { get; } = [];
    public ObservableCollection<PedagogicalSheetCourseGroupVm> PedagogicalSheetCourseGroups { get; } = [];
    public ObservableCollection<PedagogicalSheetLeafColumnVm> PedagogicalSheetLeafColumns { get; } = [];
    public ObservableCollection<PedagogicalSheetRowVm> PedagogicalSheetRows { get; } = [];
    public ObservableCollection<PedagogicalSheetRowVm> FilteredPedagogicalSheetRows { get; } = [];

    public event Action? PedagogicalSheetColumnsChanged;

    public bool ShowManagerTabPedagogicalSheet =>
        IsGlobalCotationOpen && GlobalCotationTabIndex == 1;

    public bool ShowPedagogicalSheetContent =>
        ShowManagerTabPedagogicalSheet
        && !IsPedagogicalSheetLoading
        && string.IsNullOrWhiteSpace(PedagogicalSheetError);

    public bool ShowPedagogicalSheetError =>
        ShowManagerTabPedagogicalSheet
        && !IsPedagogicalSheetLoading
        && !string.IsNullOrWhiteSpace(PedagogicalSheetError);

    public string PedagogicalSheetModeLabel =>
        IsPedagogicalSheetSubPeriodMode ? "Sous-période" : "Période principale";

    partial void OnPedagogicalSheetSearchTextChanged(string value) => ApplyPedagogicalSheetFilter();

    partial void OnIsPedagogicalSheetLoadingChanged(bool value) => NotifyPedagogicalSheetUi();

    partial void OnIsPedagogicalSheetSubPeriodModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PedagogicalSheetModeLabel));
        RebuildPedagogicalSheetPeriodOptions();
        _ = ReloadPedagogicalSheetAsync();
    }

    partial void OnSelectedPedagogicalSheetPeriodChanged(PedagogicalSheetPeriodOptionDto? value)
    {
        if (_suppressPedagogicalSheetReload)
        {
            return;
        }

        _ = ReloadPedagogicalSheetAsync();
    }

    [RelayCommand]
    private async Task RetryPedagogicalSheetAsync() => await EnsurePedagogicalSheetLoadedAsync(force: true);

    [RelayCommand]
    private void SelectPedagogicalSheetSubPeriodMode() => IsPedagogicalSheetSubPeriodMode = true;

    [RelayCommand]
    private void SelectPedagogicalSheetMainPeriodMode() => IsPedagogicalSheetSubPeriodMode = false;

    [RelayCommand]
    private async Task ExportPedagogicalSheetExcelAsync()
    {
        if (FilteredPedagogicalSheetRows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV Excel (*.csv)|*.csv",
            FileName = $"Vue_globale_{PedagogicalSheetClassName}_{PedagogicalSheetPeriodLabel}.csv"
                .Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        var sb = new StringBuilder();

        // Ligne cours
        sb.Append(";;");
        foreach (var group in PedagogicalSheetCourseGroups)
        {
            sb.Append(EscapeCsv(group.CourseName));
            var span = Math.Max(1, group.EvaluationCount) + 2; // evals + TOTAL + MOYENNE
            for (var i = 1; i < span; i++)
            {
                sb.Append(';');
            }
        }

        sb.AppendLine();

        // Ligne évaluations
        sb.Append("N°;Matricule;Nom");
        foreach (var leaf in PedagogicalSheetLeafColumns)
        {
            sb.Append(';').Append(EscapeCsv(leaf.ShortLabel));
        }

        sb.AppendLine();

        // Ligne maxima
        sb.Append(";;");
        foreach (var leaf in PedagogicalSheetLeafColumns)
        {
            sb.Append(';').Append(EscapeCsv(leaf.MaxLabel));
        }

        sb.AppendLine();

        foreach (var row in FilteredPedagogicalSheetRows)
        {
            sb.Append(row.RowNumber).Append(';')
                .Append(EscapeCsv(row.RegistrationNumber)).Append(';')
                .Append(EscapeCsv(row.StudentName));
            foreach (var cell in row.LeafDisplays)
            {
                sb.Append(';').Append(EscapeCsv(cell));
            }

            sb.AppendLine();
        }

        // Synthèse
        sb.Append(";;;Synthèse");
        sb.Append(";Moyenne classe=").Append(EscapeCsv(PedagogicalSheetSummary.ClassAverageDisplay));
        sb.Append(";Max=").Append(EscapeCsv(PedagogicalSheetSummary.MaxObtainedDisplay));
        sb.Append(";Min=").Append(EscapeCsv(PedagogicalSheetSummary.MinObtainedDisplay));
        sb.Append(";Cotés=").Append(EscapeCsv(PedagogicalSheetSummary.GradedCountDisplay));
        sb.Append(";ABS=").Append(EscapeCsv(PedagogicalSheetSummary.AbsentCountDisplay));
        sb.AppendLine();

        await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
        StatusMessage = "Export Excel enregistré.";
    }

    [RelayCommand]
    private void PrintPedagogicalSheet()
    {
        if (FilteredPedagogicalSheetRows.Count == 0)
        {
            StatusMessage = "Aucune donnée à imprimer.";
            return;
        }

        var doc = BuildPedagogicalSheetDocument();
        var printDialog = new PrintDialog();
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Vue globale");
        StatusMessage = "Impression envoyée.";
    }

    private async Task EnsurePedagogicalSheetLoadedAsync(bool force = false)
    {
        if (!IsGlobalCotationOpen && !IsEvaluationManagerOpen)
        {
            return;
        }

        await LoadPedagogicalSheetContextAndDataAsync();
    }

    private async Task LoadPedagogicalSheetContextAndDataAsync()
    {
        IsPedagogicalSheetLoading = true;
        PedagogicalSheetError = null;
        try
        {
            var yearId = _globalYearId != Guid.Empty
                ? _globalYearId
                : SelectedYear?.Id ?? _session?.AcademicYearId ?? Guid.Empty;
            var classRoomId = _globalClassRoomId != Guid.Empty
                ? _globalClassRoomId
                : SelectedLocal?.Id ?? Guid.Empty;
            if (yearId == Guid.Empty || classRoomId == Guid.Empty || _session is null)
            {
                PedagogicalSheetError = "Contexte de cotation incomplet.";
                ClearPedagogicalSheet();
                return;
            }

            _pedagogicalSheetContext = await _gradeApiService.GetPedagogicalSheetContextAsync(
                yearId,
                classRoomId);

            PedagogicalSheetClassName = _pedagogicalSheetContext.ClassDisplayName;
            RebuildPedagogicalSheetPeriodOptions();
            await ReloadPedagogicalSheetAsync();
        }
        catch (Exception ex)
        {
            PedagogicalSheetError = ex.Message;
            ClearPedagogicalSheet();
        }
        finally
        {
            IsPedagogicalSheetLoading = false;
            NotifyPedagogicalSheetUi();
        }
    }

    private void RebuildPedagogicalSheetPeriodOptions()
    {
        if (_pedagogicalSheetContext is null)
        {
            return;
        }

        _suppressPedagogicalSheetReload = true;
        try
        {
            PedagogicalSheetPeriodOptions.Clear();
            var source = IsPedagogicalSheetSubPeriodMode
                ? _pedagogicalSheetContext.SubPeriods
                : _pedagogicalSheetContext.MainPeriods;

            foreach (var opt in source)
            {
                PedagogicalSheetPeriodOptions.Add(opt);
            }

            Guid? preferred = IsPedagogicalSheetSubPeriodMode
                ? _pedagogicalSheetContext.DefaultSubPeriodId
                : _pedagogicalSheetContext.DefaultMainPeriodId;

            SelectedPedagogicalSheetPeriod =
                PedagogicalSheetPeriodOptions.FirstOrDefault(o => o.Id == preferred)
                ?? PedagogicalSheetPeriodOptions.FirstOrDefault();
        }
        finally
        {
            _suppressPedagogicalSheetReload = false;
        }
    }

    private async Task ReloadPedagogicalSheetAsync()
    {
        if (_session is null || SelectedPedagogicalSheetPeriod is null)
        {
            return;
        }

        var yearId = _globalYearId != Guid.Empty
            ? _globalYearId
            : SelectedYear?.Id ?? _session.AcademicYearId;
        var classRoomId = _globalClassRoomId != Guid.Empty
            ? _globalClassRoomId
            : SelectedLocal?.Id ?? Guid.Empty;
        if (classRoomId == Guid.Empty)
        {
            return;
        }

        var wasLoading = IsPedagogicalSheetLoading;
        if (!wasLoading)
        {
            IsPedagogicalSheetLoading = true;
        }

        PedagogicalSheetError = null;
        try
        {
            var mode = IsPedagogicalSheetSubPeriodMode
                ? PedagogicalSheetPeriodMode.SubPeriod
                : PedagogicalSheetPeriodMode.MainPeriod;

            var sheet = await _gradeApiService.GetPedagogicalSheetAsync(
                yearId,
                classRoomId,
                mode,
                SelectedPedagogicalSheetPeriod.Id,
                _session.TeacherId);

            PedagogicalSheetClassName = sheet.ClassDisplayName;
            PedagogicalSheetPeriodLabel = sheet.SelectedPeriodLabel;
            PedagogicalSheetSummary = sheet.Summary;

            PedagogicalSheetCourseGroups.Clear();
            PedagogicalSheetLeafColumns.Clear();
            foreach (var course in sheet.Courses)
            {
                var groupVm = new PedagogicalSheetCourseGroupVm(course);
                PedagogicalSheetCourseGroups.Add(groupVm);
                foreach (var leaf in groupVm.LeafColumns)
                {
                    PedagogicalSheetLeafColumns.Add(leaf);
                }
            }

            PedagogicalSheetRows.Clear();
            foreach (var row in sheet.Students)
            {
                PedagogicalSheetRows.Add(new PedagogicalSheetRowVm(row, sheet.Courses));
            }

            ApplyPedagogicalSheetFilter();
            PedagogicalSheetColumnsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            PedagogicalSheetError = ex.Message;
            ClearPedagogicalSheet();
        }
        finally
        {
            if (!wasLoading)
            {
                IsPedagogicalSheetLoading = false;
            }

            NotifyPedagogicalSheetUi();
        }
    }

    private void ClearPedagogicalSheet()
    {
        PedagogicalSheetCourseGroups.Clear();
        PedagogicalSheetLeafColumns.Clear();
        PedagogicalSheetRows.Clear();
        FilteredPedagogicalSheetRows.Clear();
        PedagogicalSheetSummary = new PedagogicalSheetSummaryDto("—", "—", "—", "—", "—");
        PedagogicalSheetColumnsChanged?.Invoke();
    }

    private void ApplyPedagogicalSheetFilter()
    {
        FilteredPedagogicalSheetRows.Clear();
        var q = (PedagogicalSheetSearchText ?? string.Empty).Trim();
        IEnumerable<PedagogicalSheetRowVm> source = PedagogicalSheetRows;
        if (!string.IsNullOrWhiteSpace(q))
        {
            source = PedagogicalSheetRows.Where(r =>
                r.StudentName.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || r.RegistrationNumber.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        }

        foreach (var row in source)
        {
            FilteredPedagogicalSheetRows.Add(row);
        }
    }

    private void NotifyPedagogicalSheetUi()
    {
        OnPropertyChanged(nameof(ShowManagerTabPedagogicalSheet));
        OnPropertyChanged(nameof(ShowPedagogicalSheetContent));
        OnPropertyChanged(nameof(ShowPedagogicalSheetError));
        OnPropertyChanged(nameof(ShowGlobalVueTab));
        OnPropertyChanged(nameof(ShowGlobalSaisieTab));
    }

    private FlowDocument BuildPedagogicalSheetDocument()
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(30),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 10
        };

        doc.Blocks.Add(new Paragraph(new Run("Vue globale — feuille pédagogique"))
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        doc.Blocks.Add(new Paragraph(new Run(
            $"Classe : {PedagogicalSheetClassName}  |  Période : {PedagogicalSheetPeriodLabel}"))
        {
            Margin = new Thickness(0, 0, 0, 10)
        });

        var table = new Table { CellSpacing = 0 };
        var colCount = 3 + PedagogicalSheetLeafColumns.Count;
        for (var i = 0; i < colCount; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var headers = new TableRowGroup();
        var courseRow = new TableRow();
        void AddCell(TableRow row, string text, int span = 1, bool bold = false)
        {
            var cell = new TableCell(new Paragraph(new Run(text)))
            {
                Padding = new Thickness(3),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0.5),
                ColumnSpan = span
            };
            if (bold)
            {
                cell.FontWeight = FontWeights.SemiBold;
            }

            row.Cells.Add(cell);
        }

        AddCell(courseRow, "", 3, true);
        foreach (var group in PedagogicalSheetCourseGroups)
        {
            var span = Math.Max(1, group.EvaluationCount) + 1;
            AddCell(courseRow, group.CourseName, span, true);
        }

        headers.Rows.Add(courseRow);

        var evalRow = new TableRow();
        AddCell(evalRow, "N°", bold: true);
        AddCell(evalRow, "Matricule", bold: true);
        AddCell(evalRow, "Élève", bold: true);
        foreach (var leaf in PedagogicalSheetLeafColumns)
        {
            AddCell(evalRow, leaf.ShortLabel, bold: true);
        }

        headers.Rows.Add(evalRow);

        var maxRow = new TableRow();
        AddCell(maxRow, "", 3);
        foreach (var leaf in PedagogicalSheetLeafColumns)
        {
            AddCell(maxRow, leaf.MaxLabel);
        }

        headers.Rows.Add(maxRow);
        table.RowGroups.Add(headers);

        var body = new TableRowGroup();
        foreach (var row in FilteredPedagogicalSheetRows)
        {
            var tr = new TableRow();
            AddCell(tr, row.RowNumber.ToString());
            AddCell(tr, row.RegistrationNumber);
            AddCell(tr, row.StudentName);
            foreach (var cell in row.LeafDisplays)
            {
                AddCell(tr, cell);
            }

            body.Rows.Add(tr);
        }

        table.RowGroups.Add(body);

        var summaryGroup = new TableRowGroup();
        var summaryRow = new TableRow();
        AddCell(summaryRow,
            $"Synthèse — Moyenne: {PedagogicalSheetSummary.ClassAverageDisplay} | Max: {PedagogicalSheetSummary.MaxObtainedDisplay} | Min: {PedagogicalSheetSummary.MinObtainedDisplay} | Cotés: {PedagogicalSheetSummary.GradedCountDisplay} | ABS: {PedagogicalSheetSummary.AbsentCountDisplay}",
            colCount,
            true);
        summaryGroup.Rows.Add(summaryRow);
        table.RowGroups.Add(summaryGroup);

        doc.Blocks.Add(table);
        return doc;
    }
}

public sealed class PedagogicalSheetCourseGroupVm
{
    public PedagogicalSheetCourseGroupVm(PedagogicalSheetCourseGroupDto dto)
    {
        CourseId = dto.CourseId;
        CourseName = dto.CourseName;
        TargetMaxScore = dto.TargetMaxScore;
        EvaluationCount = dto.Evaluations.Count;

        for (var i = 0; i < dto.Evaluations.Count; i++)
        {
            var ev = dto.Evaluations[i];
            LeafColumns.Add(new PedagogicalSheetLeafColumnVm(
                CourseId,
                CourseName,
                isFirstInGroup: i == 0,
                kind: PedagogicalSheetLeafKind.Evaluation,
                evaluationTitle: ev.Title,
                shortLabel: PedagogicalSheetLeafColumnVm.BuildShortLabel(ev.Title),
                maxScore: ev.MaxScore,
                evaluationId: ev.EvaluationId));
        }

        LeafColumns.Add(new PedagogicalSheetLeafColumnVm(
            CourseId,
            CourseName,
            isFirstInGroup: dto.Evaluations.Count == 0,
            kind: PedagogicalSheetLeafKind.Total,
            evaluationTitle: "Total",
            shortLabel: "TOTAL",
            maxScore: null,
            evaluationId: null));

        LeafColumns.Add(new PedagogicalSheetLeafColumnVm(
            CourseId,
            CourseName,
            isFirstInGroup: false,
            kind: PedagogicalSheetLeafKind.Average,
            evaluationTitle: "Moyenne",
            shortLabel: "MOY",
            maxScore: dto.TargetMaxScore > 0 ? dto.TargetMaxScore : null,
            evaluationId: null));
    }

    public Guid CourseId { get; }
    public string CourseName { get; }
    public int TargetMaxScore { get; }
    public int EvaluationCount { get; }
    public List<PedagogicalSheetLeafColumnVm> LeafColumns { get; } = [];
}

public enum PedagogicalSheetLeafKind
{
    Evaluation = 1,
    Total = 2,
    Average = 3
}

public sealed class PedagogicalSheetLeafColumnVm
{
    public PedagogicalSheetLeafColumnVm(
        Guid courseId,
        string courseName,
        bool isFirstInGroup,
        PedagogicalSheetLeafKind kind,
        string evaluationTitle,
        string shortLabel,
        int? maxScore,
        Guid? evaluationId)
    {
        CourseId = courseId;
        CourseName = courseName;
        IsFirstInGroup = isFirstInGroup;
        Kind = kind;
        EvaluationTitle = evaluationTitle;
        ShortLabel = shortLabel;
        MaxScore = maxScore;
        EvaluationId = evaluationId;
        MaxLabel = kind == PedagogicalSheetLeafKind.Evaluation && maxScore is not null
            ? $"/{maxScore}"
            : kind == PedagogicalSheetLeafKind.Average && maxScore is not null
                ? $"/{maxScore}"
                : string.Empty;
        CourseHeaderText = isFirstInGroup ? courseName.ToUpperInvariant() : string.Empty;
        IsTotal = kind == PedagogicalSheetLeafKind.Total;
        IsAverage = kind == PedagogicalSheetLeafKind.Average;
        IsSynthesis = IsTotal || IsAverage;
    }

    public Guid CourseId { get; }
    public string CourseName { get; }
    public bool IsFirstInGroup { get; }
    public PedagogicalSheetLeafKind Kind { get; }
    public bool IsTotal { get; }
    public bool IsAverage { get; }
    public bool IsSynthesis { get; }
    public string EvaluationTitle { get; }
    public string ShortLabel { get; }
    public int? MaxScore { get; }
    public Guid? EvaluationId { get; }
    public string MaxLabel { get; }
    public string CourseHeaderText { get; }

    public static string BuildShortLabel(string title)
    {
        var t = (title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            return "EV";
        }

        var upper = t.ToUpperInvariant();
        if (upper.Contains("EXAM"))
        {
            return "EXAM";
        }

        if (upper.Contains("INTERRO") || upper.StartsWith("INT"))
        {
            return "INT" + ExtractTrailingNumber(t);
        }

        if (upper.Contains("DEVOIR") || upper.StartsWith("DEV"))
        {
            return "DEV" + ExtractTrailingNumber(t);
        }

        if (upper.Contains("TRAVAUX") || upper.Contains("JOURNAL"))
        {
            return "TJ" + ExtractTrailingNumber(t);
        }

        var compact = new string(t.Where(char.IsLetterOrDigit).Take(6).ToArray());
        return string.IsNullOrWhiteSpace(compact) ? "EV" : compact.ToUpperInvariant();
    }

    private static string ExtractTrailingNumber(string title)
    {
        var digits = new string(title.Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digits) ? string.Empty : digits;
    }
}

public partial class PedagogicalSheetRowVm : ObservableObject
{
    public PedagogicalSheetRowVm(
        PedagogicalSheetStudentRowDto dto,
        IReadOnlyList<PedagogicalSheetCourseGroupDto> courses)
    {
        RowNumber = dto.RowNumber;
        StudentId = dto.StudentId;
        RegistrationNumber = dto.RegistrationNumber;
        StudentName = dto.StudentName;

        var byCourse = dto.CourseCells.ToDictionary(c => c.CourseId);
        foreach (var course in courses)
        {
            if (!byCourse.TryGetValue(course.CourseId, out var cells))
            {
                foreach (var _ in course.Evaluations)
                {
                    LeafDisplays.Add("—");
                }

                LeafDisplays.Add("—");
                LeafDisplays.Add("—");
                continue;
            }

            foreach (var cell in cells.Cells)
            {
                LeafDisplays.Add(cell.Display);
            }

            LeafDisplays.Add(cells.TotalDisplay);
            LeafDisplays.Add(cells.AverageDisplay);
        }
    }

    public int RowNumber { get; }
    public Guid StudentId { get; }
    public string RegistrationNumber { get; }
    public string StudentName { get; }
    public ObservableCollection<string> LeafDisplays { get; } = [];
}
