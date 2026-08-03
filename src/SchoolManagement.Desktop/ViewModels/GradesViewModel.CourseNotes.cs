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
    [ObservableProperty] private bool _isCourseNotesLoading;
    [ObservableProperty] private string? _courseNotesError;
    [ObservableProperty] private string _courseNotesSearchText = string.Empty;
    [ObservableProperty] private string _courseNotesCourseName = "—";
    [ObservableProperty] private string _courseNotesClassName = "—";
    [ObservableProperty] private string _courseNotesPeriodName = "—";
    [ObservableProperty] private int _courseNotesEvaluationCount;
    [ObservableProperty] private int _courseNotesStudentCount;

    public ObservableCollection<CourseNotesColumnVm> CourseNotesColumns { get; } = [];
    public ObservableCollection<CourseNotesRowVm> CourseNotesRows { get; } = [];
    public ObservableCollection<CourseNotesRowVm> FilteredCourseNotesRows { get; } = [];

    public event Action? CourseNotesColumnsChanged;

    public bool ShowManagerTabNotes =>
        IsEvaluationManagerOpen
        && EvaluationManagerTabIndex == 1;

    public bool ShowCourseNotesContent =>
        ShowManagerTabNotes
        && !IsCourseNotesLoading
        && string.IsNullOrWhiteSpace(CourseNotesError);

    public bool ShowCourseNotesError =>
        ShowManagerTabNotes
        && !IsCourseNotesLoading
        && !string.IsNullOrWhiteSpace(CourseNotesError);

    /// <summary>Bouton « Créer une évaluation » : visible uniquement sur l'onglet Évaluations (liste non vide).</summary>
    public bool ShowCreateEvaluationInManager =>
        IsEvaluationManagerOpen
        && EvaluationManagerTabIndex == 0
        && !IsManagedEvaluationsLoading
        && string.IsNullOrWhiteSpace(ManagedEvaluationsError)
        && HasManagedEvaluations;

    partial void OnCourseNotesSearchTextChanged(string value) => ApplyCourseNotesFilter();

    partial void OnIsCourseNotesLoadingChanged(bool value) => NotifyCourseNotesUi();

    [RelayCommand]
    private async Task RetryCourseNotesAsync() => await ReloadCourseNotesGridAsync();

    [RelayCommand]
    private async Task ExportCourseNotesExcelAsync()
    {
        if (CourseNotesColumns.Count == 0 && FilteredCourseNotesRows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV Excel (*.csv)|*.csv",
            FileName = $"Notes_{CourseNotesCourseName}_{CourseNotesClassName}.csv".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append("N°;Matricule;Nom");
        foreach (var col in CourseNotesColumns)
        {
            sb.Append(';').Append(EscapeCsv(col.Header));
        }

        sb.AppendLine();

        foreach (var row in FilteredCourseNotesRows)
        {
            sb.Append(row.RowNumber).Append(';')
                .Append(EscapeCsv(row.RegistrationNumber)).Append(';')
                .Append(EscapeCsv(row.StudentName));
            foreach (var cell in row.Cells)
            {
                sb.Append(';').Append(EscapeCsv(cell.Display));
            }

            sb.AppendLine();
        }

        await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
        StatusMessage = "Export Excel enregistré.";
    }

    [RelayCommand]
    private void PrintCourseNotes()
    {
        if (FilteredCourseNotesRows.Count == 0)
        {
            StatusMessage = "Aucune donnée à imprimer.";
            return;
        }

        var doc = BuildCourseNotesDocument();
        var printDialog = new PrintDialog();
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Récapitulatif des notes");
        StatusMessage = "Impression envoyée.";
    }

    private async Task ReloadCourseNotesGridAsync()
    {
        if (!IsEvaluationManagerOpen)
        {
            return;
        }

        IsCourseNotesLoading = true;
        CourseNotesError = null;
        try
        {
            var yearId = SelectedYear?.Id ?? _session?.AcademicYearId ?? Guid.Empty;
            var classRoomId = SelectedLocal?.Id ?? Guid.Empty;
            var courseId = _managedCourseId != Guid.Empty
                ? _managedCourseId
                : SelectedCourse?.CourseId ?? Guid.Empty;
            var periodId = SelectedPeriod?.Id ?? _activeCotationPeriod?.Id ?? Guid.Empty;

            if (yearId == Guid.Empty || classRoomId == Guid.Empty || courseId == Guid.Empty || periodId == Guid.Empty)
            {
                CourseNotesError = "Contexte de cotation incomplet (classe / cours / période).";
                ClearCourseNotesGrid();
                return;
            }

            var grid = await _gradeApiService.GetCourseNotesGridAsync(
                yearId,
                classRoomId,
                courseId,
                periodId);

            CourseNotesCourseName = grid.CourseName;
            CourseNotesClassName = grid.ClassDisplayName;
            CourseNotesPeriodName = grid.PeriodName;
            CourseNotesEvaluationCount = grid.EvaluationCount;
            CourseNotesStudentCount = grid.StudentCount;

            CourseNotesColumns.Clear();
            foreach (var col in grid.Evaluations)
            {
                CourseNotesColumns.Add(new CourseNotesColumnVm(col));
            }

            CourseNotesRows.Clear();
            foreach (var row in grid.Students)
            {
                CourseNotesRows.Add(new CourseNotesRowVm(row, CourseNotesColumns));
            }

            ApplyCourseNotesFilter();
            CourseNotesColumnsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            CourseNotesError = ex.Message;
            ClearCourseNotesGrid();
        }
        finally
        {
            IsCourseNotesLoading = false;
            NotifyCourseNotesUi();
        }
    }

    private void ClearCourseNotesGrid()
    {
        CourseNotesColumns.Clear();
        CourseNotesRows.Clear();
        FilteredCourseNotesRows.Clear();
        CourseNotesEvaluationCount = 0;
        CourseNotesStudentCount = 0;
        CourseNotesColumnsChanged?.Invoke();
    }

    private void ApplyCourseNotesFilter()
    {
        FilteredCourseNotesRows.Clear();
        var q = (CourseNotesSearchText ?? string.Empty).Trim();
        IEnumerable<CourseNotesRowVm> source = CourseNotesRows;
        if (!string.IsNullOrWhiteSpace(q))
        {
            source = CourseNotesRows.Where(r =>
                r.StudentName.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || r.RegistrationNumber.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        }

        foreach (var row in source)
        {
            FilteredCourseNotesRows.Add(row);
        }
    }

    private void NotifyCourseNotesUi()
    {
        OnPropertyChanged(nameof(ShowManagerTabNotes));
        OnPropertyChanged(nameof(ShowCourseNotesContent));
        OnPropertyChanged(nameof(ShowCourseNotesError));
        OnPropertyChanged(nameof(ShowManagerTabPlaceholder));
        OnPropertyChanged(nameof(ShowCreateEvaluationInManager));
    }

    private FlowDocument BuildCourseNotesDocument()
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11
        };

        doc.Blocks.Add(new Paragraph(new Run("Récapitulatif des notes"))
        {
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        doc.Blocks.Add(new Paragraph(new Run(
            $"Cours : {CourseNotesCourseName}  |  Classe : {CourseNotesClassName}  |  Sous-période : {CourseNotesPeriodName}"))
        {
            Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0 };
        var colCount = 3 + CourseNotesColumns.Count;
        for (var i = 0; i < colCount; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow();
        void AddHeader(string text) =>
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(text)))
            {
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });

        AddHeader("N°");
        AddHeader("Matricule");
        AddHeader("Élève");
        foreach (var col in CourseNotesColumns)
        {
            AddHeader(col.Header);
        }

        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        var body = new TableRowGroup();
        foreach (var row in FilteredCourseNotesRows)
        {
            var tr = new TableRow();
            void AddCell(string text) =>
                tr.Cells.Add(new TableCell(new Paragraph(new Run(text)))
                {
                    Padding = new Thickness(4),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                });

            AddCell(row.RowNumber.ToString());
            AddCell(row.RegistrationNumber);
            AddCell(row.StudentName);
            foreach (var cell in row.Cells)
            {
                AddCell(cell.Display);
            }

            body.Rows.Add(tr);
        }

        table.RowGroups.Add(body);
        doc.Blocks.Add(table);
        return doc;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
    }
}

