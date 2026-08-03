using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class ResultsHubViewModel : ViewModelBase
{
    public ResultsHubViewModel(
        ClassResultsViewModel classResults,
        IndividualResultViewModel individual,
        DeliberationWorkspaceViewModel deliberation,
        ResultsPlaceholderViewModel placeholder)
    {
        ClassResults = classResults;
        Individual = individual;
        Deliberation = deliberation;
        Placeholder = placeholder;
        ResultsNavigationBridge.IndividualRequested += OnIndividualRequested;
        ApplyNavigation(ResultsNavCatalog.DefaultItem);
    }

    public ClassResultsViewModel ClassResults { get; }

    public IndividualResultViewModel Individual { get; }

    public DeliberationWorkspaceViewModel Deliberation { get; }

    public ResultsPlaceholderViewModel Placeholder { get; }

    [ObservableProperty] private ResultsSection _selectedSection = ResultsSection.ParClasse;

    public bool IsParClasseSelected => SelectedSection == ResultsSection.ParClasse;

    public bool IsIndividuelSelected => SelectedSection == ResultsSection.Individuel;

    public bool IsDeliberationSelected => SelectedSection == ResultsSection.Deliberation;

    public bool IsPlaceholderSelected =>
        SelectedSection is not ResultsSection.ParClasse
            and not ResultsSection.Individuel
            and not ResultsSection.Deliberation;

    public string? ActiveNavKey { get; private set; }

    public string SelectedSectionTitle =>
        ResultsNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Title ?? "Résultats scolaires";

    public string SelectedSectionDescription =>
        ResultsNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Subtitle
        ?? "Point d'accès unique aux résultats académiques";

    public void ApplyNavigation(ResultsNavItem item)
    {
        ActiveNavKey = item.Key;
        SelectedSection = item.Section;

        if (item.IsPlaceholder)
        {
            var message = item.IsBulletinSection
                ? "Bientôt disponible.\n\nArchitecture prévue : affichage uniquement des données " +
                  "fournies par ResultCalculationService — aucun calcul dans l'interface."
                : "Bientôt disponible";
            Placeholder.Configure(item.Title, message);
        }
        else if (item.Section == ResultsSection.ParClasse)
        {
            _ = ClassResults.EnsureLoadedAsync();
        }
        else if (item.Section == ResultsSection.Individuel && !Individual.HasData)
        {
            Individual.ShowEmptyHint();
        }
        else if (item.Section == ResultsSection.Deliberation)
        {
            _ = Deliberation.EnsureLoadedAsync();
        }

        OnPropertyChanged(nameof(ActiveNavKey));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
        OnPropertyChanged(nameof(IsParClasseSelected));
        OnPropertyChanged(nameof(IsIndividuelSelected));
        OnPropertyChanged(nameof(IsDeliberationSelected));
        OnPropertyChanged(nameof(IsPlaceholderSelected));
    }

    private async void OnIndividualRequested(IndividualResultNavRequest request)
    {
        await Individual.LoadAsync(request);

        var item = ResultsNavCatalog.FindByKey("individuel") ?? ResultsNavCatalog.DefaultItem;
        ActiveNavKey = item.Key;
        SelectedSection = ResultsSection.Individuel;
        ResultsNavigationBridge.Select(item);

        OnPropertyChanged(nameof(ActiveNavKey));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
        OnPropertyChanged(nameof(IsParClasseSelected));
        OnPropertyChanged(nameof(IsIndividuelSelected));
        OnPropertyChanged(nameof(IsDeliberationSelected));
        OnPropertyChanged(nameof(IsPlaceholderSelected));
    }

    partial void OnSelectedSectionChanged(ResultsSection value)
    {
        OnPropertyChanged(nameof(IsParClasseSelected));
        OnPropertyChanged(nameof(IsIndividuelSelected));
        OnPropertyChanged(nameof(IsDeliberationSelected));
        OnPropertyChanged(nameof(IsPlaceholderSelected));
    }
}

public partial class ResultsPlaceholderViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "Module en préparation";
    [ObservableProperty] private string _message = "Bientôt disponible";

    public void Configure(string title, string message)
    {
        Title = title;
        Message = message;
    }
}

public enum ClassResultsSortField
{
    Rank = 0,
    Name = 1,
    Average = 2,
    Decision = 3,
    Mention = 4
}

