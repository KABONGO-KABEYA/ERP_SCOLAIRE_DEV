using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Helpers;
using SchoolManagement.Desktop.Models;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public sealed record ReportPeriodOption(RealizedReceiptsPeriodKind Kind, string Label);

public sealed record ReportMonthOption(int Month, string Label);

public sealed record ReportCalendarYearOption(int Year, string Label);

public sealed record ReportClassOption(Guid Id, string DisplayName, Guid SectionId, string SectionName);

/// <summary>Rapports financiers — recettes réalisées (journalier / hebdo / mensuel / période).</summary>
public partial class FinancialReportsViewModel : ViewModelBase
{
    private readonly IReportApiService _reportApi;
    private readonly IRevenueAllocationApiService _allocationApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly IAcademicApiService _academicApi;
    private readonly IEnrollmentWizardApiService _wizardApi;
    private readonly IPromoterDashboardApiService _dashboardApi;
    private readonly IDocumentBrandingApiService _brandingApi;
    private readonly IDocumentBrandingPathResolver _brandingPathResolver;
    private bool _suppressPeriodReload;
    private bool _suppressFilterReload;
    private bool _suppressMonthReload;
    private List<ReportClassOption> _allClassRooms = [];
    private HashSet<string> _organizedSectionNames = new(StringComparer.OrdinalIgnoreCase);
    private Guid? _structureAcademicYearId;
    private Guid? _defaultFeeTypeId;

    public FinancialReportsViewModel(
        IReportApiService reportApi,
        IRevenueAllocationApiService allocationApi,
        ISchoolApiService schoolApi,
        ISchoolFeeApiService schoolFeeApi,
        IAcademicApiService academicApi,
        IEnrollmentWizardApiService wizardApi,
        IPromoterDashboardApiService dashboardApi,
        IDocumentBrandingApiService brandingApi,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        _reportApi = reportApi;
        _allocationApi = allocationApi;
        _schoolApi = schoolApi;
        _schoolFeeApi = schoolFeeApi;
        _academicApi = academicApi;
        _wizardApi = wizardApi;
        _dashboardApi = dashboardApi;
        _brandingApi = brandingApi;
        _brandingPathResolver = brandingPathResolver;
        PeriodOptions =
        [
            new ReportPeriodOption(RealizedReceiptsPeriodKind.Day, "Journalier"),
            new ReportPeriodOption(RealizedReceiptsPeriodKind.Week, "Hebdomadaire"),
            new ReportPeriodOption(RealizedReceiptsPeriodKind.Month, "Mensuel"),
            new ReportPeriodOption(RealizedReceiptsPeriodKind.Custom, "Période définie")
        ];

        MonthOptions =
        [
            new ReportMonthOption(1, "Janvier"),
            new ReportMonthOption(2, "Février"),
            new ReportMonthOption(3, "Mars"),
            new ReportMonthOption(4, "Avril"),
            new ReportMonthOption(5, "Mai"),
            new ReportMonthOption(6, "Juin"),
            new ReportMonthOption(7, "Juillet"),
            new ReportMonthOption(8, "Août"),
            new ReportMonthOption(9, "Septembre"),
            new ReportMonthOption(10, "Octobre"),
            new ReportMonthOption(11, "Novembre"),
            new ReportMonthOption(12, "Décembre")
        ];

        var currentYear = DateTime.Today.Year;
        for (var year = currentYear - 5; year <= currentYear + 1; year++)
        {
            CalendarYears.Add(new ReportCalendarYearOption(year, year.ToString()));
        }

        _suppressMonthReload = true;
        SelectedMonth = MonthOptions.First(m => m.Month == DateTime.Today.Month);
        SelectedCalendarYear = CalendarYears.FirstOrDefault(y => y.Year == currentYear) ?? CalendarYears.LastOrDefault();
        _suppressMonthReload = false;

        SelectedPeriod = PeriodOptions[2]; // Mensuel par défaut → plus de données visibles
        ApplyPeriodDates(SelectedPeriod.Kind);
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
    }

    private void OnGlobalAcademicYearChanged()
    {
        if (!IsInitialized || _suppressFilterReload)
        {
            return;
        }

        _ = ReloadClassRoomsAsync();
        _ = SearchAsync();
    }

    public ObservableCollection<ReportPeriodOption> PeriodOptions { get; }

    public ObservableCollection<ReportMonthOption> MonthOptions { get; }

    public ObservableCollection<ReportCalendarYearOption> CalendarYears { get; } = [];

    public ObservableCollection<SectionDto> Sections { get; } = [];

    public ObservableCollection<FeeTypeDto> FeeTypes { get; } = [];

    public ObservableCollection<ReportClassOption> ClassRooms { get; } = [];

    public ObservableCollection<RealizedReceiptsDailyBucketDto> DailyBuckets { get; } = [];

    public ObservableCollection<RealizedReceiptsByCurrencyDto> ByCurrency { get; } = [];

    public ObservableCollection<RealizedReceiptsByClassDto> ByClass { get; } = [];

    public ObservableCollection<RealizedReceiptsByFeeTypeDto> ByFeeType { get; } = [];

    public ObservableCollection<RealizedReceiptsBySectionDto> BySection { get; } = [];

    public ObservableCollection<ReportDailyGroupRow<RealizedReceiptsDailyByClassDto>> DailyByClassGroups { get; } = [];