public sealed class CourseNotesColumnVm
{
    public CourseNotesColumnVm(CourseNotesEvaluationColumnDto dto)
    {
        EvaluationId = dto.EvaluationId;
        Title = dto.Title;
        MaxScore = dto.MaxScore;
        EvaluationDate = dto.EvaluationDate;
        Header = $"{dto.Title} ({dto.MaxScore})";
    }

    public Guid EvaluationId { get; }
    public string Title { get; }
    public int MaxScore { get; }
    public DateOnly EvaluationDate { get; }
    public string Header { get; }
}

public partial class CourseNotesRowVm : ObservableObject
{
    public CourseNotesRowVm(CourseNotesStudentRowDto dto, IReadOnlyList<CourseNotesColumnVm> columns)
    {
        RowNumber = dto.RowNumber;
        StudentId = dto.StudentId;
        RegistrationNumber = dto.RegistrationNumber;
        StudentName = dto.StudentName;

        var byEval = dto.Cells.ToDictionary(c => c.EvaluationId);
        foreach (var col in columns)
        {
            byEval.TryGetValue(col.EvaluationId, out var cell);
            Cells.Add(new CourseNotesCellVm(cell));
        }
    }

    public int RowNumber { get; }
    public Guid StudentId { get; }
    public string RegistrationNumber { get; }
    public string StudentName { get; }
    public ObservableCollection<CourseNotesCellVm> Cells { get; } = [];
}

public sealed class CourseNotesCellVm
{
    public CourseNotesCellVm(CourseNotesCellDto? dto)
    {
        if (dto is null || !dto.HasGrade)
        {
            Display = "—";
            return;
        }

        if (dto.IsAbsent)
        {
            Display = "Abs";
            return;
        }

        Display = dto.Score?.ToString("0.##", CultureInfo.GetCultureInfo("fr-FR")) ?? "—";
    }

    public string Display { get; }
}