public partial class ClassResultsViewModel : ViewModelBase
{
    private readonly ISchoolApiService _schoolApi;
    private readonly IGradeApiService _gradeApi;
    private SchoolLookupsDto? _lookups;
    private PedagogicalSheetContextDto? _periodContext;
    private bool _filtersReady;
    private bool _suppressReload;
    private List<ClassResultRowVm> _allRows = [];

    public ClassResultsViewModel(ISchoolApiService schoolApi, IGradeApiService gradeApi)
    {
        _schoolApi = schoolApi;
        _gradeApi = gradeApi;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        SortOptions =
        [
            new ClassResultsSortOption(ClassResultsSortField.Rank, "Rang"),
            new ClassResultsSortOption(ClassResultsSortField.Name, "Nom"),
            new ClassResultsSortOption(ClassResultsSortField.Average, "Moyenne"),
            new ClassResultsSortOption(ClassResultsSortField.Decision, "Décision"),
            new ClassResultsSortOption(ClassResultsSortField.Mention, "Mention")
        ];
        SelectedSort = SortOptions[0];
    }

    public ObservableCollection<ClassRoomLookupDto> ClassRooms { get; } = [];
    public ObservableCollection<PedagogicalSheetPeriodOptionDto> PeriodOptions { get; } = [];
    public ObservableCollection<ClassResultsCourseColumnDto> CourseColumns { get; } = [];
    public ObservableCollection<ClassResultRowVm> FilteredRows { get; } = [];
    public IReadOnlyList<ClassResultsSortOption> SortOptions { get; }

    public event Action? ColumnsChanged;

    /// <summary>Année scolaire = année active de la barre de titre (pas de sélecteur local).</summary>
    [ObservableProperty] private AcademicYearDto? _selectedYear;
    [ObservableProperty] private ClassRoomLookupDto? _selectedClassRoom;
    [ObservableProperty] private PedagogicalSheetPeriodOptionDto? _selectedPeriod;
    [ObservableProperty] private bool _isSubPeriodMode = true;
    [ObservableProperty] private ClassResultsSortOption? _selectedSort;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _classDisplayName = string.Empty;
    [ObservableProperty] private string _periodLabel = string.Empty;
    [ObservableProperty] private string _summaryClassAverage = "—";
    [ObservableProperty] private string _summaryMax = "—";
    [ObservableProperty] private string _summaryMin = "—";
    [ObservableProperty] private int _studentCount;
    [ObservableProperty] private int _gradedCount;

    public string PeriodModeLabel => IsSubPeriodMode ? "Sous-période" : "Période principale";

