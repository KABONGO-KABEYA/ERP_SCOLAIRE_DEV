using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Helpers;
using SchoolManagement.Desktop.Models;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

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
        IEnrollmentWizardApiService wizardApi)
    {
        _reportApi = reportApi;
        _allocationApi = allocationApi;
        _schoolApi = schoolApi;
        _schoolFeeApi = schoolFeeApi;
        _academicApi = academicApi;
        _wizardApi = wizardApi;
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

        SelectedPeriod = PeriodOptions[0];
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

    public ObservableCollection<WithholdingReportTypeGroupRow> WithholdingGroups { get; } = [];

    [ObservableProperty] private AllocationCashFlowRowDto? _allocationTotals;

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

    public bool IsCustomPeriod => SelectedPeriod?.Kind == RealizedReceiptsPeriodKind.Custom;

    public bool IsMonthPeriod => SelectedPeriod?.Kind == RealizedReceiptsPeriodKind.Month;

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
            AllocationTotals = null;
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
            await Task.WhenAll(reportTask, allocationTask, withholdingTask);

            var result = await reportTask;
            ApplyPivot(result);
            ApplyDailyPivot(result);
            ApplyAllocationCashFlow(await allocationTask);
            ApplyWithholdingReport(await withholdingTask);

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
            StatusMessage = $"{result.PivotRows.Count} élève(s) — total {result.GrandTotal:N2}";
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

        AllocationTotals = result.Totals;
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
        SelectedPeriod = PeriodOptions[0];
        FilterFeeType = DefaultFeeTypeHelper.Resolve(FeeTypes, _defaultFeeTypeId);
        FilterSection = null;
        FilterClassRoom = null;
        _suppressMonthReload = true;
        SelectedMonth = MonthOptions.First(m => m.Month == DateTime.Today.Month);
        SelectedCalendarYear = CalendarYears.FirstOrDefault(y => y.Year == DateTime.Today.Year)
            ?? CalendarYears.LastOrDefault();
        _suppressMonthReload = false;
        ApplyPeriodDates(RealizedReceiptsPeriodKind.Day);
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
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"recettes-realisees-{FilterFromDate:yyyyMMdd}-{FilterToDate:yyyyMMdd}.pdf",
                Filter = "PDF|*.pdf"
            };
            if (ErpFileDialog.ShowSave(dialog) != true)
            {
                return;
            }

            var bytes = await _reportApi.ExportRealizedReceiptsPdfAsync(BuildRequest());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusMessage = $"Export PDF enregistré : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
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
                FileName = $"recettes-realisees-{FilterFromDate:yyyyMMdd}-{FilterToDate:yyyyMMdd}.xlsx",
                Filter = "Excel|*.xlsx"
            };
            if (ErpFileDialog.ShowSave(dialog) != true)
            {
                return;
            }

            var bytes = await _reportApi.ExportRealizedReceiptsExcelAsync(BuildRequest());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusMessage = $"Export Excel enregistré : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
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
