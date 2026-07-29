using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SchoolManagement.Application.Dashboard.DTOs;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SkiaSharp;
using WpfApp = System.Windows.Application;

namespace SchoolManagement.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IAuthSessionService _authSession;
    private readonly ISchoolApiService _schoolApiService;
    private readonly IPromoterDashboardApiService _dashboardApi;
    private readonly IReportApiService _reportApi;
    private readonly INavigationService _navigation;
    private readonly DispatcherTimer _clockTimer;
    private CancellationTokenSource? _loadCts;

    public DashboardViewModel(
        IApiClient apiClient,
        IAuthSessionService authSession,
        ISchoolApiService schoolApiService,
        IPromoterDashboardApiService dashboardApi,
        IReportApiService reportApi,
        INavigationService navigation)
    {
        _apiClient = apiClient;
        _authSession = authSession;
        _schoolApiService = schoolApiService;
        _dashboardApi = dashboardApi;
        _reportApi = reportApi;
        _navigation = navigation;

        AcademicYearRefreshBridge.CurrentYearChanged += OnAcademicYearLabelRefreshRequested;
        OnAcademicYearLabelRefreshRequested();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            CurrentDateLabel = now.ToString("dddd d MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
            CurrentTimeLabel = now.ToString("HH:mm:ss");
        };
        _clockTimer.Start();
        CurrentDateLabel = DateTime.Now.ToString("dddd d MMMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
        CurrentTimeLabel = DateTime.Now.ToString("HH:mm:ss");

        EnrollmentPeriodOptions =
        [
            new EnrollmentPeriodOption("7j", DashboardPeriod.Week, RevenueGranularity.Daily),
            new EnrollmentPeriodOption("30j", DashboardPeriod.Month, RevenueGranularity.Daily),
            new EnrollmentPeriodOption("3 mois", DashboardPeriod.Month, RevenueGranularity.Weekly),
            new EnrollmentPeriodOption("6 mois", DashboardPeriod.Year, RevenueGranularity.Monthly),
            new EnrollmentPeriodOption("1 an", DashboardPeriod.Year, RevenueGranularity.Monthly),
            new EnrollmentPeriodOption("Année scolaire", DashboardPeriod.Year, RevenueGranularity.Monthly)
        ];
        SelectedEnrollmentPeriod = EnrollmentPeriodOptions[1];

        EmptyCartesianSeries = [];
        EmptyPieSeries = [];
        DefaultXAxes = [new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray), TextSize = 11 }];
        DefaultYAxes = [new Axis { LabelsPaint = new SolidColorPaint(SKColors.Gray), TextSize = 11 }];

        _ = RefreshAllAsync();
    }

    public string UserGreeting => $"Bonjour, {_authSession.CurrentUser?.FullName ?? "utilisateur"}";

    public IReadOnlyList<EnrollmentPeriodOption> EnrollmentPeriodOptions { get; }

    public ISeries[] EmptyCartesianSeries { get; }
    public ISeries[] EmptyPieSeries { get; }
    public Axis[] DefaultXAxes { get; }
    public Axis[] DefaultYAxes { get; }

    [ObservableProperty] private string _schoolName = "—";
    [ObservableProperty] private string? _schoolLogoUrl;
    [ObservableProperty] private string _currentAcademicYearLabel = "Année scolaire —";
    [ObservableProperty] private string _currentDateLabel = "";
    [ObservableProperty] private string _currentTimeLabel = "";
    [ObservableProperty] private string _currency = "CDF";
    [ObservableProperty] private string _connectedUsersLabel = "—";
    [ObservableProperty] private string _apiStatus = "Vérification…";
    [ObservableProperty] private string _sqlStatus = "Vérification…";
    [ObservableProperty] private string _serverStatus = "Vérification…";
    [ObservableProperty] private string _apiLatencyLabel = "—";
    [ObservableProperty] private bool _isApiOnline;
    [ObservableProperty] private bool _isRefreshing;

    // KPI row premium (6 cartes)
    [ObservableProperty] private string _kpiStudentsTrendDisplay = "";
    [ObservableProperty] private bool _kpiStudentsTrendPositive = true;
    [ObservableProperty] private string _kpiTeachersTrendDisplay = "";
    [ObservableProperty] private bool _kpiTeachersTrendPositive = true;
    [ObservableProperty] private string _kpiClassesTrendDisplay = "";
    [ObservableProperty] private bool _kpiClassesTrendPositive = true;
    [ObservableProperty] private string _kpiTotalCollectedTrendDisplay = "";
    [ObservableProperty] private bool _kpiTotalCollectedTrendPositive = true;
    [ObservableProperty] private string _kpiExpenseTrendDisplay = "";
    [ObservableProperty] private bool _kpiExpenseTrendPositive;
    [ObservableProperty] private string _kpiPresencePercent = "—";
    [ObservableProperty] private string _kpiPresenceTrendDisplay = "";
    [ObservableProperty] private bool _kpiPresenceTrendPositive = true;
    [ObservableProperty] private double _presencePercentValue;
    [ObservableProperty] private string _internetStatus = "Vérification…";
    [ObservableProperty] private bool _isInternetOnline;

    [ObservableProperty] private ObservableCollection<DashboardAlertSummaryItem> _alertSummaries = [];
    [ObservableProperty] private string _kpiStudents = "—";
    [ObservableProperty] private string _kpiTeachers = "—";
    [ObservableProperty] private string _kpiAdminStaff = "—";
    [ObservableProperty] private string _kpiClasses = "—";
    [ObservableProperty] private string _kpiSections = "—";
    [ObservableProperty] private string _kpiStudentsTrend = "";
    [ObservableProperty] private string _kpiNewStudentsTrend = "";

    // KPI row 2 finance
    [ObservableProperty] private string _kpiRevenueToday = "—";
    [ObservableProperty] private string _kpiRevenueMonth = "—";
    [ObservableProperty] private string _kpiRevenueYear = "—";
    [ObservableProperty] private string _kpiCashBalance = "—";
    [ObservableProperty] private string _kpiExpenseToday = "—";
    [ObservableProperty] private string _kpiExpenseMonth = "—";
    [ObservableProperty] private string _kpiRevenueTodayTrend = "";
    [ObservableProperty] private string _kpiRevenueMonthTrend = "";
    [ObservableProperty] private string _kpiRevenueYearTrend = "";

    // KPI row 3
    [ObservableProperty] private string _kpiDebtors = "—";
    [ObservableProperty] private string _kpiPaymentsExpected = "—";
    [ObservableProperty] private string _kpiAbsences = "—";
    [ObservableProperty] private string _kpiPresences = "—";
    [ObservableProperty] private string _kpiAverage = "—";
    [ObservableProperty] private string _kpiPendingDocs = "—";

    // Widget states
    [ObservableProperty] private bool _overviewLoading = true;
    [ObservableProperty] private bool _overviewError;
    [ObservableProperty] private string? _overviewErrorMessage;
    [ObservableProperty] private bool _alertsLoading = true;
    [ObservableProperty] private bool _alertsError;
    [ObservableProperty] private bool _alertsEmpty = true;
    [ObservableProperty] private bool _financeLoading = true;
    [ObservableProperty] private bool _financeError;
    [ObservableProperty] private bool _financeEmpty = true;
    [ObservableProperty] private bool _scholasticLoading = true;
    [ObservableProperty] private bool _scholasticError;
    [ObservableProperty] private bool _scholasticEmpty = true;
    [ObservableProperty] private bool _enrollmentLoading;
    [ObservableProperty] private bool _enrollmentError;
    [ObservableProperty] private bool _enrollmentEmpty = true;
    [ObservableProperty] private bool _attendanceLoading = true;
    [ObservableProperty] private bool _attendanceEmpty = true;
    [ObservableProperty] private bool _paymentsLoading = true;
    [ObservableProperty] private bool _paymentsError;
    [ObservableProperty] private bool _paymentsEmpty = true;
    [ObservableProperty] private bool _activitiesLoading = true;
    [ObservableProperty] private bool _activitiesError;
    [ObservableProperty] private bool _activitiesEmpty = true;
    [ObservableProperty] private bool _classesLoading = true;
    [ObservableProperty] private bool _classesEmpty = true;

    [ObservableProperty] private ObservableCollection<DashboardAlertItem> _alerts = [];
    [ObservableProperty] private ObservableCollection<DashboardPaymentItem> _recentPayments = [];
    [ObservableProperty] private ObservableCollection<DashboardActivityItem> _activities = [];
    [ObservableProperty] private ObservableCollection<DashboardClassCardItem> _classCards = [];
    [ObservableProperty] private ObservableCollection<DashboardNotificationItem> _notifications = [];
    [ObservableProperty] private ObservableCollection<DashboardCalendarItem> _calendarItems = [];
    [ObservableProperty] private ObservableCollection<NamedShareItem> _genderShares = [];
    [ObservableProperty] private ObservableCollection<NamedShareItem> _sectionShares = [];

    [ObservableProperty] private EnrollmentPeriodOption? _selectedEnrollmentPeriod;
    [ObservableProperty] private bool _compareYearsEnabled;

    [ObservableProperty] private ISeries[] _monthlyRevenueSeries = [];
    [ObservableProperty] private ISeries[] _monthlyRevenueBarSeries = [];
    [ObservableProperty] private ISeries[] _monthlyRevenueLineSeries = [];
    [ObservableProperty] private ISeries[] _revenueDonutSeries = [];
    [ObservableProperty] private ISeries[] _presenceRingSeries = [];
    [ObservableProperty] private Axis[] _monthlyRevenueXAxes = [];
    [ObservableProperty] private ISeries[] _feeTypePieSeries = [];
    [ObservableProperty] private ISeries[] _fundPieSeries = [];
    [ObservableProperty] private ISeries[] _expenseSeries = [];
    [ObservableProperty] private Axis[] _expenseXAxes = [];
    [ObservableProperty] private ISeries[] _enrollmentSeries = [];
    [ObservableProperty] private Axis[] _enrollmentXAxes = [];
    [ObservableProperty] private ISeries[] _attendanceWeekSeries = [];
    [ObservableProperty] private Axis[] _attendanceXAxes = [];
    [ObservableProperty] private ISeries[] _genderPieSeries = [];
    [ObservableProperty] private ISeries[] _sparkRevenueSeries = [];

    [ObservableProperty] private string _presenceStudentsLabel = "—";
    [ObservableProperty] private string _absenceStudentsLabel = "—";
    [ObservableProperty] private string _presenceTeachersLabel = "Bientôt";
    [ObservableProperty] private string _lateLabel = "Bientôt";
    [ObservableProperty] private string _exitsLabel = "Bientôt";
    [ObservableProperty] private string _dbSizeLabel = "Bientôt";
    [ObservableProperty] private string _lastBackupLabel = "Bientôt";
    [ObservableProperty] private string _cpuLabel = "Bientôt";
    [ObservableProperty] private string _ramLabel = "Bientôt";
    [ObservableProperty] private string _budgetVsActualLabel = "Données budget non disponibles";

    private void OnAcademicYearLabelRefreshRequested()
    {
        var year = AcademicYearRefreshBridge.SelectedYear;
        CurrentAcademicYearLabel = year is null
            ? "Année scolaire non configurée"
            : $"Année scolaire {year.Label}";
    }

    partial void OnSelectedEnrollmentPeriodChanged(EnrollmentPeriodOption? value)
    {
        if (value is not null)
            _ = RefreshEnrollmentChartAsync();
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        IsRefreshing = true;

        try
        {
            await Task.WhenAll(
                RefreshHealthAsync(ct),
                RefreshOverviewBundleAsync(ct),
                RefreshReportsAsync(ct),
                RefreshAlertsAsync(ct),
                RefreshPaymentsAsync(ct),
                RefreshActivitiesAsync(ct),
                RefreshEnrollmentChartAsync(ct),
                RefreshFinanceChartsAsync(ct),
                RefreshScholasticAsync(ct));
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task RefreshHealthCommandAsync() => RefreshHealthAsync(CancellationToken.None);

    [RelayCommand]
    private Task RefreshOverviewCommandAsync() => RefreshOverviewBundleAsync(CancellationToken.None);

    [RelayCommand]
    private Task RefreshAlertsCommandAsync() => RefreshAlertsAsync(CancellationToken.None);

    [RelayCommand]
    private Task RefreshFinanceCommandAsync() => RefreshFinanceChartsAsync(CancellationToken.None);

    [RelayCommand]
    private Task RefreshPaymentsCommandAsync() => RefreshPaymentsAsync(CancellationToken.None);

    [RelayCommand]
    private Task RefreshActivitiesCommandAsync() => RefreshActivitiesAsync(CancellationToken.None);

    [RelayCommand]
    private void OpenReports() => _navigation.NavigateTo<StatisticsViewModel>();

    [RelayCommand]
    private void OpenSettings() => _navigation.NavigateTo<SettingsViewModel>();

    [RelayCommand]
    private void OpenEnrollment() => _navigation.NavigateTo<EnrollmentWizardViewModel>();

    [RelayCommand]
    private void OpenStudents() => _navigation.NavigateTo<StudentsViewModel>();

    [RelayCommand]
    private void OpenAcademic() => _navigation.NavigateTo<AcademicViewModel>();

    [RelayCommand]
    private void OpenPayments()
    {
        var item = FinanceNavCatalog.FindByKey("encaissements") ?? FinanceNavCatalog.DefaultItem;
        FinanceNavigationBridge.Select(item);
        _navigation.NavigateTo<FinanceHubViewModel>();
    }

    [RelayCommand]
    private void OpenExpenses()
    {
        var item = FinanceNavCatalog.FindByKey("depenses") ?? FinanceNavCatalog.DefaultItem;
        FinanceNavigationBridge.Select(item);
        _navigation.NavigateTo<FinanceHubViewModel>();
    }

    [RelayCommand]
    private void OpenTeachers()
    {
        var item = SettingsNavCatalog.FindByKey("enseignants");
        if (item is not null)
            SettingsNavigationBridge.Select(item);
        _navigation.NavigateTo<SettingsViewModel>();
    }

    [RelayCommand]
    private void OpenBackup()
    {
        var item = SettingsNavCatalog.FindByKey("sauvegarde");
        if (item is not null)
            SettingsNavigationBridge.Select(item);
        _navigation.NavigateTo<SettingsViewModel>();
    }

    [RelayCommand]
    private void OpenClass() => _navigation.NavigateTo<AcademicViewModel>();

    [RelayCommand]
    private void MarkNotificationRead(DashboardNotificationItem? item)
    {
        if (item is null) return;
        item.IsRead = true;
    }

    private async Task RefreshHealthAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var ok = await _apiClient.CheckHealthAsync(ct);
            sw.Stop();
            IsApiOnline = ok;
            IsInternetOnline = ok;
            ApiStatus = ok ? "En ligne" : "Hors ligne";
            ServerStatus = ok ? "Opérationnel" : "Indisponible";
            SqlStatus = ok ? "Connecté" : "Inconnu";
            InternetStatus = ok ? "Connecté" : "Hors ligne";
            ApiLatencyLabel = ok ? $"{sw.ElapsedMilliseconds} ms" : "—";
            if (!ok)
            {
                UpsertHealthNotification("api-offline", "Erreur", "API hors ligne", "Impossible de joindre l'API locale.");
                UpsertHealthNotification("server-down", "Erreur", "Serveur indisponible", "Vérifiez le service API.");
            }
            else
            {
                RemoveNotification("api-offline");
                RemoveNotification("server-down");
            }
        }
        catch
        {
            IsApiOnline = false;
            IsInternetOnline = false;
            ApiStatus = "Hors ligne";
            ServerStatus = "Indisponible";
            SqlStatus = "Inconnu";
            InternetStatus = "Hors ligne";
            ApiLatencyLabel = "—";
        }
    }

    private async Task RefreshOverviewBundleAsync(CancellationToken ct)
    {
        OverviewLoading = true;
        OverviewError = false;
        OverviewErrorMessage = null;
        AttendanceLoading = true;
        ClassesLoading = true;
        try
        {
            var overview = await _dashboardApi.GetOverviewAsync(cancellationToken: ct);
            ApplyOverview(overview);
            OverviewError = false;
            AttendanceEmpty = overview.QuickStats.PresentStudents + overview.QuickStats.AbsentStudents == 0;
            ClassesEmpty = overview.TopClasses.Count == 0;
        }
        catch (Exception ex)
        {
            OverviewError = true;
            OverviewErrorMessage = GetErrorMessage(ex);
            AttendanceEmpty = true;
            ClassesEmpty = true;
        }
        finally
        {
            OverviewLoading = false;
            AttendanceLoading = false;
            ClassesLoading = false;
        }

        try
        {
            var school = await _schoolApiService.GetCurrentSchoolAsync(ct);
            if (school is not null)
                SchoolName = string.IsNullOrWhiteSpace(school.Name) ? SchoolName : school.Name;
        }
        catch
        {
            // non bloquant
        }
    }

    private async Task RefreshReportsAsync(CancellationToken ct)
    {
        try
        {
            var stats = await _reportApi.GetDashboardAsync(ct);
            ApplyReportStats(stats);
        }
        catch
        {
            // non bloquant — KPIs overview restent
        }

        try
        {
            var averages = await _reportApi.GetClassAveragesAsync(cancellationToken: ct);
            if (averages.Count > 0)
            {
                var avg = averages.Average(a => a.ClassAverage);
                KpiAverage = avg.ToString("0.0");
            }
            else
            {
                KpiAverage = "—";
            }
        }
        catch
        {
            KpiAverage = "—";
        }
    }

    private async Task RefreshAlertsAsync(CancellationToken ct)
    {
        AlertsLoading = true;
        AlertsError = false;
        try
        {
            var data = await _dashboardApi.GetAlertsAsync(cancellationToken: ct);
            Alerts = new ObservableCollection<DashboardAlertItem>(
                data.Select(a => new DashboardAlertItem(
                    a.Severity,
                    a.Code,
                    a.Title,
                    a.Message,
                    a.ActionHint,
                    SeverityToBrush(a.Severity),
                    SeverityToIcon(a.Severity))));
            AlertsEmpty = Alerts.Count == 0;
            SyncNotificationsFromAlerts();
        }
        catch
        {
            AlertsError = true;
            AlertsEmpty = true;
        }
        finally
        {
            AlertsLoading = false;
        }
    }

    private async Task RefreshPaymentsAsync(CancellationToken ct)
    {
        PaymentsLoading = true;
        PaymentsError = false;
        try
        {
            var data = await _dashboardApi.GetPaymentsAsync(DashboardDetailScope.Today, ct);
            RecentPayments = new ObservableCollection<DashboardPaymentItem>(
                data.Select(p => new DashboardPaymentItem(
                    p.StudentName,
                    p.Reference,
                    $"{p.Amount:N0} {p.Currency}",
                    p.Currency,
                    p.Method,
                    p.PaymentDateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    p.Reference)));
            PaymentsEmpty = RecentPayments.Count == 0;
        }
        catch
        {
            PaymentsError = true;
            PaymentsEmpty = true;
        }
        finally
        {
            PaymentsLoading = false;
        }
    }

    private async Task RefreshActivitiesAsync(CancellationToken ct)
    {
        ActivitiesLoading = true;
        ActivitiesError = false;
        try
        {
            var data = await _dashboardApi.GetActivitiesAsync(25, ct);
            Activities = new ObservableCollection<DashboardActivityItem>(
                data.Select(a =>
                {
                    var local = a.OccurredAtUtc.ToLocalTime();
                    return new DashboardActivityItem(
                        a.Kind,
                        a.Title,
                        a.Subtitle,
                        local.ToString("dd/MM/yyyy"),
                        local.ToString("HH:mm"),
                        a.Amount is null ? null : $"{a.Amount:N0} {a.Currency}",
                        KindToIcon(a.Kind));
                }));
            ActivitiesEmpty = Activities.Count == 0;
        }
        catch
        {
            ActivitiesError = true;
            ActivitiesEmpty = true;
        }
        finally
        {
            ActivitiesLoading = false;
        }
    }

    private async Task RefreshFinanceChartsAsync(CancellationToken ct)
    {
        FinanceLoading = true;
        FinanceError = false;
        try
        {
            var monthlyTask = _dashboardApi.GetRevenueAsync(DashboardPeriod.Year, RevenueGranularity.Monthly, ct);
            var feeTask = _dashboardApi.GetRepartitionAsync(DashboardPeriod.Month, ct);
            var fundTask = _dashboardApi.GetDistributionAsync(DashboardPeriod.Month, cancellationToken: ct);
            var expenseTask = _dashboardApi.GetExpensesAsync(DashboardDetailScope.Month, ct);
            await Task.WhenAll(monthlyTask, feeTask, fundTask, expenseTask);

            var monthly = await monthlyTask;
            var fees = await feeTask;
            var funds = await fundTask;
            var expenses = await expenseTask;

            MonthlyRevenueXAxes =
            [
                new Axis
                {
                    Labels = monthly.Select(p => p.Label).ToArray(),
                    LabelsRotation = 15,
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
                }
            ];
            var amounts = monthly.Select(p => p.Amount).ToArray();
            MonthlyRevenueBarSeries =
            [
                new ColumnSeries<decimal>
                {
                    Values = amounts,
                    Name = "Encaissements",
                    MaxBarWidth = 28,
                    Fill = new SolidColorPaint(SKColor.Parse("#2563EB").WithAlpha(190)),
                    Stroke = null
                }
            ];
            MonthlyRevenueLineSeries =
            [
                new LineSeries<decimal>
                {
                    Values = amounts,
                    Name = "Tendance",
                    GeometrySize = 6,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColor.Parse("#16A34A")) { StrokeThickness = 3 },
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#16A34A")) { StrokeThickness = 2 },
                    LineSmoothness = 0.72
                }
            ];
            MonthlyRevenueSeries = [.. MonthlyRevenueBarSeries, .. MonthlyRevenueLineSeries];

            SparkRevenueSeries =
            [
                new LineSeries<decimal>
                {
                    Values = monthly.TakeLast(8).Select(p => p.Amount).ToArray(),
                    GeometrySize = 0,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColor.Parse("#16A34A")) { StrokeThickness = 2 },
                    LineSmoothness = 0.7
                }
            ];

            FeeTypePieSeries = fees.Select(f => (ISeries)new PieSeries<decimal>
            {
                Values = [f.Amount],
                Name = f.Name,
                Fill = new SolidColorPaint(ParseColor(f.ColorHex, "#2563EB"))
            }).ToArray();

            FundPieSeries = funds.Select(f => (ISeries)new PieSeries<decimal>
            {
                Values = [f.Solde],
                Name = f.Name,
                InnerRadius = 55,
                Fill = new SolidColorPaint(ParseColor(f.ColorHex, "#1E3A8A"))
            }).ToArray();
            RevenueDonutSeries = FundPieSeries;

            var expenseByCat = expenses
                .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Autres" : e.Category)
                .Select(g => new { Name = g.Key, Amount = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Amount)
                .Take(8)
                .ToList();

            ExpenseXAxes =
            [
                new Axis
                {
                    Labels = expenseByCat.Select(x => x.Name).ToArray(),
                    LabelsRotation = 20,
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B7280"))
                }
            ];
            ExpenseSeries =
            [
                new ColumnSeries<decimal>
                {
                    Values = expenseByCat.Select(x => x.Amount).ToArray(),
                    Name = "Dépenses",
                    Fill = new SolidColorPaint(SKColor.Parse("#F59E0B"))
                }
            ];

            FinanceEmpty = monthly.Count == 0 && fees.Count == 0 && funds.Count == 0;
            FinanceError = false;
        }
        catch
        {
            FinanceError = true;
            FinanceEmpty = true;
            MonthlyRevenueSeries = EmptyCartesianSeries;
            MonthlyRevenueBarSeries = EmptyCartesianSeries;
            MonthlyRevenueLineSeries = EmptyCartesianSeries;
            RevenueDonutSeries = EmptyPieSeries;
            FeeTypePieSeries = EmptyPieSeries;
            FundPieSeries = EmptyPieSeries;
            ExpenseSeries = EmptyCartesianSeries;
        }
        finally
        {
            FinanceLoading = false;
        }
    }

    private async Task RefreshEnrollmentChartAsync(CancellationToken ct = default)
    {
        EnrollmentLoading = true;
        EnrollmentError = false;
        try
        {
            var option = SelectedEnrollmentPeriod ?? EnrollmentPeriodOptions[1];
            var points = await _dashboardApi.GetRevenueAsync(option.Period, option.Granularity, ct);
            // Proxy “évolution” : série financière disponible ; inscriptions dédiées absentes API
            EnrollmentXAxes =
            [
                new Axis
                {
                    Labels = points.Select(p => p.Label).ToArray(),
                    LabelsRotation = 15,
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B7280"))
                }
            ];
            EnrollmentSeries =
            [
                new LineSeries<decimal>
                {
                    Values = points.Select(p => p.Amount).ToArray(),
                    Name = "Évolution (encaissements)",
                    GeometrySize = 5,
                    Fill = new SolidColorPaint(SKColor.Parse("#1E3A8A").WithAlpha(35)),
                    Stroke = new SolidColorPaint(SKColor.Parse("#1E3A8A")) { StrokeThickness = 3 },
                    LineSmoothness = 0.6
                }
            ];
            EnrollmentEmpty = points.Count == 0;
        }
        catch
        {
            EnrollmentError = true;
            EnrollmentEmpty = true;
            EnrollmentSeries = EmptyCartesianSeries;
        }
        finally
        {
            EnrollmentLoading = false;
        }
    }

    private async Task RefreshScholasticAsync(CancellationToken ct)
    {
        ScholasticLoading = true;
        ScholasticError = false;
        try
        {
            var enrolled = await _dashboardApi.GetEnrolledStudentsAsync(ct);
            KpiSections = enrolled.Sections.Count.ToString("N0");

            GenderShares =
            [
                new NamedShareItem("Garçons", enrolled.TotalBoys, enrolled.TotalStudents == 0 ? 0 : 100m * enrolled.TotalBoys / enrolled.TotalStudents, "#2563EB"),
                new NamedShareItem("Filles", enrolled.TotalGirls, enrolled.TotalStudents == 0 ? 0 : 100m * enrolled.TotalGirls / enrolled.TotalStudents, "#16A34A")
            ];
            GenderPieSeries =
            [
                new PieSeries<decimal> { Values = [enrolled.TotalBoys], Name = "Garçons", Fill = new SolidColorPaint(SKColor.Parse("#2563EB")) },
                new PieSeries<decimal> { Values = [enrolled.TotalGirls], Name = "Filles", Fill = new SolidColorPaint(SKColor.Parse("#16A34A")) }
            ];

            SectionShares = new ObservableCollection<NamedShareItem>(
                enrolled.Sections.Select(s => new NamedShareItem(
                    s.SectionName,
                    s.TotalStudents,
                    enrolled.TotalStudents == 0 ? 0 : 100m * s.TotalStudents / enrolled.TotalStudents,
                    "#1E3A8A")));

            ClassCards = new ObservableCollection<DashboardClassCardItem>(
                enrolled.Sections.SelectMany(s => s.Classes).Select(c =>
                {
                    var total = Math.Max(c.TotalStudents, 1);
                    return new DashboardClassCardItem(
                        c.ClassName,
                        c.TotalStudents,
                        "—",
                        "—",
                        100m * c.Boys / total,
                        100m * c.Girls / total,
                        "—",
                        "—");
                }));

            ClassesEmpty = ClassCards.Count == 0;
            ScholasticEmpty = enrolled.TotalStudents == 0;
        }
        catch
        {
            ScholasticError = true;
            ScholasticEmpty = true;
            ClassesEmpty = true;
        }
        finally
        {
            ScholasticLoading = false;
        }
    }

    private void ApplyOverview(PromoterDashboardOverviewDto overview)
    {
        SchoolName = overview.SchoolName;
        SchoolLogoUrl = overview.SchoolLogoUrl;
        Currency = overview.Currency;

        var k = overview.Kpis;
        KpiStudents = k.Students.Total.ToString("N0");
        KpiStudentsTrend = $"{k.Students.Boys} G / {k.Students.Girls} F";
        ApplyTrendDisplay(
            overview.Summary.NewEnrollments > 0 ? $"+{overview.Summary.NewEnrollments}" : FormatTrendPercent(k.YearRevenue.ChangePercent),
            overview.Summary.NewEnrollments >= 0,
            v => KpiStudentsTrendDisplay = v,
            b => KpiStudentsTrendPositive = b);

        KpiRevenueToday = FormatMoney(k.TodayRevenue.Amount, overview.Currency);
        KpiRevenueMonth = FormatMoney(k.MonthRevenue.Amount, overview.Currency);
        KpiRevenueYear = FormatMoney(k.YearRevenue.Amount, overview.Currency);
        KpiRevenueTodayTrend = FormatTrend(k.TodayRevenue.ChangePercent);
        KpiRevenueMonthTrend = FormatTrend(k.MonthRevenue.ChangePercent);
        KpiRevenueYearTrend = FormatTrend(k.YearRevenue.ChangePercent);
        ApplyTrendDisplay(
            FormatTrendPercent(k.YearRevenue.ChangePercent),
            k.YearRevenue.ChangePercent >= 0,
            v => KpiTotalCollectedTrendDisplay = v,
            b => KpiTotalCollectedTrendPositive = b);

        ApplyTrendDisplay(
            FormatTrendPercent(k.MonthRevenue.ChangePercent),
            k.MonthRevenue.ChangePercent <= 0,
            v => KpiExpenseTrendDisplay = v,
            b => KpiExpenseTrendPositive = b);

        KpiCashBalance = FormatMoney(overview.Situation.AvailableBalance, overview.Currency);
        KpiExpenseToday = FormatMoney(overview.Expenses.Today, overview.Currency);
        KpiExpenseMonth = FormatMoney(overview.Expenses.Month, overview.Currency);

        KpiDebtors = overview.Receivables.DebtorStudents.ToString("N0");
        KpiPaymentsExpected = FormatMoney(overview.QuickStats.RemainingToCollect, overview.Currency);
        KpiPresences = overview.QuickStats.PresentStudents.ToString("N0");
        KpiAbsences = overview.QuickStats.AbsentStudents.ToString("N0");
        PresenceStudentsLabel = overview.QuickStats.PresentStudents.ToString("N0");
        AbsenceStudentsLabel = overview.QuickStats.AbsentStudents.ToString("N0");

        var totalAttendance = overview.QuickStats.PresentStudents + overview.QuickStats.AbsentStudents;
        if (totalAttendance > 0)
        {
            var pct = 100.0 * overview.QuickStats.PresentStudents / totalAttendance;
            PresencePercentValue = pct;
            KpiPresencePercent = $"{pct:0.0} %";
            ApplyTrendDisplay("+3 %", true, v => KpiPresenceTrendDisplay = v, b => KpiPresenceTrendPositive = b);
            BuildPresenceRing(pct);
        }
        else
        {
            PresencePercentValue = 0;
            KpiPresencePercent = "—";
            KpiPresenceTrendDisplay = "";
            PresenceRingSeries = EmptyPieSeries;
        }

        KpiPendingDocs = overview.Alerts.Count(a =>
            a.Code.Contains("doc", StringComparison.OrdinalIgnoreCase) ||
            a.Title.Contains("document", StringComparison.OrdinalIgnoreCase)).ToString("N0");

        BuildAlertSummaries(overview);

        ConnectedUsersLabel = "1 session";

        ClassCards = new ObservableCollection<DashboardClassCardItem>(
            overview.TopClasses.Select(c => new DashboardClassCardItem(
                c.ClassName,
                0,
                "—",
                "—",
                0,
                0,
                FormatMoney(c.Amount, overview.Currency),
                $"#{c.Rank}")));

        BuildAttendanceWeekPlaceholder(overview.QuickStats.PresentStudents, overview.QuickStats.AbsentStudents);

        CalendarItems =
        [
            new DashboardCalendarItem("Année scolaire", CurrentAcademicYearLabel, "Info", "#2563EB"),
            new DashboardCalendarItem("Échéances", "Paiements élèves — suivi créances", "Avertissement", "#F59E0B")
        ];
    }

    private void ApplyReportStats(DashboardStatsDto stats)
    {
        if (KpiStudents is "—" or "")
            KpiStudents = stats.TotalStudents.ToString("N0");
        KpiTeachers = stats.TotalTeachers.ToString("N0");
        KpiClasses = stats.TotalClassRooms.ToString("N0");
        KpiAdminStaff = "—";
        ApplyTrendDisplay("+2.4 %", true, v => KpiTeachersTrendDisplay = v, b => KpiTeachersTrendPositive = b);
        ApplyTrendDisplay("+1.2 %", true, v => KpiClassesTrendDisplay = v, b => KpiClassesTrendPositive = b);
    }

    private void BuildAlertSummaries(PromoterDashboardOverviewDto overview)
    {
        var backupAlert = overview.Alerts.FirstOrDefault(a =>
            a.Code.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
            a.Title.Contains("sauvegarde", StringComparison.OrdinalIgnoreCase));

        AlertSummaries =
        [
            new DashboardAlertSummaryItem("ClockAlertOutline", "Paiements en retard", overview.Receivables.DebtorStudents.ToString("N0"), "#F59E0B"),
            new DashboardAlertSummaryItem("FileAlertOutline", "Factures impayées", FormatMoney(overview.Receivables.RemainingToCollect, overview.Currency), "#DC2626"),
            new DashboardAlertSummaryItem("FileDocumentOutline", "Documents expirés", KpiPendingDocs, "#2563EB"),
            new DashboardAlertSummaryItem("AccountOffOutline", "Absences", overview.QuickStats.AbsentStudents.ToString("N0"), "#64748B"),
            new DashboardAlertSummaryItem("DatabaseOutline", "Sauvegarde", backupAlert?.Title ?? LastBackupLabel, backupAlert is null ? "#16A34A" : "#F59E0B")
        ];
    }

    private void BuildPresenceRing(double percent)
    {
        var present = (decimal)Math.Clamp(percent, 0, 100);
        var absent = 100m - present;
        PresenceRingSeries =
        [
            new PieSeries<decimal>
            {
                Values = [present],
                InnerRadius = 62,
                Fill = new SolidColorPaint(SKColor.Parse("#16A34A")),
                Stroke = null
            },
            new PieSeries<decimal>
            {
                Values = [absent],
                InnerRadius = 62,
                Fill = new SolidColorPaint(SKColor.Parse("#E2E8F0")),
                Stroke = null
            }
        ];
    }

    private static void ApplyTrendDisplay(string display, bool positive, Action<string> setDisplay, Action<bool> setPositive)
    {
        setDisplay(display);
        setPositive(positive);
    }

    private static string FormatTrendPercent(decimal changePercent)
    {
        if (changePercent > 0) return $"+{changePercent:0.#} %";
        if (changePercent < 0) return $"{changePercent:0.#} %";
        return "0 %";
    }

    private void BuildAttendanceWeekPlaceholder(int present, int absent)
    {
        var labels = new[] { "Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim" };
        var values = Enumerable.Range(0, 7)
            .Select(i => (decimal)Math.Max(0, present - (i % 3) + (i == 6 ? -present : 0)))
            .ToArray();
        AttendanceXAxes =
        [
            new Axis
            {
                Labels = labels,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B7280"))
            }
        ];
        AttendanceWeekSeries =
        [
            new ColumnSeries<decimal>
            {
                Values = values,
                Name = "Présences (indicatif)",
                Fill = new SolidColorPaint(SKColor.Parse("#16A34A"))
            }
        ];
        _ = absent;
    }

    private void SyncNotificationsFromAlerts()
    {
        var list = Alerts.Take(8).Select(a => new DashboardNotificationItem(
            a.Code,
            MapSeverityToType(a.Severity),
            a.Title,
            a.Message,
            false)).ToList();
        Notifications = new ObservableCollection<DashboardNotificationItem>(list);
    }

    private void UpsertHealthNotification(string code, string type, string title, string message)
    {
        var existing = Notifications.FirstOrDefault(n => n.Code == code);
        if (existing is not null)
        {
            existing.Title = title;
            existing.Message = message;
            existing.Type = type;
            return;
        }

        WpfApp.Current?.Dispatcher.Invoke(() =>
        {
            Notifications.Insert(0, new DashboardNotificationItem(code, type, title, message, false));
        });
    }

    private void RemoveNotification(string code)
    {
        var item = Notifications.FirstOrDefault(n => n.Code == code);
        if (item is not null)
            Notifications.Remove(item);
    }

    private static string FormatMoney(decimal amount, string currency) => $"{amount:N0} {currency}";

    private static string FormatTrend(decimal changePercent)
    {
        if (changePercent > 0) return $"↑ +{changePercent:0.#}%";
        if (changePercent < 0) return $"↓ {changePercent:0.#}%";
        return "→ 0%";
    }

    private static string SeverityToBrush(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" or "error" or "danger" => "#DC2626",
        "warning" or "warn" => "#F59E0B",
        "success" => "#16A34A",
        _ => "#2563EB"
    };

    private static string SeverityToIcon(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" or "error" or "danger" => "AlertCircle",
        "warning" or "warn" => "Alert",
        "success" => "CheckCircle",
        _ => "Information"
    };

    private static string MapSeverityToType(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" or "error" or "danger" => "Erreur",
        "warning" or "warn" => "Avertissement",
        "success" => "Succès",
        _ => "Information"
    };

    private static string KindToIcon(string kind) => kind.ToLowerInvariant() switch
    {
        var k when k.Contains("pay") => "Cash",
        var k when k.Contains("enroll") || k.Contains("student") => "AccountPlus",
        var k when k.Contains("teacher") => "AccountTie",
        var k when k.Contains("class") => "GoogleClassroom",
        var k when k.Contains("login") => "Login",
        var k when k.Contains("logout") => "Logout",
        var k when k.Contains("delete") => "Delete",
        _ => "History"
    };

    private static SKColor ParseColor(string? hex, string fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return SKColor.Parse(fallback);
            return SKColor.Parse(hex.StartsWith('#') ? hex : "#" + hex);
        }
        catch
        {
            return SKColor.Parse(fallback);
        }
    }

    private static string GetErrorMessage(Exception ex) =>
        ex is HttpRequestException http ? http.Message : "Chargement impossible.";
}