    public async Task EnsureLoadedAsync()
    {
        if (_lookups is not null)
        {
            return;
        }

        await LoadFiltersAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadSheetAsync();

    [RelayCommand]
    private void Consult(ClassResultRowVm? row)
    {
        if (row is null
            || SelectedYear is null
            || SelectedClassRoom is null
            || SelectedPeriod is null)
        {
            StatusMessage = "Sélectionnez une classe et une période.";
            return;
        }

        ResultsNavigationBridge.RequestIndividual(new IndividualResultNavRequest(
            row.StudentId,
            SelectedYear.Id,
            SelectedClassRoom.Id,
            IsSubPeriodMode
                ? PedagogicalSheetPeriodMode.SubPeriod
                : PedagogicalSheetPeriodMode.MainPeriod,
            SelectedPeriod.Id));
    }

    [RelayCommand]
    private void SelectSubPeriodMode() => IsSubPeriodMode = true;

    [RelayCommand]
    private void SelectMainPeriodMode() => IsSubPeriodMode = false;

    [RelayCommand]
    private void ExportExcel()
    {
        if (FilteredRows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV Excel (*.csv)|*.csv",
            FileName = $"Resultats_{ClassDisplayName}_{PeriodLabel}.csv".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append("Rang;Matricule;Nom complet");
        foreach (var course in CourseColumns)
        {
            sb.Append(';').Append(EscapeCsv(course.CourseName));
        }

        sb.AppendLine(";Moyenne générale;Pourcentage;Mention;Décision;Statut");

        foreach (var row in FilteredRows)
        {
            sb.Append(EscapeCsv(row.RankDisplay)).Append(';')
                .Append(EscapeCsv(row.RegistrationNumber)).Append(';')
                .Append(EscapeCsv(row.StudentName));
            foreach (var cell in row.CourseDisplays)
            {
                sb.Append(';').Append(EscapeCsv(cell));
            }

            sb.Append(';').Append(EscapeCsv(row.AverageDisplay))
                .Append(';').Append(EscapeCsv(row.PercentageDisplay))
                .Append(';').Append(EscapeCsv(row.Mention ?? string.Empty))
                .Append(';').Append(EscapeCsv(row.DecisionLabel))
                .Append(';').Append(EscapeCsv(row.StatusLabel))
                .AppendLine();
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        StatusMessage = "Export Excel (CSV) terminé.";
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (FilteredRows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "HTML pour PDF (*.html)|*.html",
            FileName = $"Resultats_{ClassDisplayName}_{PeriodLabel}.html".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, BuildHtmlDocument(), Encoding.UTF8);
            StatusMessage = "Fichier HTML exporté — ouvrez-le et imprimez en PDF.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Print()
    {
        if (FilteredRows.Count == 0)
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

            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Résultats par classe");
            StatusMessage = "Impression envoyée.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
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
                SelectedYear.Id,
                SelectedClassRoom.Id);
            ClassDisplayName = _periodContext.ClassDisplayName;
            RebuildPeriodOptions(selectDefault: true);
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

    private void RebuildPeriodOptions(bool selectDefault)
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

            var source = IsSubPeriodMode ? _periodContext.SubPeriods : _periodContext.MainPeriods;
            foreach (var option in source.OrderBy(o => o.OrderIndex).ThenBy(o => o.Name))
            {
                PeriodOptions.Add(option);
            }

            if (!selectDefault)
            {
                SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == SelectedPeriod?.Id)
                    ?? PeriodOptions.FirstOrDefault();
                return;
            }

            Guid? preferred = IsSubPeriodMode
                ? _periodContext.DefaultSubPeriodId
                : _periodContext.DefaultMainPeriodId;
            SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == preferred)
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
            StatusMessage = SelectedYear is null
                ? "Aucune année scolaire sélectionnée dans la barre de titre."
                : "Sélectionnez une classe et une période.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var mode = IsSubPeriodMode
                ? PedagogicalSheetPeriodMode.SubPeriod
                : PedagogicalSheetPeriodMode.MainPeriod;

            var sheet = await _gradeApi.GetClassResultsSheetAsync(
                SelectedYear.Id,
                SelectedClassRoom.Id,
                mode,
                SelectedPeriod.Id);

            ClassDisplayName = sheet.ClassDisplayName;
            PeriodLabel = sheet.SelectedPeriodLabel;
            SummaryClassAverage = sheet.Summary.ClassAverageDisplay;
            SummaryMax = sheet.Summary.MaxObtainedDisplay;
            SummaryMin = sheet.Summary.MinObtainedDisplay;
            StudentCount = sheet.Summary.StudentCount;
            GradedCount = sheet.Summary.GradedStudentCount;

            CourseColumns.Clear();
            foreach (var course in sheet.Courses)
            {
                CourseColumns.Add(course);
            }

            _allRows = sheet.Students
                .Select(s => ClassResultRowVm.FromDto(s, sheet.Courses.Count))
                .ToList();

            ApplyFilterAndSort();
            HasResults = FilteredRows.Count > 0;
            ColumnsChanged?.Invoke();
            StatusMessage = HasResults
                ? null
                : "Aucun résultat pour cette sélection.";
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

    private void ClearSheet()
    {
        CourseColumns.Clear();
        _allRows = [];
        FilteredRows.Clear();
        HasResults = false;
        StudentCount = 0;
        GradedCount = 0;
        SummaryClassAverage = "—";
        SummaryMax = "—";
        SummaryMin = "—";
        ColumnsChanged?.Invoke();
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<ClassResultRowVm> query = _allRows;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(r =>
                r.StudentName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || r.RegistrationNumber.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        query = (SelectedSort?.Field ?? ClassResultsSortField.Rank) switch
        {
            ClassResultsSortField.Name => query
                .OrderBy(r => r.StudentName, StringComparer.CurrentCultureIgnoreCase),
            ClassResultsSortField.Average => query
                .OrderByDescending(r => r.Average ?? decimal.MinValue)
                .ThenBy(r => r.StudentName, StringComparer.CurrentCultureIgnoreCase),
            ClassResultsSortField.Decision => query
                .OrderBy(r => r.Decision)
                .ThenBy(r => r.Rank == 0 ? int.MaxValue : r.Rank)
                .ThenBy(r => r.StudentName, StringComparer.CurrentCultureIgnoreCase),
            ClassResultsSortField.Mention => query
                .OrderBy(r => r.Mention ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.Rank == 0 ? int.MaxValue : r.Rank),
            _ => query
                .OrderBy(r => r.Rank == 0 ? int.MaxValue : r.Rank)
                .ThenBy(r => r.StudentName, StringComparer.CurrentCultureIgnoreCase)
        };

        FilteredRows.Clear();
        foreach (var row in query)
        {
            FilteredRows.Add(row);
        }

        HasResults = FilteredRows.Count > 0;
    }

    private FlowDocument BuildPrintDocument()
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11
        };

        doc.Blocks.Add(new Paragraph(new Run("Résultats scolaires — Feuille de classe"))
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        doc.Blocks.Add(new Paragraph(new Run($"{ClassDisplayName} — {PeriodLabel}"))
        {
            Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0 };
        var columnCount = 3 + CourseColumns.Count + 5;
        for (var i = 0; i < columnCount; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        table.RowGroups.Add(new TableRowGroup());
        var header = new TableRow();
        void AddHeader(string text) =>
            header.Cells.Add(new TableCell(new Paragraph(new Run(text)))
            {
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });

        AddHeader("Rang");
        AddHeader("Matricule");
        AddHeader("Nom");
        foreach (var course in CourseColumns)
        {
            AddHeader(course.CourseName);
        }

        AddHeader("Moy.");
        AddHeader("%");
        AddHeader("Mention");
        AddHeader("Décision");
        AddHeader("Statut");
        table.RowGroups[0].Rows.Add(header);

        foreach (var row in FilteredRows)
        {
            var tr = new TableRow();
            void AddCell(string text) =>
                tr.Cells.Add(new TableCell(new Paragraph(new Run(text)))
                {
                    Padding = new Thickness(4),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                });

            AddCell(row.RankDisplay);
            AddCell(row.RegistrationNumber);
            AddCell(row.StudentName);
            foreach (var cell in row.CourseDisplays)
            {
                AddCell(cell);
            }

            AddCell(row.AverageDisplay);
            AddCell(row.PercentageDisplay);
            AddCell(row.Mention ?? "—");
            AddCell(row.DecisionLabel);
            AddCell(row.StatusLabel);
            table.RowGroups[0].Rows.Add(tr);
        }

        doc.Blocks.Add(table);
        return doc;
    }

    private string BuildHtmlDocument()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Résultats scolaires</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;padding:24px}table{border-collapse:collapse;width:100%;font-size:12px}th,td{border:1px solid #cbd5e1;padding:6px 8px;text-align:left}th{background:#0f1f4a;color:#fff}.admis{color:#16a34a;font-weight:600}.ajourne{color:#ea580c;font-weight:600}.exclu{color:#dc2626;font-weight:600}.attente{color:#6b7280}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>Résultats scolaires — Feuille de classe</h1>");
        sb.Append("<h2>").Append(System.Net.WebUtility.HtmlEncode($"{ClassDisplayName} — {PeriodLabel}")).AppendLine("</h2>");
        sb.AppendLine("<table><thead><tr>");
        foreach (var h in new[] { "Rang", "Matricule", "Nom complet" }
                     .Concat(CourseColumns.Select(c => c.CourseName))
                     .Concat(["Moyenne générale", "Pourcentage", "Mention", "Décision", "Statut"]))
        {
            sb.Append("<th>").Append(System.Net.WebUtility.HtmlEncode(h)).Append("</th>");
        }

        sb.AppendLine("</tr></thead><tbody>");
        foreach (var row in FilteredRows)
        {
            var decisionClass = row.Decision switch
            {
                ClassCouncilDecision.Admis => "admis",
                ClassCouncilDecision.Ajourne => "ajourne",
                ClassCouncilDecision.Exclu => "exclu",
                _ => "attente"
            };
            sb.Append("<tr>");
            void Td(string v) => sb.Append("<td>").Append(System.Net.WebUtility.HtmlEncode(v)).Append("</td>");
            Td(row.RankDisplay);
            Td(row.RegistrationNumber);
            Td(row.StudentName);
            foreach (var cell in row.CourseDisplays)
            {
                Td(cell);
            }

            Td(row.AverageDisplay);
            Td(row.PercentageDisplay);
            Td(row.Mention ?? "—");
            sb.Append("<td class=\"").Append(decisionClass).Append("\">")
                .Append(System.Net.WebUtility.HtmlEncode(row.DecisionLabel)).Append("</td>");
            Td(row.StatusLabel);
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></body></html>");
        return sb.ToString();
    }

    private void OnGlobalAcademicYearChanged()
    {
        if (_lookups is null)
        {
            return;
        }

        var year = AcademicYearRefreshBridge.SelectedYear;
        if (year is null)
        {
            return;
        }

        if (SelectedYear?.Id == year.Id)
        {
            return;
        }

        SelectedYear = year;
    }

    partial void OnSelectedYearChanged(AcademicYearDto? value)
    {
        if (!_filtersReady)
        {
            return;
        }

        RefreshClassRooms();
        _ = LoadPeriodContextAndSheetAsync();
    }

    partial void OnSelectedClassRoomChanged(ClassRoomLookupDto? value)
    {
        if (!_filtersReady)
        {
            return;
        }

        _ = LoadPeriodContextAndSheetAsync();
    }

    partial void OnIsSubPeriodModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PeriodModeLabel));
        if (!_filtersReady || _periodContext is null)
        {
            return;
        }

        RebuildPeriodOptions(selectDefault: true);
        _ = LoadSheetAsync();
    }

    partial void OnSelectedPeriodChanged(PedagogicalSheetPeriodOptionDto? value)
    {
        if (!_filtersReady || _suppressReload)
        {
            return;
        }

        _ = LoadSheetAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

    partial void OnSelectedSortChanged(ClassResultsSortOption? value) => ApplyFilterAndSort();

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public sealed record ClassResultsSortOption(ClassResultsSortField Field, string Label)
{
    public override string ToString() => Label;
}

public sealed class ClassResultRowVm
{
    public required Guid StudentId { get; init; }
    public required string RegistrationNumber { get; init; }
    public required string StudentName { get; init; }
    public required int Rank { get; init; }
    public required bool IsTied { get; init; }
    public required IReadOnlyList<string> CourseDisplays { get; init; }
    public required decimal? Average { get; init; }
    public required decimal? Percentage { get; init; }
    public required string AverageDisplay { get; init; }
    public required string PercentageDisplay { get; init; }
    public required string? Mention { get; init; }
    public required ClassCouncilDecision Decision { get; init; }
    public required string DecisionLabel { get; init; }
    public required string StatusLabel { get; init; }

    public string RankDisplay => Rank <= 0
        ? "—"
        : IsTied
            ? $"{Rank} ="
            : Rank.ToString(CultureInfo.InvariantCulture);

    public Brush DecisionBrush => Decision switch
    {
        ClassCouncilDecision.Admis => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
        ClassCouncilDecision.Ajourne => new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)),
        ClassCouncilDecision.Exclu => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
        _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
    };

    public string this[int courseIndex] =>
        courseIndex >= 0 && courseIndex < CourseDisplays.Count ? CourseDisplays[courseIndex] : "—";

    public static ClassResultRowVm FromDto(ClassResultsStudentRowDto dto, int expectedCourseCount)
    {
        var displays = dto.CourseCells.Select(c => c.Display).ToList();
        while (displays.Count < expectedCourseCount)
        {
            displays.Add("—");
        }

        return new ClassResultRowVm
        {
            StudentId = dto.StudentId,
            RegistrationNumber = dto.RegistrationNumber,
            StudentName = dto.StudentName,
            Rank = dto.Rank,
            IsTied = dto.IsTied,
            CourseDisplays = displays,
            Average = dto.Average,
            Percentage = dto.Percentage,
            AverageDisplay = dto.AverageDisplay,
            PercentageDisplay = dto.PercentageDisplay,
            Mention = dto.Mention,
            Decision = dto.Decision,
            DecisionLabel = dto.DecisionLabel,
            StatusLabel = dto.StatusLabel
        };
    }
}