    public ObservableCollection<ReportDailyGroupRow<RealizedReceiptsDailyByFeeTypeDto>> DailyByFeeTypeGroups { get; } = [];

    public ObservableCollection<ReportDailyGroupRow<RealizedReceiptsDailyBySectionDto>> DailyBySectionGroups { get; } = [];

    [ObservableProperty] private decimal _byClassTotal;
    [ObservableProperty] private int _byClassPaymentCount;
    [ObservableProperty] private decimal _bySectionTotal;
    [ObservableProperty] private int _bySectionPaymentCount;
    [ObservableProperty] private decimal _byFeeTypeTotal;
    [ObservableProperty] private int _byFeeTypePaymentCount;

    public ObservableCollection<AllocationCashFlowRowDto> AllocationGlobalRows { get; } = [];

    public ObservableCollection<AllocationCashFlowDailyGroupRow> AllocationDailyGroups { get; } = [];

    public ObservableCollection<AllocationCashFlowRowDto> AllocationTotalsByCurrency { get; } = [];

    public ObservableCollection<WithholdingReportTypeGroupRow> WithholdingGroups { get; } = [];

    public ObservableCollection<SchoolManagement.Application.Dashboard.DTOs.FeeInstallmentReceivableDto> ReceivablesByInstallment { get; } = [];

    public ObservableCollection<SchoolManagement.Application.Dashboard.DTOs.FeeDestinationReceivableDto> ReceivablesByDestination { get; } = [];

    public ObservableCollection<SchoolManagement.Application.Dashboard.DTOs.DashboardDebtorLineDto> ReceivableDebtors { get; } = [];

    [ObservableProperty] private string _receivablesTitle = "Suivi des créances — promoteur";
    [ObservableProperty] private string _receivablesAcademicYear = string.Empty;
    [ObservableProperty] private string _receivablesCurrency = string.Empty;
    [ObservableProperty] private decimal _receivablesExpected;
    [ObservableProperty] private decimal _receivablesPaid;
    [ObservableProperty] private decimal _receivablesRemaining;

    [ObservableProperty] private decimal _withholdingGrandTotal;
    [ObservableProperty] private int _withholdingPaymentCount;

    [ObservableProperty] private ReportPeriodOption? _selectedPeriod;
    [ObservableProperty] private ReportMonthOption? _selectedMonth;
    [ObservableProperty] private ReportCalendarYearOption? _selectedCalendarYear;
    [ObservableProperty] private SectionDto? _filterSection;
    [ObservableProperty] private FeeTypeDto? _filterFeeType;
    [ObservableProperty] private ReportClassOption? _filterClassRoom;
    [ObservableProperty] private DateTime? _filterFromDate;
    [ObservableProperty] private DateTime? _filterToDate;
    [ObservableProperty] private string? _fromDateError;
    [ObservableProperty] private string? _toDateError;
    public ObservableCollection<DailyPivotGroupRow> DailyPivotGroups { get; } = [];

    [ObservableProperty] private DataView? _pivotView;
    [ObservableProperty] private decimal _grandTotal;
    [ObservableProperty] private int _paymentCount;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isFiltersExpanded = true;
    [ObservableProperty] private int _selectedReportTabIndex;

    public bool IsCustomPeriod => SelectedPeriod?.Kind == RealizedReceiptsPeriodKind.Custom;

    public bool IsMonthPeriod => SelectedPeriod?.Kind == RealizedReceiptsPeriodKind.Month;

    public bool IsReceivablesTabSelected => SelectedReportTabIndex == 6;

    public string FiltersToggleLabel => IsFiltersExpanded ? "Masquer les filtres" : "Afficher les filtres";

    public string FiltersHeaderText => IsCustomPeriod
        ? "Filtres — période définie"
        : IsMonthPeriod
            ? $"Filtres — mensuel ({SelectedMonth?.Label ?? "mois"} {SelectedCalendarYear?.Year})"
            : $"Filtres — {SelectedPeriod?.Label ?? "rapport"}";

    public string PeriodLabel => SelectedPeriod?.Kind switch
    {
        RealizedReceiptsPeriodKind.Day => "Recettes du jour",
        RealizedReceiptsPeriodKind.Week => "Recettes de la semaine",
        RealizedReceiptsPeriodKind.Month => SelectedMonth is null
            ? "Recettes du mois"
            : $"Recettes de {SelectedMonth.Label.ToLowerInvariant()} {SelectedCalendarYear?.Year}",
        RealizedReceiptsPeriodKind.Custom => "Recettes sur période",
        _ => "Recettes réalisées"
    };

