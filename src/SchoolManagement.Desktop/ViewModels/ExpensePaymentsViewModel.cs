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
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public sealed record ExpenseCurrencyOption(Currency Value, string Label);

public sealed record ExpenseDestinationOption(
    Guid Id,
    string Code,
    string Name,
    decimal AllocatedAmount,
    decimal SpentAmount,
    decimal AvailableAmount,
    string Currency = "CDF")
{
    public string DisplayName => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} — {Name}";
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
    public string DestinationName => string.IsNullOrWhiteSpace(Source.DestinationCode)
        ? Source.DestinationName
        : $"{Source.DestinationCode} — {Source.DestinationName}";
    public decimal Amount => Source.Amount;
    public string Currency => Source.Currency;
    public string UserDisplay => Source.AuthorizedByName;
}

/// <summary>Gestion premium des dépenses — consultation + saisie pour comptables.</summary>
public partial class ExpensePaymentsViewModel : ViewModelBase
{
    private readonly IAccountingApiService _accountingApi;
    private bool _initialized;
    private bool _suppressReload;

    public ExpensePaymentsViewModel(IAccountingApiService accountingApi)
    {
        _accountingApi = accountingApi;
        ExpenseDate = DateTime.Today;
        SelectedCurrency = Currencies[0];
        FilterCurrency = FilterCurrencies[0];
        FilterCategory = Categories[0];
        FilterStatus = Statuses[0];
        NewCategory = Categories.Skip(1).FirstOrDefault() ?? Categories[0];
        StatusMessage = "Consultez et enregistrez les dépenses de l'établissement.";
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
    }

    private void OnGlobalAcademicYearChanged()
    {
        if (_initialized && !_suppressReload)
            _ = ReloadBalancesAndSearchAsync();
    }

    public ObservableCollection<ExpensePaymentRow> Payments { get; } = [];
    public ObservableCollection<ExpenseDestinationOption> Destinations { get; } = [];
    public ObservableCollection<ExpenseDestinationOption> FilterDestinations { get; } = [];

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
    partial void OnSelectedCurrencyChanged(ExpenseCurrencyOption? value) => NotifyComputed();
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
    partial void OnNewAmountTextChanged(string value) => NotifyComputed();
    partial void OnAttachmentPathChanged(string? value) => OnPropertyChanged(nameof(HasAttachment));
    partial void OnListSearchTextChanged(string value)
    {
        if (!_suppressReload)
            _ = SearchAsync();
    }
    partial void OnSelectedDestinationChanged(ExpenseDestinationOption? value)
    {
        RefreshSelectedBalance();
        OnPropertyChanged(nameof(SelectedAccountTitle));
        if (value is not null && value.Id != Guid.Empty)
        {
            SelectedAccountCurrency = value.Currency;
            SelectedCurrency = Currencies.FirstOrDefault(c => c.Label == value.Currency) ?? SelectedCurrency;
        }
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

            if (!string.IsNullOrWhiteSpace(ListSearchText))
            {
                var term = ListSearchText.Trim();
                items = items.Where(p =>
                    p.Label.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.Reference.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.BeneficiaryName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || p.DestinationName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Statut/catégorie : UI prête ; les paiements enregistrés sont traités comme « Validée ».
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
    private void ViewPayment(ExpensePaymentRow? row)
    {
        row ??= SelectedPayment;
        if (row is null) return;
        MessageBox.Show(
            $"Réf. : {row.Reference}\nDate : {row.ExpenseDate:dd/MM/yyyy}\nLibellé : {row.Label}\n"
            + $"Bénéficiaire : {row.BeneficiaryName}\nCompte : {row.DestinationName}\n"
            + $"Montant : {row.Amount:N2} {row.Currency}\nAutorisé par : {row.UserDisplay}",
            "Détail de la dépense",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
        NewReference = row.Reference;
        ExpenseDate = row.ExpenseDate.ToDateTime(TimeOnly.MinValue);
        SelectedCurrency = Currencies.FirstOrDefault(c => c.Label == row.Currency) ?? Currencies[0];
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

        if (amount > AvailableBalance)
        {
            var confirm = MessageBox.Show(
                $"Le solde après dépense sera négatif ({BalanceAfterExpense:N2}).\nContinuer quand même ?",
                "Solde insuffisant",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }

        IsBusy = true;
        try
        {
            var label = NewLabel.Trim();
            if (!string.IsNullOrWhiteSpace(NewReference))
                label = $"{label} [{NewReference.Trim()}]";
            if (!string.IsNullOrWhiteSpace(NewObservations))
                label = $"{label} — {NewObservations.Trim()}";
            if (HasAttachment)
                label = $"{label} (PJ: {AttachmentFileName})";

            await _accountingApi.CreateExpensePaymentAsync(new CreateExpensePaymentRequest(
                yearId.Value,
                SelectedDestination.Id,
                label,
                NewBeneficiaryName.Trim(),
                authorized,
                amount,
                SelectedCurrency?.Value ?? Currency.CDF,
                DateOnly.FromDateTime(ExpenseDate.Value)));

            if (keepFormOpen)
            {
                var dest = SelectedDestination;
                ClearFormFields();
                SelectedDestination = dest;
                StatusMessage = "Dépense enregistrée — formulaire prêt pour une nouvelle saisie.";
            }
            else
            {
                ClearFormFields();
                StatusMessage = "Dépense enregistrée.";
            }

            await ReloadBalancesAndSearchAsync();
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
        ClearAttachment();
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
        FilterDestinations.Add(new ExpenseDestinationOption(
            Guid.Empty, string.Empty, "Tous les comptes", 0, 0, 0, "CDF"));

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
            var balances = await _accountingApi.GetExpenseBalancesAsync(yearId.Value);
            foreach (var balance in balances.OrderBy(b => b.DestinationName))
            {
                var option = new ExpenseDestinationOption(
                    balance.DestinationId,
                    balance.DestinationCode,
                    balance.DestinationName,
                    balance.AllocatedAmount,
                    balance.SpentAmount,
                    balance.AvailableAmount,
                    balance.Currency);
                Destinations.Add(option);
                FilterDestinations.Add(option);
            }

            SelectedDestination ??= Destinations.FirstOrDefault();
            FilterDestination ??= FilterDestinations.FirstOrDefault();
            RefreshSelectedBalance();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            SelectedDestination = null;
            FilterDestination = FilterDestinations.FirstOrDefault();
            RefreshSelectedBalance();
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
            return;
        }

        AllocatedAmount = SelectedDestination.AllocatedAmount;
        SpentAmount = SelectedDestination.SpentAmount;
        AvailableBalance = SelectedDestination.AvailableAmount;
        SelectedAccountCurrency = string.IsNullOrWhiteSpace(SelectedDestination.Currency)
            ? "CDF"
            : SelectedDestination.Currency;
        NotifyComputed();
    }
}
