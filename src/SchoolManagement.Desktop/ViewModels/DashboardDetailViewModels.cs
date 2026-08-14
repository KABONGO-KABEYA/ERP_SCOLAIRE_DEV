using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Dashboard.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class DashboardCollectedDetailViewModel : ViewModelBase
{
    private readonly IPromoterDashboardApiService _dashboardApi;
    private readonly INavigationService _navigation;

    public DashboardCollectedDetailViewModel(
        IPromoterDashboardApiService dashboardApi,
        INavigationService navigation)
    {
        _dashboardApi = dashboardApi;
        _navigation = navigation;
        _ = LoadAsync();
    }

    public IReadOnlyList<DashboardDetailScopeOption> ScopeOptions { get; } =
    [
        new(DashboardDetailScope.Today, "Aujourd'hui"),
        new(DashboardDetailScope.Month, "Ce mois"),
        new(DashboardDetailScope.Year, "Cette année"),
        new(DashboardDetailScope.Custom, "Période personnalisée")
    ];

    [ObservableProperty] private DashboardDetailScopeOption? _selectedScope;
    [ObservableProperty] private DateTime? _customFromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime? _customToDate = DateTime.Today;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _totalLabel = "—";
    [ObservableProperty] private string _currency = "CDF";

    public ObservableCollection<DashboardPaymentDetailRow> PaymentRows { get; } = [];
    public ObservableCollection<DashboardRevenuePointRow> RevenuePointRows { get; } = [];

    public bool IsCustomScope => SelectedScope?.Scope == DashboardDetailScope.Custom;
    public bool ShowPaymentLines => SelectedScope?.Scope is DashboardDetailScope.Today or DashboardDetailScope.Custom;

    partial void OnSelectedScopeChanged(DashboardDetailScopeOption? value)
    {
        OnPropertyChanged(nameof(IsCustomScope));
        OnPropertyChanged(nameof(ShowPaymentLines));
        if (value is not null)
        {
            _ = LoadAsync();
        }
    }

    partial void OnCustomFromDateChanged(DateTime? value) => TryReloadCustom();
    partial void OnCustomToDateChanged(DateTime? value) => TryReloadCustom();

    private void TryReloadCustom()
    {
        if (SelectedScope?.Scope == DashboardDetailScope.Custom)
        {
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private void GoBack() => _navigation.NavigateBack();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (SelectedScope is null)
        {
            SelectedScope = ScopeOptions.FirstOrDefault(s => s.Scope == DashboardDetailScope.Year) ?? ScopeOptions[2];
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        PaymentRows.Clear();
        RevenuePointRows.Clear();

        try
        {
            var scope = SelectedScope.Scope;
            DateOnly? from = null;
            DateOnly? to = null;
            if (scope == DashboardDetailScope.Custom)
            {
                if (CustomFromDate is null || CustomToDate is null)
                {
                    StatusMessage = "Sélectionnez une période valide.";
                    return;
                }

                from = DateOnly.FromDateTime(CustomFromDate.Value);
                to = DateOnly.FromDateTime(CustomToDate.Value);
            }

            if (scope is DashboardDetailScope.Today or DashboardDetailScope.Custom)
            {
                var lines = await _dashboardApi.GetPaymentsAsync(scope, fromDate: from, toDate: to);
                foreach (var line in lines)
                {
                    PaymentRows.Add(new DashboardPaymentDetailRow(
                        line.PaymentDateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR")),
                        line.StudentName,
                        line.Reference,
                        line.Amount,
                        line.Currency,
                        line.Method));
                }

                var total = lines.Sum(l => l.Amount);
                Currency = lines.FirstOrDefault()?.Currency ?? Currency;
                TotalLabel = $"{total:N0} {Currency}";
            }
            else
            {
                var points = await _dashboardApi.GetRevenueDetailAsync(scope, fromDate: from, toDate: to);
                foreach (var point in points)
                {
                    RevenuePointRows.Add(new DashboardRevenuePointRow(
                        point.Label,
                        point.Amount,
                        Currency));
                }

                var total = points.Sum(p => p.Amount);
                TotalLabel = $"{total:N0} {Currency}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowPaymentLines));
        }
    }
}

public sealed record DashboardDetailScopeOption(DashboardDetailScope Scope, string Label)
{
    public override string ToString() => Label;
}

public sealed record DashboardPaymentDetailRow(
    string DateLabel,
    string StudentName,
    string Reference,
    decimal Amount,
    string Currency,
    string Method)
{
    public string AmountLabel => $"{Amount:N0} {Currency}";
}

public sealed record DashboardRevenuePointRow(string Label, decimal Amount, string Currency)
{
    public string AmountLabel => $"{Amount:N0} {Currency}";
}

public partial class DashboardExpensesDetailViewModel : ViewModelBase
{
    private readonly IPromoterDashboardApiService _dashboardApi;
    private readonly INavigationService _navigation;

    public DashboardExpensesDetailViewModel(
        IPromoterDashboardApiService dashboardApi,
        INavigationService navigation)
    {
        _dashboardApi = dashboardApi;
        _navigation = navigation;
        _ = LoadAsync();
    }

    public IReadOnlyList<DashboardDetailScopeOption> ScopeOptions { get; } =
    [
        new(DashboardDetailScope.Today, "Aujourd'hui"),
        new(DashboardDetailScope.Month, "Ce mois"),
        new(DashboardDetailScope.Year, "Cette année"),
        new(DashboardDetailScope.Custom, "Période personnalisée")
    ];

    [ObservableProperty] private DashboardDetailScopeOption? _selectedScope;
    [ObservableProperty] private DateTime? _customFromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime? _customToDate = DateTime.Today;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _totalLabel = "—";
    [ObservableProperty] private string _currency = "CDF";

    public ObservableCollection<DashboardExpenseAccountGroup> AccountGroups { get; } = [];

    public bool IsCustomScope => SelectedScope?.Scope == DashboardDetailScope.Custom;

    partial void OnSelectedScopeChanged(DashboardDetailScopeOption? value)
    {
        OnPropertyChanged(nameof(IsCustomScope));
        if (value is not null)
        {
            _ = LoadAsync();
        }
    }

    partial void OnCustomFromDateChanged(DateTime? value) => TryReloadCustom();
    partial void OnCustomToDateChanged(DateTime? value) => TryReloadCustom();

    private void TryReloadCustom()
    {
        if (SelectedScope?.Scope == DashboardDetailScope.Custom)
        {
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private void GoBack() => _navigation.NavigateBack();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (SelectedScope is null)
        {
            SelectedScope = ScopeOptions[1];
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        AccountGroups.Clear();

        try
        {
            var scope = SelectedScope.Scope;
            DateOnly? from = null;
            DateOnly? to = null;
            if (scope == DashboardDetailScope.Custom)
            {
                if (CustomFromDate is null || CustomToDate is null)
                {
                    StatusMessage = "Sélectionnez une période valide.";
                    return;
                }

                from = DateOnly.FromDateTime(CustomFromDate.Value);
                to = DateOnly.FromDateTime(CustomToDate.Value);
            }

            var lines = await _dashboardApi.GetExpensesAsync(scope, fromDate: from, toDate: to);
            Currency = lines.FirstOrDefault()?.Currency ?? Currency;
            var total = lines.Sum(l => l.Amount);
            TotalLabel = $"{total:N0} {Currency}";

            foreach (var accountGroup in lines
                         .GroupBy(l => new { l.DestinationId, l.AccountTypeName })
                         .OrderBy(g => g.Key.AccountTypeName))
            {
                var periods = BuildPeriodNodes(scope, accountGroup.ToList());
                AccountGroups.Add(new DashboardExpenseAccountGroup(
                    accountGroup.Key.AccountTypeName,
                    accountGroup.Sum(x => x.Amount),
                    Currency,
                    periods));
            }
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

    private static IReadOnlyList<DashboardExpensePeriodNode> BuildPeriodNodes(
        DashboardDetailScope scope,
        IReadOnlyList<DashboardExpenseLineDto> lines)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");

        if (scope == DashboardDetailScope.Today || scope == DashboardDetailScope.Custom)
        {
            return
            [
                new DashboardExpensePeriodNode(
                    "Dépenses",
                    lines.Sum(l => l.Amount),
                    lines.FirstOrDefault()?.Currency ?? "CDF",
                    lines.Select(l => new DashboardExpenseLineRow(
                        l.Label,
                        l.Amount,
                        l.Currency,
                        l.ExpenseDate.ToString("dd/MM/yyyy", fr),
                        l.AccountTypeName)).ToList())
            ];
        }

        if (scope == DashboardDetailScope.Month)
        {
            return lines
                .GroupBy(l => l.ExpenseDate)
                .OrderByDescending(g => g.Key)
                .Select(dayGroup => new DashboardExpensePeriodNode(
                    dayGroup.Key.ToString("dd/MM/yyyy", fr),
                    dayGroup.Sum(x => x.Amount),
                    dayGroup.First().Currency,
                    dayGroup.Select(l => new DashboardExpenseLineRow(
                        l.Label,
                        l.Amount,
                        l.Currency,
                        l.ExpenseDate.ToString("dd/MM/yyyy", fr),
                        l.AccountTypeName)).ToList()))
                .ToList();
        }

        return lines
            .GroupBy(l => new DateOnly(l.ExpenseDate.Year, l.ExpenseDate.Month, 1))
            .OrderByDescending(g => g.Key)
            .Select(monthGroup =>
            {
                var monthLabel = monthGroup.Key.ToString("MMMM yyyy", fr);
                if (monthLabel.Length > 0)
                {
                    monthLabel = char.ToUpper(monthLabel[0], fr) + monthLabel[1..];
                }

                var dayNodes = monthGroup
                    .GroupBy(l => l.ExpenseDate)
                    .OrderByDescending(g => g.Key)
                    .Select(dayGroup => new DashboardExpensePeriodNode(
                        dayGroup.Key.ToString("dd/MM/yyyy", fr),
                        dayGroup.Sum(x => x.Amount),
                        dayGroup.First().Currency,
                        dayGroup.Select(l => new DashboardExpenseLineRow(
                            l.Label,
                            l.Amount,
                            l.Currency,
                            l.ExpenseDate.ToString("dd/MM/yyyy", fr),
                            l.AccountTypeName)).ToList()))
                    .ToList();

                return new DashboardExpensePeriodNode(
                    monthLabel,
                    monthGroup.Sum(x => x.Amount),
                    monthGroup.First().Currency,
                    [],
                    dayNodes);
            })
            .ToList();
    }
}

public sealed record DashboardExpenseLineRow(
    string Label,
    decimal Amount,
    string Currency,
    string DateLabel,
    string AccountTypeName)
{
    public string AmountLabel => $"{Amount:N0} {Currency}";
}

public sealed record DashboardExpensePeriodNode(
    string Title,
    decimal Total,
    string Currency,
    IReadOnlyList<DashboardExpenseLineRow> Lines,
    IReadOnlyList<DashboardExpensePeriodNode>? Children = null)
{
    public string TotalLabel => $"{Total:N0} {Currency}";
    public bool HasChildren => Children is { Count: > 0 };
}

public sealed record DashboardExpenseAccountGroup(
    string AccountTypeName,
    decimal Total,
    string Currency,
    IReadOnlyList<DashboardExpensePeriodNode> Periods)
{
    public string TotalLabel => $"{Total:N0} {Currency}";
}

public partial class StudentAttendancePlaceholderViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "Présence des élèves";
    [ObservableProperty] private string _message =
        "Le module de présence des élèves sera disponible prochainement. La navigation depuis le tableau de bord est déjà préparée.";
}
