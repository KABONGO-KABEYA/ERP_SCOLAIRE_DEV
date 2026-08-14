using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.Views;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Desktop.ViewModels;

public sealed record ExpenseCurrencyOption(Currency Value, string Label);

/// <summary>Compte unique (plusieurs devises possibles côté soldes).</summary>
public sealed record ExpenseDestinationOption(
    Guid Id,
    string Code,
    string Name)
{
    /// <summary>Intitulé affiché (sans code compte).</summary>
    public string DisplayName => Name;
}

/// <summary>Solde d'un compte pour une devise donnée.</summary>
public sealed record ExpenseCurrencyBalanceLine(
    Guid? CurrencyId,
    string CurrencyCode,
    decimal AllocatedAmount,
    decimal SpentAmount,
    decimal AvailableAmount)
{
    public string AllocatedDisplay => $"{AllocatedAmount:N2} {CurrencyCode}";
    public string SpentDisplay => $"{SpentAmount:N2} {CurrencyCode}";
    public string AvailableDisplay => $"{AvailableAmount:N2} {CurrencyCode}";
}

public sealed record ExpenseFilterOption(string Key, string Label);

public sealed record ExpensePaymentRow(
    ExpensePaymentDto Source,
    string StatusLabel,
    string StatusTone)
{
    public Guid Id => Source.Id;
    public DateOnly ExpenseDate => Source.ExpenseDate;
    public string Reference => Source.Reference;
    public string Label => Source.Label;
    public string BeneficiaryName => Source.BeneficiaryName;
    public string DestinationName => Source.DestinationName;
    public decimal Amount => Source.Amount;
    public string Currency => Source.Currency;
    public string UserDisplay => Source.AuthorizedByName;
    public string? CategoryLabel => Source.CategoryLabel;
    public string? ExternalReference => Source.ExternalReference;
    public bool HasAttachment => Source.HasAttachment;
}

/// <summary>Gestion premium des dépenses — consultation + saisie pour comptables.</summary>
public partial class ExpensePaymentsViewModel : ViewModelBase
{
    private readonly IAccountingApiService _accountingApi;
    private readonly ICurrencyApiService _currencyApi;
    private readonly IAuthSessionService _authSession;
    private bool _initialized;
    private bool _suppressReload;
    private List<ExpenseDestinationBalanceDto> _balanceRows = [];
    private IReadOnlyList<CreateExpensePaymentAllocationLine>? _pendingAllocations;
    private Guid? _pendingPrimaryCurrencyId;
    private CancellationTokenSource? _autoSplitCts;
    private bool _allocationDialogOpen;
    private decimal? _dismissedAutoSplitAmount;

    public ExpensePaymentsViewModel(
        IAccountingApiService accountingApi,
        ICurrencyApiService currencyApi,
        IAuthSessionService authSession)
    {
        _accountingApi = accountingApi;
        _currencyApi = currencyApi;
        _authSession = authSession;
        ExpenseDate = DateTime.Today;
        SelectedCurrency = Currencies[0];
        FilterCurrency = FilterCurrencies[0];
        FilterCategory = Categories[0];
        FilterStatus = Statuses[0];
        NewCategory = Categories.Skip(1).FirstOrDefault() ?? Categories[0];
        StatusMessage = "Consultez et enregistrez les dépenses de l'établissement.";
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
    }

    public bool CanManageExpenses => SessionPermissions.Can(_authSession, Permissions.AccountingManage);

    private void OnGlobalAcademicYearChanged()
    {
        if (_initialized && !_suppressReload)
            _ = ReloadBalancesAndSearchAsync();
    }

    public ObservableCollection<ExpensePaymentRow> Payments { get; } = [];
    public ObservableCollection<ExpenseDestinationOption> Destinations { get; } = [];
    public ObservableCollection<ExpenseDestinationOption> FilterDestinations { get; } = [];
    public ObservableCollection<ExpenseCurrencyBalanceLine> SelectedAccountBalances { get; } = [];

    public bool HasSelectedAccountBalances => SelectedAccountBalances.Count > 0;

    public bool HasPendingMultiCurrencyAllocation =>
        _pendingAllocations is { Count: > 0 };

