using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public enum FinanceSection
{
    Encaissements = 0,
    CategoriesTarifaires = 1,
    Rapports = 2,
    DemandesPaiement = 3,
    Depenses = 4,
    SituationPaiements = 5
}

/// <summary>Module Financier : encaissements, catégories tarifaires, rapports (config dans Paramètres).</summary>
public partial class FinanceHubViewModel : ViewModelBase
{
    public FinanceHubViewModel(
        EncaissementsViewModel encaissements,
        PricingCategoryAssignmentViewModel pricingCategoryAssignment,
        FinancialReportsViewModel financialReports,
        PaymentSituationReportViewModel paymentSituationReport,
        ExpenseRequestsViewModel expenseRequests,
        ExpensePaymentsViewModel expensePayments)
    {
        Encaissements = encaissements;
        PricingCategoryAssignment = pricingCategoryAssignment;
        FinancialReports = financialReports;
        PaymentSituationReport = paymentSituationReport;
        ExpenseRequests = expenseRequests;
        ExpensePayments = expensePayments;
        ApplyNavigation(FinanceNavCatalog.DefaultItem);
    }

    public EncaissementsViewModel Encaissements { get; }

    public PricingCategoryAssignmentViewModel PricingCategoryAssignment { get; }

    public FinancialReportsViewModel FinancialReports { get; }

    public PaymentSituationReportViewModel PaymentSituationReport { get; }

    public ExpenseRequestsViewModel ExpenseRequests { get; }

    public ExpensePaymentsViewModel ExpensePayments { get; }

    [ObservableProperty] private FinanceSection _selectedSection = FinanceSection.Encaissements;

    public bool IsEncaissementsSelected => SelectedSection == FinanceSection.Encaissements;
    public bool IsCategoriesTarifairesSelected => SelectedSection == FinanceSection.CategoriesTarifaires;
    public bool IsRapportsSelected => SelectedSection == FinanceSection.Rapports;
    public bool IsSituationPaiementsSelected => SelectedSection == FinanceSection.SituationPaiements;
    public bool IsDemandesPaiementSelected => SelectedSection == FinanceSection.DemandesPaiement;
    public bool IsDepensesSelected => SelectedSection == FinanceSection.Depenses;

    public string? ActiveNavKey { get; private set; }

    public string SelectedSectionTitle => FinanceNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Title
        ?? "Encaissements";

    public string SelectedSectionDescription => FinanceNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Subtitle
        ?? "Suivi des paiements scolaires et opérations d'encaissement";

    public void ApplyNavigation(FinanceNavItem item)
    {
        ActiveNavKey = item.Key;
        SelectedSection = item.Section;
        OnPropertyChanged(nameof(ActiveNavKey));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
    }

    partial void OnSelectedSectionChanged(FinanceSection value)
    {
        OnPropertyChanged(nameof(IsEncaissementsSelected));
        OnPropertyChanged(nameof(IsCategoriesTarifairesSelected));
        OnPropertyChanged(nameof(IsRapportsSelected));
        OnPropertyChanged(nameof(IsSituationPaiementsSelected));
        OnPropertyChanged(nameof(IsDemandesPaiementSelected));
        OnPropertyChanged(nameof(IsDepensesSelected));

        if (value == FinanceSection.Rapports)
        {
            _ = FinancialReports.EnsureInitializedAsync();
        }
        else if (value == FinanceSection.SituationPaiements)
        {
            _ = PaymentSituationReport.EnsureInitializedAsync();
        }
    }

    [RelayCommand]
    private void SelectSection(string? section)
    {
        if (Enum.TryParse<FinanceSection>(section, out var parsed))
        {
            SelectedSection = parsed;
            var item = FinanceNavCatalog.Groups
                .SelectMany(g => g.Items)
                .FirstOrDefault(i => i.Section == parsed);
            if (item is not null)
            {
                ApplyNavigation(item);
            }
        }
    }
}

public partial class KeyDetailEditorRow : ObservableObject
{
    [ObservableProperty] private Guid _destinationId;
    [ObservableProperty] private string _destinationCode = string.Empty;
    [ObservableProperty] private string _destinationName = string.Empty;
    [ObservableProperty] private AllocationCalculationType _calculationType = AllocationCalculationType.Pourcentage;
    [ObservableProperty] private decimal _value;
    [ObservableProperty] private int _sortOrder;
}