    partial void OnIsFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(FiltersToggleLabel));

    partial void OnSelectedReportTabIndexChanged(int value) => OnPropertyChanged(nameof(IsReceivablesTabSelected));

    partial void OnSelectedPeriodChanged(ReportPeriodOption? value)
    {
        OnPropertyChanged(nameof(IsCustomPeriod));
        OnPropertyChanged(nameof(IsMonthPeriod));
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(FiltersHeaderText));
        ClearDateErrors();
        if (value is null || _suppressPeriodReload)
        {
            return;
        }

        if (value.Kind != RealizedReceiptsPeriodKind.Custom)
        {
            ApplyPeriodDates(value.Kind);
        }

        if (IsInitialized)
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedMonthChanged(ReportMonthOption? value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(FiltersHeaderText));
        if (_suppressMonthReload || !IsMonthPeriod)
        {
            return;
        }

        ApplySelectedMonthDates();
        if (IsInitialized)
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedCalendarYearChanged(ReportCalendarYearOption? value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(FiltersHeaderText));
        if (_suppressMonthReload || !IsMonthPeriod)
        {
            return;
        }

        ApplySelectedMonthDates();
        if (IsInitialized)
        {
            _ = SearchAsync();
        }
    }

    partial void OnFilterFeeTypeChanged(FeeTypeDto? value)
    {
        if (_suppressFilterReload || !IsInitialized)
        {
            return;
        }

        _ = SearchAsync();
    }

    partial void OnFilterSectionChanged(SectionDto? value)
    {
        if (_suppressFilterReload)
        {
            return;
        }

        ApplyClassRoomFilter();
        if (IsInitialized)
        {
            _ = SearchAsync();
        }
    }

    partial void OnFilterFromDateChanged(DateTime? value)
    {
        if (IsCustomPeriod)
        {
            ValidateDates(showStatus: false);
        }
    }

    partial void OnFilterToDateChanged(DateTime? value)
    {
        if (IsCustomPeriod)
        {
            ValidateDates(showStatus: false);
        }
    }

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    public async Task EnsureInitializedAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Sections.Clear();
            _organizedSectionNames.Clear();
            var structure = await _wizardApi.GetStructureOptionsAsync();
            _structureAcademicYearId = structure.AcademicYearId;
            foreach (var section in structure.Sections.OrderBy(s => s.Name))
            {
                Sections.Add(section);
                _organizedSectionNames.Add(section.Name.Trim());
            }

            FeeTypes.Clear();
            var catalog = await _schoolFeeApi.GetCatalogAsync();
            foreach (var feeType in catalog.FeeTypes.Where(f => f.IsActive).OrderBy(f => f.Name))
            {
                FeeTypes.Add(feeType);
            }

            var school = await _schoolApi.GetCurrentSchoolAsync();
            _defaultFeeTypeId = school?.DefaultFeeTypeId;
            _suppressPeriodReload = true;
            _suppressFilterReload = true;
            FilterFeeType = DefaultFeeTypeHelper.Resolve(FeeTypes, _defaultFeeTypeId);
            ApplyPeriodDates(SelectedPeriod?.Kind ?? RealizedReceiptsPeriodKind.Day);
            _suppressFilterReload = false;
            _suppressPeriodReload = false;

            await ReloadClassRoomsAsync(structure);

            IsInitialized = true;
            await SearchAsync();
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
    private async Task SearchAsync()
    {
        if (!ValidateDates(showStatus: true))
        {
            return;
        }

        if (FilterFeeType is null)
        {
            StatusMessage = "Sélectionnez un type de frais pour afficher le rapport.";
            PivotView = null;
            DailyPivotGroups.Clear();
            AllocationGlobalRows.Clear();
            AllocationDailyGroups.Clear();
            AllocationTotalsByCurrency.Clear();
            WithholdingGroups.Clear();
            WithholdingGrandTotal = 0;
            WithholdingPaymentCount = 0;
            return;
        }

        IsBusy = true;
        try
        {
            var reportTask = _reportApi.GetRealizedReceiptsAsync(BuildRequest());
            var allocationTask = _allocationApi.GetAllocationCashFlowAsync(BuildAllocationRequest());
            var withholdingTask = _allocationApi.GetWithholdingReportAsync(BuildAllocationRequest());
            var receivablesTask = _dashboardApi.GetReceivablesBreakdownAsync(FilterFeeType.Id);
            await Task.WhenAll(reportTask, allocationTask, withholdingTask, receivablesTask);

            var result = await reportTask;
            ApplyPivot(result);
            ApplyDailyPivot(result);
            ApplyAllocationCashFlow(await allocationTask);
            ApplyWithholdingReport(await withholdingTask);
            ApplyReceivables(await receivablesTask);

            DailyBuckets.Clear();
            foreach (var bucket in result.DailyBuckets)
            {
                DailyBuckets.Add(bucket);
            }

            ByCurrency.Clear();
            foreach (var total in result.ByCurrency)
            {
                ByCurrency.Add(total);
            }

            ByClass.Clear();
            foreach (var item in result.ByClass)
            {
                ByClass.Add(item);
            }

            ByClassTotal = ByClass.Sum(x => x.TotalAmount);
            ByClassPaymentCount = ByClass.Sum(x => x.PaymentCount);

            ByFeeType.Clear();
            foreach (var item in result.ByFeeType)
            {
                ByFeeType.Add(item);
            }

            ByFeeTypeTotal = ByFeeType.Sum(x => x.TotalAmount);
            ByFeeTypePaymentCount = ByFeeType.Sum(x => x.PaymentCount);

            BySection.Clear();
            foreach (var item in result.BySection)
            {
                BySection.Add(item);
            }

            BySectionTotal = BySection.Sum(x => x.TotalAmount);
            BySectionPaymentCount = BySection.Sum(x => x.PaymentCount);

            DailyByClassGroups.Clear();
            foreach (var group in result.DailyByClass
                         .GroupBy(x => x.Date)
                         .OrderBy(g => g.Key))
            {
                var rows = group.OrderBy(x => x.ClassName).ToList();
                DailyByClassGroups.Add(new ReportDailyGroupRow<RealizedReceiptsDailyByClassDto>
                {
                    Date = group.Key,
                    Rows = rows,
                    DayTotal = rows.Sum(x => x.TotalAmount)
                });
            }

            DailyByFeeTypeGroups.Clear();
            foreach (var group in result.DailyByFeeType
                         .GroupBy(x => x.Date)
                         .OrderBy(g => g.Key))
            {
                var rows = group.OrderBy(x => x.FeeTypeName).ToList();
                DailyByFeeTypeGroups.Add(new ReportDailyGroupRow<RealizedReceiptsDailyByFeeTypeDto>
                {
                    Date = group.Key,
                    Rows = rows,
                    DayTotal = rows.Sum(x => x.TotalAmount)
                });
            }

            DailyBySectionGroups.Clear();
            foreach (var group in result.DailyBySection
                         .GroupBy(x => x.Date)
                         .OrderBy(g => g.Key))
            {
                var rows = group.OrderBy(x => x.SectionName).ToList();
                DailyBySectionGroups.Add(new ReportDailyGroupRow<RealizedReceiptsDailyBySectionDto>
                {
                    Date = group.Key,
                    Rows = rows,
                    DayTotal = rows.Sum(x => x.TotalAmount)
                });
            }

            GrandTotal = result.GrandTotal;
            PaymentCount = result.PaymentCount;
            StatusMessage = result.PaymentCount == 0
                ? "Aucune recette pour cette période. Élargissez la période ou vérifiez le type de frais."
                : $"{result.PivotRows.Count} élève(s) — total {result.GrandTotal:N2}";
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

    private void ApplyPivot(RealizedReceiptsResultDto result)
    {
        var table = new DataTable();
        table.Columns.Add("Nom complet", typeof(string));
        table.Columns.Add("Classe", typeof(string));
        foreach (var column in result.InstallmentColumns)
        {
            table.Columns.Add(column.InstallmentName, typeof(decimal));
        }

        table.Columns.Add("Total", typeof(decimal));

        foreach (var row in result.PivotRows)
        {
            var values = new object[2 + row.InstallmentAmounts.Count + 1];
            values[0] = row.StudentName;
            values[1] = row.ClassName;
            for (var i = 0; i < row.InstallmentAmounts.Count; i++)
            {
                values[2 + i] = row.InstallmentAmounts[i];
            }

            values[^1] = row.RowTotal;
            table.Rows.Add(values);
        }

        PivotView = table.DefaultView;
    }

    private void ApplyDailyPivot(RealizedReceiptsResultDto result)
    {
        DailyPivotGroups.Clear();

        foreach (var dateGroup in result.DailyPivotRows.GroupBy(r => r.Date).OrderBy(g => g.Key))
        {
            var table = CreateDailyPivotTable(result.InstallmentColumns);
            foreach (var row in dateGroup.OrderBy(r => r.ClassName).ThenBy(r => r.StudentName))
            {
                var values = new object[2 + row.InstallmentDetails.Count + 1];
                values[0] = row.StudentName;
                values[1] = row.ClassName;
                for (var i = 0; i < row.InstallmentDetails.Count; i++)
                {
                    values[2 + i] = string.IsNullOrWhiteSpace(row.InstallmentDetails[i])
                        ? "—"
                        : row.InstallmentDetails[i];
                }

                values[^1] = row.RowTotal;
                table.Rows.Add(values);
            }

            DailyPivotGroups.Add(new DailyPivotGroupRow
            {
                Date = dateGroup.Key,
                Rows = table.DefaultView
            });
        }
    }

    private static DataTable CreateDailyPivotTable(
        IReadOnlyList<RealizedReceiptsInstallmentColumnDto> installmentColumns)
    {
        var table = new DataTable();
        table.Columns.Add("Nom complet", typeof(string));
        table.Columns.Add("Classe", typeof(string));
        foreach (var column in installmentColumns)
        {
            table.Columns.Add(column.InstallmentName, typeof(string));
        }

        table.Columns.Add("Total", typeof(decimal));
        return table;
    }

    private void ApplyAllocationCashFlow(AllocationCashFlowResultDto result)
    {
        AllocationGlobalRows.Clear();
        foreach (var row in result.GlobalRows)
        {
            AllocationGlobalRows.Add(row);
        }

        AllocationDailyGroups.Clear();
        foreach (var group in result.DailyGroups)
        {
            AllocationDailyGroups.Add(new AllocationCashFlowDailyGroupRow
            {
                Date = group.Date,
                Rows = group.Rows
            });
        }

        AllocationTotalsByCurrency.Clear();
        foreach (var total in result.TotalsByCurrency)
        {
            AllocationTotalsByCurrency.Add(total);
        }
    }

    private void ApplyWithholdingReport(WithholdingReportResultDto result)
    {
        WithholdingGroups.Clear();
        foreach (var group in result.Groups)
        {
            WithholdingGroups.Add(new WithholdingReportTypeGroupRow
            {
                WithholdingTypeId = group.WithholdingTypeId,
                WithholdingTypeCode = group.WithholdingTypeCode,
                WithholdingTypeName = group.WithholdingTypeName,
                TypeTotal = group.TypeTotal,
                Students = group.Students
            });
        }

        WithholdingGrandTotal = result.GrandTotal;
        WithholdingPaymentCount = result.PaymentCount;
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        _suppressPeriodReload = true;
        _suppressFilterReload = true;
        SelectedPeriod = PeriodOptions[2]; // Mensuel — évite un jour vide hors encaissements
        FilterFeeType = DefaultFeeTypeHelper.Resolve(FeeTypes, _defaultFeeTypeId);
        FilterSection = null;
        FilterClassRoom = null;
        _suppressMonthReload = true;
        SelectedMonth = MonthOptions.First(m => m.Month == DateTime.Today.Month);
        SelectedCalendarYear = CalendarYears.FirstOrDefault(y => y.Year == DateTime.Today.Year)
            ?? CalendarYears.LastOrDefault();
        _suppressMonthReload = false;
        ApplyPeriodDates(RealizedReceiptsPeriodKind.Month);
        ClearDateErrors();
        _suppressFilterReload = false;
        _suppressPeriodReload = false;
        await ReloadClassRoomsAsync();
        await SearchAsync();
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (!ValidateDates(showStatus: true))
        {
            return;
        }

        if (FilterFeeType is null)
        {
            StatusMessage = "Sélectionnez un type de frais pour exporter le rapport.";
            return;
        }

        try
        {
            if (IsReceivablesTabSelected)
            {
                QuestPDF.Settings.License = LicenseType.Community;
                SchoolManagement.Desktop.Printing.DocumentPreview.ShowPdf(
                    await BuildReceivablesPdfAsync(), "Créances promoteur");
                StatusMessage = "Aperçu des créances promoteur fermé.";
                return;
            }

            var bytes = await _reportApi.ExportRealizedReceiptsPdfAsync(BuildRequest());
            SchoolManagement.Desktop.Printing.DocumentPreview.ShowPdf(bytes, "Recettes réalisées");
            StatusMessage = "Aperçu fermé.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PreviewAllocationAsync()
    {
        if (!ValidateDates(showStatus: true)) return;
        try
        {
            var bytes = await _allocationApi.ExportPdfAsync(BuildAllocationRequest());
            SchoolManagement.Desktop.Printing.DocumentPreview.ShowPdf(bytes, "Répartition des recettes");
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private void ApplyReceivables(SchoolManagement.Application.Dashboard.DTOs.FeeReceivablesBreakdownDto result)
    {
        ReceivablesByInstallment.Clear();
        foreach (var row in result.ByInstallment) ReceivablesByInstallment.Add(row);
        ReceivablesByDestination.Clear();
        foreach (var row in result.ByDestination) ReceivablesByDestination.Add(row);
        ReceivableDebtors.Clear();
        foreach (var row in result.Debtors) ReceivableDebtors.Add(row);
        ReceivablesTitle = $"Suivi des créances — {result.FeeTypeName}";
        ReceivablesAcademicYear = result.AcademicYearLabel;
        ReceivablesCurrency = result.Currency;
        ReceivablesExpected = result.TotalExpected;
        ReceivablesPaid = result.TotalPaid;
        ReceivablesRemaining = result.TotalRemaining;
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (!ValidateDates(showStatus: true))
        {
            return;
        }

        if (FilterFeeType is null)
        {
            StatusMessage = "Sélectionnez un type de frais pour exporter le rapport.";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = IsReceivablesTabSelected
                    ? $"creances-promoteur-{DateTime.Today:yyyyMMdd}.xlsx"
                    : $"recettes-realisees-{FilterFromDate:yyyyMMdd}-{FilterToDate:yyyyMMdd}.xlsx",
                Filter = "Excel|*.xlsx"
            };
            if (ErpFileDialog.ShowSave(dialog) != true)
            {
                return;
            }

            var bytes = IsReceivablesTabSelected
                ? await BuildReceivablesExcelAsync()
                : await _reportApi.ExportRealizedReceiptsExcelAsync(BuildRequest());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusMessage = $"Export Excel enregistré : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task<byte[]> BuildReceivablesExcelAsync()
    {
        var school = await _schoolApi.GetCurrentSchoolAsync();
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Créances promoteur");
            sheet.Column(1).Width = 30;
            sheet.Column(2).Width = 16;
            sheet.Column(3).Width = 16;
            sheet.Column(4).Width = 16;
            sheet.Column(5).Width = 16;
            sheet.Range("A1:E1").Merge().Value = school?.Name ?? "Établissement scolaire";
            sheet.Range("A2:E2").Merge().Value = ReceivablesTitle.ToUpperInvariant();
            sheet.Range("A3:E3").Merge().Value = $"Année scolaire : {ReceivablesAcademicYear}   •   Devise : {ReceivablesCurrency}";
            sheet.Range("A1:E1").Style.Font.Bold = true;
            sheet.Range("A1:E1").Style.Font.FontSize = 16;
            sheet.Range("A1:E1").Style.Font.FontColor = XLColor.White;
            sheet.Range("A1:E1").Style.Fill.BackgroundColor = XLColor.FromHtml("#17365D");
            sheet.Range("A2:E2").Style.Font.Bold = true;
            sheet.Range("A2:E2").Style.Font.FontSize = 13;
            sheet.Range("A2:E2").Style.Font.FontColor = XLColor.FromHtml("#17365D");
            sheet.Range("A1:E3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Range("A1:E3").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Row(1).Height = 28;
            sheet.Row(2).Height = 23;
            sheet.Row(3).Height = 20;

            sheet.Range("A5:E5").Merge().Value = "SYNTHÈSE";
            sheet.Range("A5:E5").Style.Font.Bold = true;
            sheet.Range("A5:E5").Style.Font.FontColor = XLColor.White;
            sheet.Range("A5:E5").Style.Fill.BackgroundColor = XLColor.FromHtml("#2F75B5");
            sheet.Cell(6, 1).Value = "Attendu"; sheet.Cell(6, 2).Value = ReceivablesExpected;
            sheet.Cell(6, 3).Value = "Perçu"; sheet.Cell(6, 4).Value = ReceivablesPaid;
            sheet.Cell(6, 5).Value = "Reste"; sheet.Cell(7, 5).Value = ReceivablesRemaining;
            sheet.Range("A6:E7").Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF2F8");
            sheet.Range("A6:E7").Style.Font.Bold = true;
            sheet.Range("B6:B7,D6:D7,E6:E7").Style.NumberFormat.Format = "#,##0.00";

            var row = 9;
            sheet.Range($"A{row}:E{row}").Merge().Value = "PAR TRANCHE";
            sheet.Range($"A{row}:E{row}").Style.Font.Bold = true;
            sheet.Range($"A{row}:E{row}").Style.Font.FontColor = XLColor.White;
            sheet.Range($"A{row}:E{row}").Style.Fill.BackgroundColor = XLColor.FromHtml("#2F75B5");
            row++;
            WriteReceivableHeader(sheet, row, "Tranche", "Attendu", "Perçu", "Reste");
            StyleReceivableHeader(sheet, row, 4);
            row++;
            foreach (var item in ReceivablesByInstallment)
            {
                sheet.Cell(row, 1).Value = item.InstallmentName;
                sheet.Cell(row, 2).Value = item.AmountExpected;
                sheet.Cell(row, 3).Value = item.AmountPaid;
                sheet.Cell(row, 4).Value = item.Remaining;
                row++;
            }
            StyleReceivableBody(sheet, row - ReceivablesByInstallment.Count, row - 1, 4);

            row++;
            sheet.Range($"A{row}:E{row}").Merge().Value = "PAR COMPTE DE RÉPARTITION";
            sheet.Range($"A{row}:E{row}").Style.Font.Bold = true;
            sheet.Range($"A{row}:E{row}").Style.Font.FontColor = XLColor.White;
            sheet.Range($"A{row}:E{row}").Style.Fill.BackgroundColor = XLColor.FromHtml("#2F75B5");
            row++;
            WriteReceivableHeader(sheet, row, "Compte", "%", "Attendu", "Encaissé", "Reste");
            StyleReceivableHeader(sheet, row, 5);
            row++;
            foreach (var item in ReceivablesByDestination)
            {
                sheet.Cell(row, 1).Value = item.DestinationName;
                sheet.Cell(row, 2).Value = item.Percentage;
                sheet.Cell(row, 3).Value = item.AmountExpected;
                sheet.Cell(row, 4).Value = item.AmountCollected;
                sheet.Cell(row, 5).Value = item.Remaining;
                row++;
            }
            StyleReceivableBody(sheet, row - ReceivablesByDestination.Count, row - 1, 5);

            sheet.RangeUsed()!.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.RangeUsed()!.Style.Border.OutsideBorderColor = XLColor.FromHtml("#B8C7D9");
            sheet.RangeUsed()!.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            sheet.RangeUsed()!.Style.Border.InsideBorderColor = XLColor.FromHtml("#D9E2F3");
            sheet.SheetView.FreezeRows(3);
            workbook.SaveAs(stream);
        }
        return stream.ToArray();
    }

    private static void StyleReceivableHeader(IXLWorksheet sheet, int row, int count)
    {
        var range = sheet.Range(row, 1, row, count);
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#5B9BD5");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void StyleReceivableBody(IXLWorksheet sheet, int firstRow, int lastRow, int count)
    {
        if (lastRow < firstRow) return;
        var range = sheet.Range(firstRow, 1, lastRow, count);
        range.Style.NumberFormat.Format = "#,##0.00";
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        for (var row = firstRow; row <= lastRow; row++)
        {
            if ((row - firstRow) % 2 == 0) sheet.Range(row, 1, row, count).Style.Fill.BackgroundColor = XLColor.FromHtml("#F7FAFC");
        }
    }

    private static void WriteReceivableHeader(IXLWorksheet sheet, int row, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(row, i + 1).Value = headers[i];
            sheet.Cell(row, i + 1).Style.Font.Bold = true;
        }
    }

    private async Task<byte[]> BuildReceivablesPdfAsync()
    {
        var school = await _schoolApi.GetCurrentSchoolAsync();
        var headerImage = await LoadReceivablesHeaderImageAsync();
        var schoolName = school?.Name ?? "Établissement scolaire";
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor("#24364B"));
            page.Header().Column(header =>
            {
                header.Spacing(5);
                if (headerImage is not null) header.Item().MaxHeight(72).Image(headerImage).FitArea();
                header.Item().Text(schoolName.ToUpperInvariant()).FontSize(10).Bold().FontColor("#17365D").AlignCenter();
                header.Item().LineHorizontal(1.2f).LineColor("#2F75B5");
            });
            page.Content().Column(column =>
            {
                column.Spacing(14);
                column.Item().PaddingTop(8).Text(ReceivablesTitle.ToUpperInvariant()).FontSize(17).Bold().FontColor("#17365D");
                column.Item().Text($"Année scolaire : {ReceivablesAcademicYear}   •   Devise : {ReceivablesCurrency}").FontSize(10).FontColor("#5B6573");
                column.Item().Row(row =>
                {
                    SummaryCard(row, "ATTENDU", ReceivablesExpected, "#D9EAF7");
                    SummaryCard(row, "PERÇU", ReceivablesPaid, "#E2F0D9");
                    SummaryCard(row, "RESTE À PERCEVOIR", ReceivablesRemaining, "#FCE4D6");
                });
                column.Item().Text("Par tranche").FontSize(13).Bold().FontColor("#17365D");
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                    table.Header(header => { PdfHeaderCell(header.Cell(), "Tranche"); PdfHeaderCell(header.Cell(), "Attendu"); PdfHeaderCell(header.Cell(), "Perçu"); PdfHeaderCell(header.Cell(), "Reste"); });
                    foreach (var item in ReceivablesByInstallment) { PdfBodyCell(table.Cell(), item.InstallmentName); PdfBodyCell(table.Cell(), $"{item.AmountExpected:N2}", true); PdfBodyCell(table.Cell(), $"{item.AmountPaid:N2}", true); PdfBodyCell(table.Cell(), $"{item.Remaining:N2}", true, "#FFF2CC"); }
                });
                column.Item().Text("Par compte de répartition").FontSize(13).Bold().FontColor("#17365D");
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                    table.Header(header => { PdfHeaderCell(header.Cell(), "Compte"); PdfHeaderCell(header.Cell(), "%"); PdfHeaderCell(header.Cell(), "Attendu"); PdfHeaderCell(header.Cell(), "Encaissé"); PdfHeaderCell(header.Cell(), "Reste"); });
                    foreach (var item in ReceivablesByDestination) { PdfBodyCell(table.Cell(), item.DestinationName); PdfBodyCell(table.Cell(), $"{item.Percentage:N1}", true); PdfBodyCell(table.Cell(), $"{item.AmountExpected:N2}", true); PdfBodyCell(table.Cell(), $"{item.AmountCollected:N2}", true); PdfBodyCell(table.Cell(), $"{item.Remaining:N2}", true, "#FFF2CC"); }
                });
            });
            page.Footer().AlignCenter().Text(text => { text.Span("Document généré par ERP Administration Scolaire RDC  •  "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
        })).GeneratePdf();
    }

    private static void SummaryCard(RowDescriptor row, string label, decimal value, string background)
    {
        row.RelativeItem().PaddingRight(8).Background(background).Border(1).BorderColor("#D9E2F3").Padding(9).Column(card =>
        {
            card.Item().Text(label).FontSize(8).Bold().FontColor("#5B6573");
            card.Item().PaddingTop(3).Text($"{value:N2}").FontSize(13).Bold().FontColor("#17365D");
        });
    }

    private static void PdfHeaderCell(IContainer cell, string value) => cell.Background("#2F75B5").Padding(6).Text(value).Bold().FontColor("#FFFFFF");

    private static void PdfBodyCell(IContainer cell, string value, bool right = false, string? background = null)
    {
        var styled = cell.Padding(6).BorderBottom(0.5f).BorderColor("#D9E2F3");
        if (background is not null) styled = styled.Background(background);
        if (right) styled = styled.AlignRight();
        styled.Text(value);
    }

    private async Task<byte[]?> LoadReceivablesHeaderImageAsync()
    {
        var configuration = await _brandingApi.GetConfigurationAsync();
        var header = configuration.Headers.FirstOrDefault(item => item.IsActive && item.ApplicableDocumentTypes.Contains(DocumentBrandingType.RapportFinancier))
            ?? configuration.Headers.FirstOrDefault(item => item.IsActive && item.ApplicableDocumentTypes.Contains(DocumentBrandingType.RepartitionRecettes));
        var relativePath = header?.PrintMode == HeaderPrintMode.FullImage
            ? header.ImagePath
            : (configuration.Logos.FirstOrDefault(item => item.IsActive && item.IsPrimary) ?? configuration.Logos.FirstOrDefault(item => item.IsActive))?.ImagePath;
        var path = _brandingPathResolver.ResolveAbsolutePath(relativePath);
        return path is not null && File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
    }

    private bool ValidateDates(bool showStatus)
    {
        ClearDateErrors();

        if (FilterFromDate is null)
        {
            FromDateError = "La date de début (« Du ») est obligatoire.";
            if (showStatus)
            {
                StatusMessage = FromDateError;
            }

            return false;
        }

        if (FilterToDate is null)
        {
            ToDateError = "La date de fin (« Au ») est obligatoire.";
            if (showStatus)
            {
                StatusMessage = ToDateError;
            }

            return false;
        }

        if (FilterToDate.Value.Date < FilterFromDate.Value.Date)
        {
            ToDateError = "La date de fin (« Au ») doit être postérieure ou égale à la date de début (« Du »).";
            FromDateError = "La date de début (« Du ») doit être antérieure ou égale à la date de fin (« Au »).";
            if (showStatus)
            {
                StatusMessage = ToDateError;
            }

            return false;
        }

        return true;
    }

    private void ClearDateErrors()
    {
        FromDateError = null;
        ToDateError = null;
    }

    private RealizedReceiptsRequest BuildRequest()
    {
        var from = DateOnly.FromDateTime(FilterFromDate!.Value);
        var to = DateOnly.FromDateTime(FilterToDate!.Value);
        return new RealizedReceiptsRequest(
            from,
            to,
            AcademicYearRefreshBridge.SelectedYearId,
            FilterFeeType?.Id,
            FilterClassRoom?.Id,
            FilterSection?.Id,
            Page: 1,
            PageSize: 2_000);
    }

    private RevenueAllocationSearchRequest BuildAllocationRequest()
    {
        var from = DateOnly.FromDateTime(FilterFromDate!.Value);
        var to = DateOnly.FromDateTime(FilterToDate!.Value);
        return new RevenueAllocationSearchRequest(
            AcademicYearRefreshBridge.SelectedYearId,
            from,
            to,
            StudentId: null,
            PaymentId: null,
            DestinationId: null,
            FeeTypeId: FilterFeeType?.Id,
            SectionId: FilterSection?.Id,
            ClassRoomId: FilterClassRoom?.Id);
    }

    private async Task ReloadClassRoomsAsync(
        SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStructureOptionsDto? structure = null)
    {
        try
        {
            structure ??= await _wizardApi.GetStructureOptionsAsync();
            _structureAcademicYearId = structure.AcademicYearId;

            var yearId = AcademicYearRefreshBridge.SelectedYearId;
            if (yearId is null || yearId == structure.AcademicYearId)
            {
                _allClassRooms = structure.Classes
                    .Select(c => new ReportClassOption(
                        c.ClassRoomId,
                        c.FullDisplayName,
                        c.SectionId,
                        c.SectionName))
                    .OrderBy(c => c.DisplayName)
                    .ToList();
            }
            else
            {
                _allClassRooms = (await _academicApi.GetClassRoomsAsync(yearId.Value))
                    .Where(c => _organizedSectionNames.Count == 0
                        || _organizedSectionNames.Contains((c.SectionName ?? string.Empty).Trim()))
                    .Select(c => new ReportClassOption(
                        c.Id,
                        BuildClassDisplayName(c),
                        c.SectionId,
                        c.SectionName))
                    .OrderBy(c => c.DisplayName)
                    .ToList();
            }

            ApplyClassRoomFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private static string BuildClassDisplayName(ClassRoomDto classroom)
    {
        if (!string.IsNullOrWhiteSpace(classroom.FullDisplayName)
            && !string.Equals(classroom.FullDisplayName, classroom.Name, StringComparison.OrdinalIgnoreCase))
        {
            return classroom.FullDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(classroom.Code)
            && !string.Equals(classroom.Code, classroom.Name, StringComparison.OrdinalIgnoreCase))
        {
            return classroom.Code;
        }

        if (!string.IsNullOrWhiteSpace(classroom.SectionName))
        {
            return $"{classroom.SectionName} — {classroom.Name}";
        }

        return classroom.Name;
    }

    private void ApplyClassRoomFilter()
    {
        ClassRooms.Clear();
        var rooms = FilterSection is null
            ? _allClassRooms
            : _allClassRooms
                .Where(c =>
                    c.SectionId == FilterSection.Id
                    || string.Equals(
                        c.SectionName.Trim(),
                        FilterSection.Name.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var room in rooms)
        {
            ClassRooms.Add(room);
        }

        if (FilterClassRoom is not null && ClassRooms.All(c => c.Id != FilterClassRoom.Id))
        {
            FilterClassRoom = null;
        }
    }

    private void ApplyPeriodDates(RealizedReceiptsPeriodKind kind)
    {
        var today = DateTime.Today;
        switch (kind)
        {
            case RealizedReceiptsPeriodKind.Day:
                FilterFromDate = today;
                FilterToDate = today;
                break;
            case RealizedReceiptsPeriodKind.Week:
            {
                var mondayOffset = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var monday = today.AddDays(-mondayOffset);
                FilterFromDate = monday;
                FilterToDate = monday.AddDays(6);
                break;
            }
            case RealizedReceiptsPeriodKind.Month:
                EnsureMonthSelectionDefaults();
                ApplySelectedMonthDates();
                break;
            case RealizedReceiptsPeriodKind.Custom:
                FilterFromDate ??= today.AddDays(-7);
                FilterToDate ??= today;
                break;
        }
    }

    private void EnsureMonthSelectionDefaults()
    {
        if (SelectedMonth is null)
        {
            _suppressMonthReload = true;
            SelectedMonth = MonthOptions.First(m => m.Month == DateTime.Today.Month);
            _suppressMonthReload = false;
        }

        if (SelectedCalendarYear is null)
        {
            _suppressMonthReload = true;
            SelectedCalendarYear = CalendarYears.FirstOrDefault(y => y.Year == DateTime.Today.Year)
                ?? CalendarYears.LastOrDefault();
            _suppressMonthReload = false;
        }
    }

    private void ApplySelectedMonthDates()
    {
        if (SelectedMonth is null || SelectedCalendarYear is null)
        {
            return;
        }

        FilterFromDate = new DateTime(SelectedCalendarYear.Year, SelectedMonth.Month, 1);
        FilterToDate = FilterFromDate.Value.AddMonths(1).AddDays(-1);
    }
}