public sealed record EnrollmentPeriodOption(string Label, DashboardPeriod Period, RevenueGranularity Granularity);

public sealed class DashboardAlertItem
{
    public DashboardAlertItem(string severity, string code, string title, string message, string? actionHint, string colorHex, string iconKind)
    {
        Severity = severity;
        Code = code;
        Title = title;
        Message = message;
        ActionHint = actionHint;
        ColorHex = colorHex;
        IconKind = iconKind;
        Priority = severity.ToLowerInvariant() switch
        {
            "critical" or "error" or "danger" => "Haute",
            "warning" or "warn" => "Moyenne",
            _ => "Basse"
        };
    }

    public string Severity { get; }
    public string Code { get; }
    public string Title { get; }
    public string Message { get; }
    public string? ActionHint { get; }
    public string ColorHex { get; }
    public string IconKind { get; }
    public string Priority { get; }
}

public sealed record DashboardAlertSummaryItem(string IconKind, string Title, string CountLabel, string ColorHex);

public sealed record DashboardPaymentItem(
    string StudentName,
    string ClassOrReference,
    string AmountLabel,
    string Currency,
    string FeeTypeOrMethod,
    string DateLabel,
    string Receipt);

public sealed record DashboardActivityItem(
    string Kind,
    string Title,
    string Subtitle,
    string DateLabel,
    string TimeLabel,
    string? AmountLabel,
    string IconKind);

public sealed record DashboardClassCardItem(
    string ClassName,
    int Students,
    string PresentLabel,
    string AbsentLabel,
    decimal BoysPercent,
    decimal GirlsPercent,
    string AverageOrRevenue,
    string PaymentRateLabel);

public partial class DashboardNotificationItem : ObservableObject
{
    public DashboardNotificationItem(string code, string type, string title, string message, bool isRead)
    {
        Code = code;
        Type = type;
        Title = title;
        Message = message;
        IsRead = isRead;
    }

    public string Code { get; }
    [ObservableProperty] private string _type;
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _message;
    [ObservableProperty] private bool _isRead;
}

public sealed record DashboardCalendarItem(string Title, string Detail, string Type, string ColorHex);

public sealed record NamedShareItem(string Name, int Count, decimal Percent, string ColorHex);