    public string PendingAllocationSummary =>
        !HasPendingMultiCurrencyAllocation
            ? string.Empty
            : $"Répartition multi-devises prête ({_pendingAllocations!.Count} devise(s)).";

    public IReadOnlyList<ExpenseCurrencyOption> Currencies { get; } =
    [
        new ExpenseCurrencyOption(Currency.CDF, "CDF"),
        new ExpenseCurrencyOption(Currency.USD, "USD")
    ];

    public IReadOnlyList<ExpenseFilterOption> FilterCurrencies { get; } =
    [
        new ExpenseFilterOption("all", "Toutes"),
        new ExpenseFilterOption("CDF", "CDF"),
        new ExpenseFilterOption("USD", "USD")
    ];

    public IReadOnlyList<ExpenseFilterOption> Categories { get; } =
    [
        new ExpenseFilterOption("all", "Toutes"),
        new ExpenseFilterOption("fonctionnement", "Fonctionnement"),
        new ExpenseFilterOption("pedagogie", "Pédagogie"),
        new ExpenseFilterOption("salaires", "Salaires / Prestations"),
        new ExpenseFilterOption("infrastructure", "Infrastructure"),
        new ExpenseFilterOption("autre", "Autre")
    ];

    public IReadOnlyList<ExpenseFilterOption> Statuses { get; } =
    [
        new ExpenseFilterOption("all", "Tous"),
        new ExpenseFilterOption("validee", "Validée"),
        new ExpenseFilterOption("attente", "En attente"),
        new ExpenseFilterOption("annulee", "Annulée")
    ];

    [ObservableProperty] private ExpenseDestinationOption? _selectedDestination;
    [ObservableProperty] private ExpenseDestinationOption? _filterDestination;
    [ObservableProperty] private ExpenseCurrencyOption? _selectedCurrency;
    [ObservableProperty] private ExpenseFilterOption? _filterCurrency;
    [ObservableProperty] private ExpenseFilterOption? _filterCategory;
    [ObservableProperty] private ExpenseFilterOption? _filterStatus;
    [ObservableProperty] private ExpenseFilterOption? _newCategory;
    [ObservableProperty] private ExpensePaymentRow? _selectedPayment;
    [ObservableProperty] private string _listSearchText = string.Empty;
    [ObservableProperty] private string _newLabel = string.Empty;
    [ObservableProperty] private string _newBeneficiaryName = string.Empty;
    [ObservableProperty] private string _newAuthorizedByName = string.Empty;
    [ObservableProperty] private string _newReference = string.Empty;
    [ObservableProperty] private string _newObservations = string.Empty;
    [ObservableProperty] private string _newAmountText = string.Empty;
    [ObservableProperty] private string? _attachmentPath;
    [ObservableProperty] private string? _attachmentFileName;
    [ObservableProperty] private string? _attachmentSizeLabel;
    [ObservableProperty] private DateTime? _expenseDate;
    [ObservableProperty] private DateTime? _filterFromDate;
    [ObservableProperty] private DateTime? _filterToDate;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isFiltersExpanded = true;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private decimal _availableBalance;
    [ObservableProperty] private decimal _allocatedAmount;
    [ObservableProperty] private decimal _spentAmount;
    [ObservableProperty] private string _selectedAccountCurrency = "CDF";

    public decimal AverageAmount => TotalCount > 0 ? Math.Round(TotalAmount / TotalCount, 2) : 0m;
    public decimal ExecutionRatePercent =>
        AllocatedAmount > 0 ? Math.Round(SpentAmount / AllocatedAmount * 100m, 2) : 0m;
    public double ExecutionRateDouble => (double)ExecutionRatePercent;
    public string FiltersToggleLabel => IsFiltersExpanded ? "Masquer les filtres" : "Afficher les filtres";
    public string DisplayCurrency =>
        string.IsNullOrWhiteSpace(SelectedAccountCurrency) ? "CDF" : SelectedAccountCurrency;

