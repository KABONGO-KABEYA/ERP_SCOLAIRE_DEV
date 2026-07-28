using System.Windows;
using SchoolManagement.Application.Accounting.DTOs;

namespace SchoolManagement.Desktop.Views;

public partial class ExpensePaymentDetailWindow : Window
{
    public ExpensePaymentDetailWindow(ExpensePaymentDto payment)
    {
        InitializeComponent();
        DataContext = new ExpensePaymentDetailModel(payment);
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}

public sealed class ExpensePaymentDetailModel
{
    public ExpensePaymentDetailModel(ExpensePaymentDto payment)
    {
        Reference = payment.Reference;
        ExpenseDate = payment.ExpenseDate;
        Label = payment.Label;
        BeneficiaryName = payment.BeneficiaryName;
        AuthorizedByName = payment.AuthorizedByName;
        DestinationLabel = payment.DestinationName;
        AmountDisplay = $"{payment.Amount:N2} {payment.Currency}";
        ExternalReference = string.IsNullOrWhiteSpace(payment.ExternalReference) ? "—" : payment.ExternalReference;
        CategoryLabel = string.IsNullOrWhiteSpace(payment.CategoryLabel) ? "—" : payment.CategoryLabel;
        Observations = string.IsNullOrWhiteSpace(payment.Observations) ? "—" : payment.Observations;
        AttachmentLabel = payment.HasAttachment
            ? (payment.AttachmentFileName ?? "Pièce jointe")
            : "Aucune";
        Allocations = (payment.Allocations ?? [])
            .Select(a => new ExpensePaymentAllocationDetailRow(a, payment.Currency))
            .ToList();
        AllocationSummary = Allocations.Count == 0
            ? "Aucun mouvement multi-devises enregistré (dépense mono-devise)."
            : Allocations.Count == 1
                ? "Financement en une seule devise."
                : $"{Allocations.Count} devises ont participé au financement de cette dépense.";
    }

    public string Reference { get; }
    public DateOnly ExpenseDate { get; }
    public string Label { get; }
    public string BeneficiaryName { get; }
    public string AuthorizedByName { get; }
    public string DestinationLabel { get; }
    public string AmountDisplay { get; }
    public string ExternalReference { get; }
    public string CategoryLabel { get; }
    public string Observations { get; }
    public string AttachmentLabel { get; }
    public string AllocationSummary { get; }
    public IReadOnlyList<ExpensePaymentAllocationDetailRow> Allocations { get; }
}

public sealed class ExpensePaymentAllocationDetailRow
{
    public ExpensePaymentAllocationDetailRow(ExpensePaymentAllocationDto source, string primaryCurrencyCode)
    {
        CurrencyCode = source.CurrencyCode;
        AmountDisplay = $"{source.Amount:N2} {source.CurrencyCode}";
        RateDisplay = source.AppliedExchangeRate.ToString("N8");
        RateDirectionLabel = source.RateDirectionLabel;
        EquivalentDisplay = $"{source.EquivalentInPrimaryCurrency:N2} {primaryCurrencyCode}";
    }

    public string CurrencyCode { get; }
    public string AmountDisplay { get; }
    public string RateDisplay { get; }
    public string RateDirectionLabel { get; }
    public string EquivalentDisplay { get; }
}