    public decimal ParsedAmount
    {
        get
        {
            var raw = NewAmountText.Replace(" ", string.Empty).Replace(',', '.');
            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)
                ? amount
                : 0m;
        }
    }

    public decimal BalanceAfterExpense => AvailableBalance - ParsedAmount;
    public bool IsBalanceNegative => BalanceAfterExpense < 0;
    public bool HasAttachment => !string.IsNullOrWhiteSpace(AttachmentPath);
    public string SelectedAccountTitle => SelectedDestination is null
        ? "Aucun compte sélectionné"
        : SelectedDestination.DisplayName;

    public string AvailableBalanceDisplay =>
        SelectedDestination is null
            ? "Solde disponible : —"
            : $"Solde disponible : {AvailableBalance:N2} {DisplayCurrency}";

    public string AvailableBalanceKpi => $"{AvailableBalance:N2} {DisplayCurrency}";
    public string TotalAmountKpi => $"{TotalAmount:N2} {DisplayCurrency}";
    public string AverageAmountKpi => $"{AverageAmount:N2} {DisplayCurrency}";
    public string AllocatedAmountDisplay => $"{AllocatedAmount:N2} {DisplayCurrency}";
    public string SpentAmountDisplay => $"{SpentAmount:N2} {DisplayCurrency}";
    public string BalanceAfterDisplay => $"{BalanceAfterExpense:N2} {DisplayCurrency}";
    public string ParsedAmountDisplay => $"{ParsedAmount:N2} {DisplayCurrency}";

    public string PaginationLabel =>
        TotalCount == 0
            ? "Aucune dépense"
            : $"Affichage de 1 à {Math.Min(Payments.Count, TotalCount)} sur {TotalCount} dépenses.";

    public string TotalsSummary => $"{TotalCount} dépense(s) — Total {TotalAmount:N2} {DisplayCurrency}";

    partial void OnIsFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(FiltersToggleLabel));
    partial void OnSelectedAccountCurrencyChanged(string value) => NotifyComputed();
    partial void OnAvailableBalanceChanged(decimal value) => NotifyComputed();
    partial void OnAllocatedAmountChanged(decimal value) => NotifyComputed();
    partial void OnSpentAmountChanged(decimal value) => NotifyComputed();
    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalsSummary));
        OnPropertyChanged(nameof(AverageAmount));
        OnPropertyChanged(nameof(AverageAmountKpi));
        OnPropertyChanged(nameof(PaginationLabel));
    }
    partial void OnTotalAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(TotalsSummary));
        OnPropertyChanged(nameof(AverageAmount));
        OnPropertyChanged(nameof(TotalAmountKpi));
        OnPropertyChanged(nameof(AverageAmountKpi));
    }
    partial void OnNewAmountTextChanged(string value)
    {
        NotifyComputed();
        _ = ScheduleAutoOpenMultiCurrencyAllocationAsync();
    }
    partial void OnAttachmentPathChanged(string? value) => OnPropertyChanged(nameof(HasAttachment));
    partial void OnListSearchTextChanged(string value)
    {
        if (!_suppressReload)
            _ = SearchAsync();
    }
    partial void OnSelectedDestinationChanged(ExpenseDestinationOption? value)
    {
        RebuildSelectedAccountBalances();
        RefreshSelectedBalance();
        OnPropertyChanged(nameof(SelectedAccountTitle));
        OnPropertyChanged(nameof(HasSelectedAccountBalances));
        _ = ScheduleAutoOpenMultiCurrencyAllocationAsync();
    }

    partial void OnSelectedCurrencyChanged(ExpenseCurrencyOption? value)
    {
        RefreshSelectedBalance();
        NotifyComputed();
        _ = ScheduleAutoOpenMultiCurrencyAllocationAsync();
    }

    private void NotifyComputed()
    {
        OnPropertyChanged(nameof(AvailableBalanceDisplay));
        OnPropertyChanged(nameof(AvailableBalanceKpi));
        OnPropertyChanged(nameof(TotalAmountKpi));
        OnPropertyChanged(nameof(AverageAmountKpi));
        OnPropertyChanged(nameof(AllocatedAmountDisplay));
        OnPropertyChanged(nameof(SpentAmountDisplay));
        OnPropertyChanged(nameof(BalanceAfterDisplay));
        OnPropertyChanged(nameof(ParsedAmountDisplay));
        OnPropertyChanged(nameof(DisplayCurrency));
        OnPropertyChanged(nameof(ParsedAmount));
        OnPropertyChanged(nameof(BalanceAfterExpense));
        OnPropertyChanged(nameof(IsBalanceNegative));
        OnPropertyChanged(nameof(ExecutionRatePercent));
        OnPropertyChanged(nameof(ExecutionRateDouble));
        OnPropertyChanged(nameof(TotalsSummary));
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await InitializeAsync();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        _suppressReload = true;
        try
        {
            ExpenseDate = DateTime.Today;
            SelectedCurrency = Currencies[0];
            await ReloadBalancesAndSearchAsync();
            StatusMessage = Destinations.Count == 0
                ? "Aucun compte lié à une clé de répartition active pour cette année."
                : "Prêt — consultez ou enregistrez une dépense.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            _suppressReload = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    [RelayCommand]
    private void StartNewExpense()
    {
        ClearFormFields();
        StatusMessage = "Nouvelle dépense — renseignez le formulaire à droite.";
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
            return;
        }

        IsBusy = true;
        try
        {
            var destinationId = FilterDestination is null || FilterDestination.Id == Guid.Empty
                ? (Guid?)null
                : FilterDestination.Id;

            var result = await _accountingApi.SearchExpensePaymentsAsync(new ExpenseSearchRequest(
                yearId.Value,
                FilterFromDate.HasValue ? DateOnly.FromDateTime(FilterFromDate.Value) : null,
                FilterToDate.HasValue ? DateOnly.FromDateTime(FilterToDate.Value) : null,
                destinationId,
                PageSize: 500));

            IEnumerable<ExpensePaymentDto> items = result.Items;
            if (FilterCurrency is { Key: not "all" })
                items = items.Where(p => p.Currency.Equals(FilterCurrency.Key, StringComparison.OrdinalIgnoreCase));

            if (FilterCategory is { Key: not "all" })
                items = items.Where(p =>
                    string.Equals(p.Category, FilterCategory.Key, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(ListSearchText))
            {
                var term = ListSearchText.Trim();
                items = items.Where(p =>
                    p.Label.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.Reference.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (p.ExternalReference?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || p.BeneficiaryName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.DestinationName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Statut : les dépenses enregistrées sont « Validée ».
            if (FilterStatus is { Key: "attente" or "annulee" })
                items = [];

            Payments.Clear();
            foreach (var item in items)
            {
                Payments.Add(new ExpensePaymentRow(item, "Validée", "success"));
            }

            TotalCount = Payments.Count;
            TotalAmount = Payments.Sum(p => p.Amount);
            StatusMessage = $"{TotalCount} dépense(s) — total {TotalAmount:N2}.";
            OnPropertyChanged(nameof(PaginationLabel));
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
    private void ResetFilters()
    {
        _suppressReload = true;
        try
        {
            FilterDestination = FilterDestinations.FirstOrDefault();
            FilterFromDate = null;
            FilterToDate = null;
            FilterCurrency = FilterCurrencies[0];
            FilterCategory = Categories[0];
            FilterStatus = Statuses[0];
            ListSearchText = string.Empty;
        }
        finally
        {
            _suppressReload = false;
        }

        _ = SearchAsync();
        StatusMessage = "Filtres réinitialisés.";
    }

    [RelayCommand]
    private async Task CreateAsync() => await SaveInternalAsync(keepFormOpen: false);

    [RelayCommand]
    private async Task CreateAndNewAsync() => await SaveInternalAsync(keepFormOpen: true);

    [RelayCommand]
    private void CancelForm()
    {
        ClearFormFields();
        StatusMessage = "Saisie annulée.";
    }

    [RelayCommand]
    private void BrowseAttachment()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pièce justificative",
            Filter = "Documents|*.pdf;*.png;*.jpg;*.jpeg;*.doc;*.docx|Tous les fichiers|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        SetAttachment(dialog.FileName);
    }

    [RelayCommand]
    private void ClearAttachment()
    {
        AttachmentPath = null;
        AttachmentFileName = null;
        AttachmentSizeLabel = null;
    }

    [RelayCommand]
    private async Task ViewPaymentAsync(ExpensePaymentRow? row)
    {
        row ??= SelectedPayment;
        if (row is null) return;

        try
        {
            IsBusy = true;
            var detail = await _accountingApi.GetExpensePaymentByIdAsync(row.Id);
            var window = new ExpensePaymentDetailWindow(detail)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Détail de la dépense", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenMultiCurrencyAllocationAsync()
    {
        var amount = ParsedAmount;
        if (amount <= 0)
        {
            StatusMessage = "Saisissez d'abord un montant de dépense.";
            return;
        }

        if (SelectedDestination is null || SelectedAccountBalances.Count == 0)
        {
            StatusMessage = "Sélectionnez un compte avec des soldes disponibles.";
            return;
        }

        var opened = await ShowAllocationDialogAsync(amount);
        if (opened)
        {
            StatusMessage = PendingAllocationSummary + " Cliquez sur Enregistrer pour valider.";
            OnPropertyChanged(nameof(HasPendingMultiCurrencyAllocation));
            OnPropertyChanged(nameof(PendingAllocationSummary));
        }
    }

    [RelayCommand]
    private void ClearPendingAllocation()
    {
        _pendingAllocations = null;
        _pendingPrimaryCurrencyId = null;
        _dismissedAutoSplitAmount = null;
        OnPropertyChanged(nameof(HasPendingMultiCurrencyAllocation));
        OnPropertyChanged(nameof(PendingAllocationSummary));
        StatusMessage = "Répartition multi-devises annulée.";
        _ = ScheduleAutoOpenMultiCurrencyAllocationAsync();
    }

    [RelayCommand]
    private void EditPayment(ExpensePaymentRow? row)
    {
        row ??= SelectedPayment;
        if (row is null) return;
        SelectedDestination = Destinations.FirstOrDefault(d => d.Id == row.Source.DestinationId) ?? SelectedDestination;
        NewLabel = row.Label;
        NewBeneficiaryName = row.BeneficiaryName;
        NewAuthorizedByName = row.UserDisplay;
        NewAmountText = row.Amount.ToString("N2", CultureInfo.CurrentCulture);
        NewReference = row.ExternalReference ?? string.Empty;
        NewObservations = row.Source.Observations ?? string.Empty;
        ExpenseDate = row.ExpenseDate.ToDateTime(TimeOnly.MinValue);
        SelectedCurrency = Currencies.FirstOrDefault(c => c.Label == row.Currency) ?? Currencies[0];
        NewCategory = Categories.FirstOrDefault(c =>
                          string.Equals(c.Key, row.Source.Category, StringComparison.OrdinalIgnoreCase))
                      ?? Categories.Skip(1).FirstOrDefault()
                      ?? Categories[0];
        StatusMessage = "Modification préparée dans le formulaire (ré-enregistrement = nouvelle écriture pour l’instant).";
    }

    [RelayCommand]
    private void DeletePayment(ExpensePaymentRow? row)
    {
        row ??= SelectedPayment;
        if (row is null) return;
        MessageBox.Show(
            "La suppression définitive des dépenses sera disponible dans une prochaine itération (piste d’audit comptable).",
            "Suppression",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ExportList() =>
        StatusMessage = "Export Excel/PDF — à brancher sur le moteur d’export existant.";

    [RelayCommand]
    private void PrintList() =>
        StatusMessage = "Impression de la liste — à brancher sur le service d’impression.";

    private async Task SaveInternalAsync(bool keepFormOpen)
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null || SelectedDestination is null)
        {
            StatusMessage = "Sélectionnez l'année scolaire et un compte.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewLabel))
        {
            StatusMessage = "Renseignez le libellé de la dépense.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewBeneficiaryName))
        {
            StatusMessage = "Renseignez le bénéficiaire.";
            return;
        }

        var authorized = string.IsNullOrWhiteSpace(NewAuthorizedByName)
            ? "Direction"
            : NewAuthorizedByName.Trim();

        var amount = ParsedAmount;
        if (amount <= 0)
        {
            StatusMessage = "Renseignez un montant valide supérieur à zéro.";
            return;
        }

        if (ExpenseDate is null)
        {
            StatusMessage = "Renseignez la date de la dépense.";
            return;
        }

        IReadOnlyList<CreateExpensePaymentAllocationLine>? allocations = _pendingAllocations;
        Guid? primaryCurrencyId = _pendingPrimaryCurrencyId
            ?? SelectedAccountBalances.FirstOrDefault(b =>
                string.Equals(b.CurrencyCode, SelectedCurrency?.Label, StringComparison.OrdinalIgnoreCase))?.CurrencyId;

        if (allocations is null && amount > AvailableBalance)
        {
            if (SelectedAccountBalances.Count <= 1)
            {
                StatusMessage =
                    $"Solde insuffisant en {DisplayCurrency} ({AvailableBalance:N2}). Aucune autre devise disponible sur ce compte.";
                return;
            }

            // Ouverture automatique de la répartition (sans confirmation).
            var ok = await ShowAllocationDialogAsync(amount);
            if (!ok)
            {
                StatusMessage =
                    $"Solde insuffisant en {DisplayCurrency}. Validez une répartition multi-devises pour enregistrer.";
                return;
            }

            allocations = _pendingAllocations;
            primaryCurrencyId = _pendingPrimaryCurrencyId ?? primaryCurrencyId;
            OnPropertyChanged(nameof(HasPendingMultiCurrencyAllocation));
            OnPropertyChanged(nameof(PendingAllocationSummary));
        }

        IsBusy = true;
        try
        {
            var label = NewLabel.Trim();
            var categoryKey = NewCategory is null || NewCategory.Key == "all"
                ? null
                : NewCategory.Key;

            string? attachmentStoragePath = null;
            string? attachmentFileName = null;
            if (HasAttachment && !string.IsNullOrWhiteSpace(AttachmentPath) && File.Exists(AttachmentPath))
            {
                (attachmentStoragePath, attachmentFileName) = StoreExpenseAttachmentLocally(
                    yearId.Value,
                    AttachmentPath);
            }

            var created = await _accountingApi.CreateExpensePaymentAsync(new CreateExpensePaymentRequest(
                yearId.Value,
                SelectedDestination.Id,
                label,
                NewBeneficiaryName.Trim(),
                authorized,
                amount,
                SelectedCurrency?.Value ?? Currency.CDF,
                DateOnly.FromDateTime(ExpenseDate.Value),
                ExpenseRequestId: null,
                PrimaryCurrencyId: primaryCurrencyId,
                CurrencyAllocations: allocations,
                ExternalReference: string.IsNullOrWhiteSpace(NewReference) ? null : NewReference.Trim(),
                Category: categoryKey,
                Observations: string.IsNullOrWhiteSpace(NewObservations) ? null : NewObservations.Trim(),
                AttachmentFileName: attachmentFileName,
                AttachmentStoragePath: attachmentStoragePath));

            _pendingAllocations = null;
            _pendingPrimaryCurrencyId = null;
            _dismissedAutoSplitAmount = null;
            OnPropertyChanged(nameof(HasPendingMultiCurrencyAllocation));
            OnPropertyChanged(nameof(PendingAllocationSummary));

            var splitInfo = created.HasMultiCurrencyAllocation
                ? $"\nRépartition : {created.Allocations?.Count ?? 0} devise(s)."
                : string.Empty;

            MessageBox.Show(
                $"Dépense enregistrée.\nRéf. système : {created.Reference}\n"
                + $"Montant : {created.Amount:N2} {created.Currency}\n"
                + $"Compte : {created.DestinationName}{splitInfo}",
                "Enregistrement",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (keepFormOpen)
            {
                var dest = SelectedDestination;
                ClearFormFields();
                SelectedDestination = dest;
                StatusMessage = $"Dépense {created.Reference} enregistrée — formulaire prêt pour une nouvelle saisie.";
            }
            else
            {
                ClearFormFields();
                StatusMessage = $"Dépense {created.Reference} enregistrée.";
            }

            await ReloadBalancesAndSearchAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Enregistrement impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Copie la pièce justificative dans le stockage local partagé avec l'API.
    /// </summary>
    private static (string RelativePath, string FileName) StoreExpenseAttachmentLocally(
        Guid academicYearId,
        string sourcePath)
    {
        var root = Environment.GetEnvironmentVariable("FILE_STORAGE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ERP_Administration_Scolaire",
                "storage");
        }

        var fileName = Path.GetFileName(sourcePath);
        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{fileName}";
        var relative = Path.Combine("depenses", academicYearId.ToString("N"), safeName);
        var target = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(sourcePath, target, overwrite: true);
        return (relative.Replace('\\', '/'), fileName);
    }

    private async Task ScheduleAutoOpenMultiCurrencyAllocationAsync()
    {
        _autoSplitCts?.Cancel();
        _autoSplitCts?.Dispose();
        _autoSplitCts = new CancellationTokenSource();
        var token = _autoSplitCts.Token;

        try
        {
            // Laisse finir la saisie du montant avant d'ouvrir la fenêtre.
            await Task.Delay(450, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || _allocationDialogOpen || IsBusy)
            return;

        var amount = ParsedAmount;
        if (amount <= 0 || SelectedDestination is null)
            return;

        if (amount <= AvailableBalance)
        {
            _dismissedAutoSplitAmount = null;
            return;
        }

        if (SelectedAccountBalances.Count <= 1)
        {
            StatusMessage =
                $"Solde insuffisant en {DisplayCurrency} ({AvailableBalance:N2}). Aucune autre devise sur ce compte.";
            return;
        }

        // Déjà une répartition validée pour ce montant, ou utilisateur a annulé pour ce montant.
        if (HasPendingMultiCurrencyAllocation)
            return;
        if (_dismissedAutoSplitAmount.HasValue
            && Math.Abs(_dismissedAutoSplitAmount.Value - amount) < 0.009m)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        await dispatcher.InvokeAsync(async () =>
        {
            if (_allocationDialogOpen || HasPendingMultiCurrencyAllocation || amount <= AvailableBalance)
                return;

            StatusMessage =
                $"Montant supérieur au solde {DisplayCurrency} — ouverture de la répartition multi-devises…";
            var ok = await ShowAllocationDialogAsync(amount);
            if (ok)
            {
                _dismissedAutoSplitAmount = null;
                StatusMessage = PendingAllocationSummary + " Cliquez sur Enregistrer pour valider.";
                OnPropertyChanged(nameof(HasPendingMultiCurrencyAllocation));
                OnPropertyChanged(nameof(PendingAllocationSummary));
            }
            else
            {
                _dismissedAutoSplitAmount = amount;
                StatusMessage =
                    "Répartition annulée. Ajustez le montant ou rouvrez via « Répartir sur plusieurs devises ».";
            }
        });
    }

    private async Task<bool> ShowAllocationDialogAsync(decimal amount)
    {
        if (_allocationDialogOpen)
            return false;

        var primaryCode = SelectedCurrency?.Label ?? "CDF";
        var primaryId = SelectedAccountBalances.FirstOrDefault(b =>
            string.Equals(b.CurrencyCode, primaryCode, StringComparison.OrdinalIgnoreCase))?.CurrencyId;

        _allocationDialogOpen = true;
        try
        {
            var window = new ExpenseMultiCurrencyAllocationWindow(
                SelectedAccountTitle,
                amount,
                primaryCode,
                primaryId,
                SelectedAccountBalances.ToList(),
                DateOnly.FromDateTime(ExpenseDate ?? DateTime.Today),
                _currencyApi,
                _authSession)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = window.ShowDialog();
            if (result == true && window.Confirmed && window.ConfirmedLines.Count > 0)
            {
                _pendingAllocations = window.ConfirmedLines;
                _pendingPrimaryCurrencyId = primaryId;
                _dismissedAutoSplitAmount = null;
                return true;
            }

            return false;
        }
        finally
        {
            _allocationDialogOpen = false;
        }
    }

    private void SetAttachment(string path)
    {
        AttachmentPath = path;
        AttachmentFileName = Path.GetFileName(path);
        try
        {
            var size = new FileInfo(path).Length;
            AttachmentSizeLabel = size < 1024
                ? $"{size} o"
                : size < 1024 * 1024
                    ? $"{size / 1024.0:0.#} Ko"
                    : $"{size / (1024.0 * 1024.0):0.##} Mo";
        }
        catch
        {
            AttachmentSizeLabel = "—";
        }
    }

    private void ClearFormFields()
    {
        NewLabel = string.Empty;
        NewBeneficiaryName = string.Empty;
        NewAuthorizedByName = string.Empty;
        NewReference = string.Empty;
        NewObservations = string.Empty;
        NewAmountText = string.Empty;
        NewCategory = Categories.Skip(1).FirstOrDefault() ?? Categories[0];
        ExpenseDate = DateTime.Today;
        SelectedCurrency = Currencies[0];
        AttachmentPath = null;
        AttachmentFileName = null;
        AttachmentSizeLabel = null;
        _pendingAllocations = null;
        _pendingPrimaryCurrencyId = null;
        _dismissedAutoSplitAmount = null;
        OnPropertyChanged(nameof(HasPendingMultiCurrencyAllocation));
        OnPropertyChanged(nameof(PendingAllocationSummary));
        RefreshSelectedBalance();
    }

    private async Task ReloadBalancesAndSearchAsync()
    {
        await ReloadBalancesAsync();
        await SearchAsync();
    }

    private async Task ReloadBalancesAsync()
    {
        Destinations.Clear();
        FilterDestinations.Clear();
        SelectedAccountBalances.Clear();
        _balanceRows = [];
        FilterDestinations.Add(new ExpenseDestinationOption(Guid.Empty, string.Empty, "Tous les comptes"));

        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            SelectedDestination = null;
            FilterDestination = FilterDestinations[0];
            RefreshSelectedBalance();
            StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
            return;
        }

        try
        {
            _balanceRows = (await _accountingApi.GetExpenseBalancesAsync(yearId.Value)).ToList();
            foreach (var group in _balanceRows
                         .GroupBy(b => b.DestinationId)
                         .OrderBy(g => g.First().DestinationName))
            {
                var first = group.First();
                var option = new ExpenseDestinationOption(
                    first.DestinationId,
                    first.DestinationCode,
                    first.DestinationName);
                Destinations.Add(option);
                FilterDestinations.Add(option);
            }

            SelectedDestination ??= Destinations.FirstOrDefault();
            FilterDestination ??= FilterDestinations.FirstOrDefault();
            RebuildSelectedAccountBalances();
            RefreshSelectedBalance();
            OnPropertyChanged(nameof(HasSelectedAccountBalances));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            SelectedDestination = null;
            FilterDestination = FilterDestinations.FirstOrDefault();
            SelectedAccountBalances.Clear();
            RefreshSelectedBalance();
            OnPropertyChanged(nameof(HasSelectedAccountBalances));
        }
    }

    private void RebuildSelectedAccountBalances()
    {
        SelectedAccountBalances.Clear();
        if (SelectedDestination is null || SelectedDestination.Id == Guid.Empty)
        {
            return;
        }

        foreach (var row in _balanceRows
                     .Where(b => b.DestinationId == SelectedDestination.Id)
                     .OrderBy(b => b.Currency))
        {
            SelectedAccountBalances.Add(new ExpenseCurrencyBalanceLine(
                row.CurrencyId,
                row.Currency,
                row.AllocatedAmount,
                row.SpentAmount,
                row.AvailableAmount));
        }

        // Si la devise sélectionnée n'existe pas sur ce compte, bascule sur la 1re disponible.
        if (SelectedAccountBalances.Count > 0)
        {
            var currentCode = SelectedCurrency?.Label;
            var match = SelectedAccountBalances.FirstOrDefault(b =>
                string.Equals(b.CurrencyCode, currentCode, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                var firstCode = SelectedAccountBalances[0].CurrencyCode;
                SelectedCurrency = Currencies.FirstOrDefault(c => c.Label == firstCode) ?? SelectedCurrency;
            }
        }
    }

    private void RefreshSelectedBalance()
    {
        if (SelectedDestination is null || SelectedDestination.Id == Guid.Empty)
        {
            AvailableBalance = 0;
            AllocatedAmount = 0;
            SpentAmount = 0;
            SelectedAccountCurrency = SelectedCurrency?.Label ?? "CDF";
            NotifyComputed();
            return;
        }

        var currencyCode = SelectedCurrency?.Label ?? "CDF";
        var line = SelectedAccountBalances.FirstOrDefault(b =>
            string.Equals(b.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
            ?? SelectedAccountBalances.FirstOrDefault();

        if (line is null)
        {
            AvailableBalance = 0;
            AllocatedAmount = 0;
            SpentAmount = 0;
            SelectedAccountCurrency = currencyCode;
        }
        else
        {
            AllocatedAmount = line.AllocatedAmount;
            SpentAmount = line.SpentAmount;
            AvailableBalance = line.AvailableAmount;
            SelectedAccountCurrency = line.CurrencyCode;
        }

        NotifyComputed();
    }
}
