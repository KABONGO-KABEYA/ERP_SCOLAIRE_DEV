using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Desktop.Helpers;
using SchoolManagement.Desktop.Models;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.Views;

public enum EncaissementActionMode
{
    CollectPayment,
    PaymentHistory,
    FinancialSituation,
    Allocations,
    Withholdings,
    ReprintReceipt,
    EditPayment,
    CancelPayment
}

public partial class EncaissementActionWindow : Window
{
    private readonly EncaissementActionMode _mode;
    private readonly StudentPaymentSituationDto _situation;
    private readonly IPaymentApiService _paymentApi;
    private readonly IFinanceApiService _financeApi;
    private readonly IRevenueAllocationApiService _allocationApi;
    private readonly IWithholdingApiService _withholdingApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly IFeeTypeStatementPrintService _statementPrint;
    private readonly IAuthSessionService? _authSession;
    private readonly bool _canMutatePaidPayments;

    private readonly List<PaymentListItem> _allPayments = [];
    private readonly ObservableCollection<InstallmentCollectRow> _installmentRows = [];
    private PaymentDto? _selectedPayment;
    private decimal _editFeeAmountBaseline;
    private FeeTypeStatementDto? _receiptStatement;
    private bool _busy;
    private bool _suppressDistribute;
    private bool _suppressTodayPaymentSync;

    public bool NeedsRefresh { get; private set; }

    public EncaissementActionWindow(
        EncaissementActionMode mode,
        StudentPaymentSituationDto situation,
        IPaymentApiService paymentApi,
        IFinanceApiService financeApi,
        IRevenueAllocationApiService allocationApi,
        IWithholdingApiService withholdingApi,
        ISchoolApiService schoolApi,
        IFeeTypeStatementPrintService statementPrint,
        IAuthSessionService? authSession = null)
    {
        InitializeComponent();
        _mode = mode;
        _situation = situation;
        _paymentApi = paymentApi;
        _financeApi = financeApi;
        _allocationApi = allocationApi;
        _withholdingApi = withholdingApi;
        _schoolApi = schoolApi;
        _statementPrint = statementPrint;
        _authSession = authSession;
        _canMutatePaidPayments = authSession?.IsAdministrator == true;
        _ = _schoolApi;

        SubtitleText.Text = $"{situation.FullName} — {situation.FeeTypeName} ({situation.AcademicYearLabel})";
        ConfigureForMode();
    }

    private void ConfigureForMode()
    {
        HideAllPanels();
        PrimaryButton.Visibility = Visibility.Collapsed;
        HistoryActionsPanel.Visibility = Visibility.Collapsed;

        switch (_mode)
        {
            case EncaissementActionMode.CollectPayment:
                Title = "Encaisser un paiement";
                TitleText.Text = "Encaisser un paiement";
                Width = 900;
                Height = 700;
                MinWidth = 780;
                MinHeight = 560;
                CollectPanel.Visibility = Visibility.Visible;
                PrimaryButton.Content = "Encaisser";
                PrimaryButton.Visibility = Visibility.Visible;
                SecondaryButton.Content = "Annuler";
                break;

            case EncaissementActionMode.PaymentHistory:
                Title = "Historique des paiements";
                TitleText.Text = "Historique des paiements";
                HistoryPanel.Visibility = Visibility.Visible;
                HistoryActionsPanel.Visibility = Visibility.Visible;
                HistoryEditBtn.Visibility = _canMutatePaidPayments ? Visibility.Visible : Visibility.Collapsed;
                HistoryCancelBtn.Visibility = _canMutatePaidPayments ? Visibility.Visible : Visibility.Collapsed;
                PrimaryButton.Visibility = Visibility.Collapsed;
                SecondaryButton.Content = "Fermer";
                break;

            case EncaissementActionMode.FinancialSituation:
                Title = "Situation financière";
                TitleText.Text = "Situation financière";
                SituationPanel.Visibility = Visibility.Visible;
                SecondaryButton.Content = "Fermer";
                break;

            case EncaissementActionMode.Allocations:
                Title = "Répartitions";
                TitleText.Text = "Répartitions des recettes";
                AllocationsPanel.Visibility = Visibility.Visible;
                SecondaryButton.Content = "Fermer";
                break;

            case EncaissementActionMode.Withholdings:
                Title = "Retenues";
                TitleText.Text = "Retenues applicables";
                WithholdingsPanel.Visibility = Visibility.Visible;
                SecondaryButton.Content = "Fermer";
                break;

            case EncaissementActionMode.ReprintReceipt:
                Title = "Réimpression du relevé";
                TitleText.Text = "Aperçu du relevé de frais scolaire";
                HistoryPanel.Visibility = Visibility.Visible;
                ReprintPanel.Visibility = Visibility.Collapsed;
                HistoryActionsPanel.Visibility = Visibility.Collapsed;
                PrimaryButton.Content = "Imprimer";
                PrimaryButton.Visibility = Visibility.Visible;
                PrimaryButton.IsEnabled = false;
                SecondaryButton.Content = "Fermer";
                break;

            case EncaissementActionMode.EditPayment:
                Title = "Modifier un paiement";
                TitleText.Text = "Modifier le dernier versement";
                HistoryPanel.Visibility = Visibility.Collapsed;
                EditPanel.Visibility = Visibility.Visible;
                HistoryActionsPanel.Visibility = Visibility.Collapsed;
                PrimaryButton.Content = "Enregistrer le nouveau montant";
                PrimaryButton.Visibility = Visibility.Visible;
                PrimaryButton.IsEnabled = false;
                SecondaryButton.Content = "Fermer";
                break;

            case EncaissementActionMode.CancelPayment:
                Title = "Annuler un paiement";
                TitleText.Text = "Annuler le dernier versement";
                HistoryPanel.Visibility = Visibility.Collapsed;
                CancelPanel.Visibility = Visibility.Visible;
                HistoryActionsPanel.Visibility = Visibility.Collapsed;
                PrimaryButton.Content = "Confirmer l'annulation";
                PrimaryButton.Style = (Style)FindResource("ErpDangerButton");
                PrimaryButton.Visibility = Visibility.Visible;
                PrimaryButton.IsEnabled = false;
                SecondaryButton.Content = "Fermer";
                break;
        }
    }

    private void HideAllPanels()
    {
        CollectPanel.Visibility = Visibility.Collapsed;
        HistoryPanel.Visibility = Visibility.Collapsed;
        SituationPanel.Visibility = Visibility.Collapsed;
        AllocationsPanel.Visibility = Visibility.Collapsed;
        WithholdingsPanel.Visibility = Visibility.Collapsed;
        ReprintPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Collapsed;
        CancelPanel.Visibility = Visibility.Collapsed;
        ExportPdfButton.Visibility = Visibility.Collapsed;
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Chargement…");
            switch (_mode)
            {
                case EncaissementActionMode.CollectPayment:
                    await LoadCollectAsync();
                    break;
                case EncaissementActionMode.PaymentHistory:
                case EncaissementActionMode.ReprintReceipt:
                case EncaissementActionMode.EditPayment:
                case EncaissementActionMode.CancelPayment:
                    await LoadPaymentsAsync();
                    break;
                case EncaissementActionMode.FinancialSituation:
                    await LoadSituationAsync();
                    break;
                case EncaissementActionMode.Allocations:
                    await LoadAllocationsAsync();
                    break;
                case EncaissementActionMode.Withholdings:
                    await LoadWithholdingsAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadCollectAsync()
    {
        if (_situation.FeeTypeId is null)
        {
            ErrorText.Text = "Aucun type de frais associé à cette situation.";
            PrimaryButton.IsEnabled = false;
            return;
        }

        CollectHeaderText.Text =
            $"{_situation.FullName}  ·  {_situation.FeeTypeName}  ·  {_situation.Currency}";

        var remaining = Math.Max(0, _situation.AmountExpected - _situation.AmountPaid);
        CollectBalanceText.Text =
            $"Solde global : {remaining:N0} {_situation.Currency} (payé {_situation.AmountPaid:N0} / attendu {_situation.AmountExpected:N0})";

        var plan = await _financeApi.GetInstallmentPaymentPlanAsync(
            _situation.EnrollmentId,
            _situation.FeeTypeId.Value);

        _installmentRows.Clear();
        foreach (var line in plan.Lines.OrderBy(l => l.SortOrder).ThenBy(l => l.InstallmentName))
        {
            _installmentRows.Add(new InstallmentCollectRow(
                line.FeeInstallmentId,
                line.InstallmentName,
                line.SortOrder,
                line.AmountExpected,
                line.AmountPaid,
                line.Remaining));
        }

        InstallmentsGrid.ItemsSource = _installmentRows;

        if (_installmentRows.Count == 0)
        {
            CollectNoInstallmentsText.Visibility = Visibility.Visible;
            TotalToDistributeBox.IsEnabled = false;
            PrimaryButton.IsEnabled = false;
            UpdateCollectTotals();
            return;
        }

        CollectNoInstallmentsText.Visibility = Visibility.Collapsed;
        _suppressTodayPaymentSync = true;
        try
        {
            InstallmentPaymentCascade.Redistribute(_installmentRows, 0);
        }
        finally
        {
            _suppressTodayPaymentSync = false;
        }

        InstallmentPaymentCascade.RefreshEditability(_installmentRows);
        UpdateCollectTotals();

        if (remaining > 0)
        {
            _suppressDistribute = true;
            TotalToDistributeBox.Text = remaining.ToString("0.##", CultureInfo.InvariantCulture);
            _suppressDistribute = false;
            _suppressTodayPaymentSync = true;
            try
            {
                InstallmentPaymentCascade.Redistribute(_installmentRows, remaining);
            }
            finally
            {
                _suppressTodayPaymentSync = false;
            }

            InstallmentPaymentCascade.RefreshEditability(_installmentRows);
            UpdateCollectTotals();
        }
    }

    private void TotalToDistributeBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressDistribute || _mode != EncaissementActionMode.CollectPayment)
        {
            return;
        }

        if (!InstallmentPaymentCascade.TryParseDecimal(TotalToDistributeBox.Text, out var total) || total < 0)
        {
            total = 0;
        }

        _suppressTodayPaymentSync = true;
        try
        {
            InstallmentPaymentCascade.Redistribute(_installmentRows, total);
        }
        finally
        {
            _suppressTodayPaymentSync = false;
        }

        InstallmentPaymentCascade.RefreshEditability(_installmentRows);
        UpdateCollectTotals();
    }

    private void TodayPaymentBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTodayPaymentSync || _suppressDistribute)
        {
            return;
        }

        if (sender is not TextBox { DataContext: InstallmentCollectRow row })
        {
            return;
        }

        InstallmentPaymentCascade.ApplyTodayPaymentEdit(_installmentRows, row, commitClamp: false);
        SyncTotalFromRows();
        UpdateCollectTotals();
    }

    private void TodayPaymentBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: InstallmentCollectRow row })
        {
            return;
        }

        InstallmentPaymentCascade.ApplyTodayPaymentEdit(_installmentRows, row, commitClamp: true);
        SyncTotalFromRows();
        UpdateCollectTotals();
    }

    private void SyncTotalFromRows()
    {
        var sum = _installmentRows.Sum(r => r.TodayPayment);
        _suppressDistribute = true;
        try
        {
            TotalToDistributeBox.Text = sum.ToString("0.##", CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressDistribute = false;
        }
    }

    private void UpdateCollectTotals()
    {
        var versements = _installmentRows.Sum(r => r.TodayPayment);
        var remainingAfter = _installmentRows.Sum(r => Math.Max(0, r.Remaining - r.TodayPayment));
        CollectTotalsText.Text =
            $"Total des versements : {versements:N0} {_situation.Currency}  ·  " +
            $"Reste après ce paiement : {remainingAfter:N0} {_situation.Currency}";
    }

    private async Task LoadPaymentsAsync()
    {
        var result = await _paymentApi.SearchAsync(new PaymentSearchRequest(_situation.StudentId, null, null, 1, 200));
        _allPayments.Clear();

        var feeTypeId = _situation.FeeTypeId;
        var filterByFeeType = feeTypeId.HasValue
            && _mode is EncaissementActionMode.EditPayment or EncaissementActionMode.CancelPayment;

        foreach (var p in result.Items
                     .OrderByDescending(x => x.PaymentDate)
                     .ThenByDescending(x => x.Id))
        {
            if (!filterByFeeType)
            {
                _allPayments.Add(new PaymentListItem(p));
                continue;
            }

            try
            {
                var detail = await _paymentApi.GetByIdAsync(p.Id);
                var feeLines = detail.Lines.Where(l => l.FeeTypeId == feeTypeId!.Value).ToList();
                if (feeLines.Count == 0)
                {
                    continue;
                }

                _allPayments.Add(new PaymentListItem(p, feeLines.Sum(l => l.Amount)));
            }
            catch
            {
                // ignore payments we cannot inspect
            }
        }

        RefreshPaymentsGrid();

        var latestCompleted = GetLatestCompletedPayment();
        if (_mode is EncaissementActionMode.CancelPayment)
        {
            if (latestCompleted is null)
            {
                CancelLastAmountText.Text = "Aucun versement";
                CancelPaymentInfoText.Text = "Aucun versement complet à annuler pour ce type de frais.";
                PrimaryButton.IsEnabled = false;
                StatusText.Text = "Aucun versement complet à annuler.";
            }
            else
            {
                ShowCancelPanel(latestCompleted.Dto, latestCompleted.DisplayAmount);
            }

            return;
        }

        if (_mode is EncaissementActionMode.EditPayment)
        {
            if (latestCompleted is null)
            {
                EditLastAmountText.Text = "Aucun versement";
                EditPaymentInfoText.Text = "Aucun versement complet à modifier pour ce type de frais.";
                EditAmountBox.Text = string.Empty;
                PrimaryButton.IsEnabled = false;
                StatusText.Text = "Aucun versement complet à modifier.";
            }
            else
            {
                ShowEditPanel(latestCompleted.Dto, latestCompleted.DisplayAmount);
            }

            return;
        }

        if (_mode is EncaissementActionMode.ReprintReceipt)
        {
            if (latestCompleted is not null)
            {
                PaymentsGrid.SelectedItem = latestCompleted;
            }
        }
        else if (_allPayments.Count > 0)
        {
            PaymentsGrid.SelectedIndex = 0;
        }

        if (_allPayments.Count == 0)
        {
            StatusText.Text = "Aucun paiement trouvé pour cet élève.";
        }
    }

    private PaymentListItem? GetLatestCompletedPayment() =>
        _allPayments
            .Where(p => p.Status == PaymentStatus.Complet)
            .OrderByDescending(p => p.Dto.PaymentDate)
            .ThenByDescending(p => p.Dto.Id)
            .FirstOrDefault();

    private bool IsLatestCompletedPayment(PaymentDto payment)
    {
        var latest = GetLatestCompletedPayment();
        return latest is not null && latest.Dto.Id == payment.Id;
    }

    private void RefreshPaymentsGrid()
    {
        var hideCancelled = HideCancelledCheck.IsChecked == true;
        var items = hideCancelled
            ? _allPayments.Where(p => p.Status != PaymentStatus.Annule).ToList()
            : _allPayments.ToList();
        PaymentsGrid.ItemsSource = items;
    }

    private async Task LoadSituationAsync()
    {
        SituationFeeTypeText.Text = _situation.FeeTypeName;
        SituationPaidText.Text = $"{_situation.AmountPaid:N0} {_situation.Currency}";
        SituationExpectedText.Text = $"{_situation.AmountExpected:N0} {_situation.Currency}";
        SituationBalanceText.Text = $"{_situation.Balance:N0} {_situation.Currency}";
        SituationStatusText.Text = _situation.PaymentStatusLabel;

        var summary = await _paymentApi.GetStudentFinancialSummaryAsync(
            _situation.StudentId,
            _situation.AcademicYearId);
        SummaryTotalsText.Text =
            $"Total dû : {summary.TotalDue:N0} {summary.Currency}\n" +
            $"Total payé : {summary.TotalPaid:N0} {summary.Currency}\n" +
            $"Solde global : {summary.Balance:N0} {summary.Currency}";
    }

    private async Task LoadAllocationsAsync()
    {
        var result = await _allocationApi.SearchEntriesAsync(new RevenueAllocationSearchRequest(
            _situation.AcademicYearId,
            null,
            null,
            _situation.StudentId,
            null,
            null,
            _situation.FeeTypeId,
            1,
            100));
        AllocationsGrid.ItemsSource = result.Items;
        StatusText.Text = result.TotalCount == 0
            ? "Aucune répartition trouvée."
            : $"{result.TotalCount} écriture(s) — total {result.Totals.GrandTotal:N0}.";
    }

    private async Task LoadWithholdingsAsync()
    {
        if (_situation.FeeTypeId is null)
        {
            ErrorText.Text = "Aucun type de frais associé.";
            return;
        }

        var balance = Math.Max(0, _situation.Balance);
        var gross = Math.Max(balance, _situation.AmountExpected);
        if (gross <= 0)
        {
            gross = _situation.AmountExpected;
        }

        var result = await _withholdingApi.CalculateAsync(new WithholdingCalculateRequest(
            gross,
            new WithholdingResolveContext(
                _situation.AcademicYearId,
                _situation.FeeTypeId.Value,
                null,
                _situation.FeePricingCategoryId)));

        WithholdingTotalsText.Text =
            $"Montant brut : {result.GrossAmount:N0} {_situation.Currency}  ·  " +
            $"Retenues : {result.TotalWithheld:N0}  ·  " +
            $"Net : {result.NetAmount:N0}";
        WithholdingsGrid.ItemsSource = result.Lines;
        if (result.Lines.Count == 0)
        {
            StatusText.Text = "Aucune retenue applicable pour ce contexte.";
        }
    }

    private void HideCancelledCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RefreshPaymentsGrid();
    }

    private async void PaymentsGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PaymentsGrid.SelectedItem is not PaymentListItem item)
        {
            _selectedPayment = null;
            if (_mode is EncaissementActionMode.ReprintReceipt or EncaissementActionMode.EditPayment or EncaissementActionMode.CancelPayment)
            {
                PrimaryButton.IsEnabled = false;
            }

            return;
        }

        _selectedPayment = item.Dto;

        if (_mode == EncaissementActionMode.ReprintReceipt)
        {
            await ShowReceiptPreviewAsync(item.Dto);
        }
        else if (_mode == EncaissementActionMode.EditPayment)
        {
            var amount = item.DisplayAmount;
            ShowEditPanel(item.Dto, amount);
        }
        else if (_mode == EncaissementActionMode.CancelPayment)
        {
            ShowCancelPanel(item.Dto, item.DisplayAmount);
        }
    }

    private async Task ShowReceiptPreviewAsync(PaymentDto payment)
    {
        try
        {
            SetBusy(true, "Chargement du relevé…");
            _receiptStatement = await _statementPrint.LoadAsync(payment.Id, _situation.FeeTypeId);
            HistoryPanel.Visibility = Visibility.Collapsed;
            ReprintPanel.Visibility = Visibility.Visible;
            ReceiptPreviewText.Text = FormatStatementPreview(_receiptStatement);
            PrimaryButton.IsEnabled = true;
            ExportPdfButton.Visibility = Visibility.Visible;
            TitleText.Text = $"Relevé {_receiptStatement.ReceiptNumber}";
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            PrimaryButton.IsEnabled = false;
            ExportPdfButton.Visibility = Visibility.Collapsed;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowEditPanel(PaymentDto payment, decimal? displayAmount = null)
    {
        if (!_canMutatePaidPayments)
        {
            ErrorText.Text = "Seul l'administrateur peut modifier un frais déjà payé.";
            PrimaryButton.IsEnabled = false;
            return;
        }

        if (payment.Status == PaymentStatus.Annule)
        {
            ErrorText.Text = "Impossible de modifier un paiement annulé.";
            PrimaryButton.IsEnabled = false;
            return;
        }

        if (!IsLatestCompletedPayment(payment))
        {
            ErrorText.Text =
                "Seul le dernier versement de ce type de frais peut être modifié. " +
                "Traitez d'abord les versements plus récents (ordre rétrograde).";
            PrimaryButton.IsEnabled = false;
            HistoryPanel.Visibility = Visibility.Visible;
            EditPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var amount = displayAmount ?? ResolveDisplayAmount(payment.Id) ?? payment.TotalAmount;
        ErrorText.Text = string.Empty;
        HistoryPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        _selectedPayment = payment;
        _editFeeAmountBaseline = amount;
        EditLastAmountText.Text = $"{amount:N0} {payment.Currency}";
        EditPaymentInfoText.Text =
            $"Reçu {payment.ReceiptNumber} · {payment.PaymentDate:dd/MM/yyyy HH:mm} · {FormatStatus(payment.Status)}";
        // Préremplir avec le montant réel du dernier versement (pas un autre solde / tranche).
        EditAmountBox.Text = amount.ToString("0.##", CultureInfo.CurrentCulture);
        EditNotesBox.Text = payment.Notes ?? string.Empty;
        PrimaryButton.Content = "Enregistrer le nouveau montant";
        PrimaryButton.IsEnabled = true;
        TitleText.Text = "Modifier le montant du dernier versement";
    }

    private void ShowCancelPanel(PaymentDto payment, decimal? displayAmount = null)
    {
        if (!_canMutatePaidPayments)
        {
            ErrorText.Text = "Seul l'administrateur peut supprimer un frais déjà payé.";
            PrimaryButton.IsEnabled = false;
            return;
        }

        if (payment.Status != PaymentStatus.Complet)
        {
            ErrorText.Text = "Seuls les paiements complets peuvent être annulés.";
            PrimaryButton.IsEnabled = false;
            CancelPanel.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Visible;
            return;
        }

        if (!IsLatestCompletedPayment(payment))
        {
            ErrorText.Text =
                "Seul le dernier versement de ce type de frais peut être annulé. " +
                "Annulez d'abord les versements plus récents (ordre rétrograde).";
            PrimaryButton.IsEnabled = false;
            CancelPanel.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Visible;
            return;
        }

        var amount = displayAmount ?? ResolveDisplayAmount(payment.Id) ?? payment.TotalAmount;
        ErrorText.Text = string.Empty;
        HistoryPanel.Visibility = Visibility.Collapsed;
        CancelPanel.Visibility = Visibility.Visible;
        _selectedPayment = payment;
        CancelLastAmountText.Text = $"{amount:N0} {payment.Currency}";
        CancelPaymentInfoText.Text =
            $"Reçu {payment.ReceiptNumber} · {payment.PaymentDate:dd/MM/yyyy HH:mm}\n" +
            "Cette action annule immédiatement ce versement et met à jour les soldes.";
        if (string.IsNullOrWhiteSpace(CancelReasonBox.Text))
        {
            CancelReasonBox.Text = "Annulation du dernier versement";
        }

        PrimaryButton.IsEnabled = true;
        TitleText.Text = "Confirmer l'annulation";
    }

    private decimal? ResolveDisplayAmount(Guid paymentId) =>
        _allPayments.FirstOrDefault(p => p.Dto.Id == paymentId)?.DisplayAmount;

    private async void HistoryReprintBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedPayment is null)
        {
            ErrorText.Text = "Sélectionnez un paiement.";
            return;
        }

        await ShowReceiptPreviewAsync(_selectedPayment);
        PrimaryButton.Content = "Imprimer";
        PrimaryButton.Visibility = Visibility.Visible;
        PrimaryButton.IsEnabled = true;
        HistoryActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void HistoryEditBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedPayment is null)
        {
            ErrorText.Text = "Sélectionnez un paiement.";
            return;
        }

        ShowEditPanel(_selectedPayment);
        PrimaryButton.Content = "Enregistrer le nouveau montant";
        PrimaryButton.Visibility = Visibility.Visible;
        HistoryActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void HistoryCancelBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedPayment is null)
        {
            ErrorText.Text = "Sélectionnez un paiement.";
            return;
        }

        ShowCancelPanel(_selectedPayment);
        if (CancelPanel.Visibility == Visibility.Visible)
        {
            PrimaryButton.Content = "Confirmer l'annulation";
            PrimaryButton.Style = (Style)FindResource("ErpDangerButton");
            PrimaryButton.Visibility = Visibility.Visible;
            HistoryActionsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        ErrorText.Text = string.Empty;
        try
        {
            if (_mode == EncaissementActionMode.CollectPayment
                || (CollectPanel.Visibility == Visibility.Visible && _mode == EncaissementActionMode.CollectPayment))
            {
                if (_mode == EncaissementActionMode.CollectPayment)
                {
                    await SubmitCollectAsync();
                    return;
                }
            }

            if (PrimaryButton.Content is string label)
            {
                if (label.Contains("Encaisser", StringComparison.OrdinalIgnoreCase))
                {
                    await SubmitCollectAsync();
                    return;
                }

                if (label.Contains("Imprimer", StringComparison.OrdinalIgnoreCase))
                {
                    await PrintReceiptAsync();
                    return;
                }

                if (label.Contains("Enregistrer", StringComparison.OrdinalIgnoreCase))
                {
                    await SubmitEditNotesAsync();
                    return;
                }

                if (label.Contains("annulation", StringComparison.OrdinalIgnoreCase))
                {
                    await SubmitCancelAsync();
                    return;
                }
            }

            if (CollectPanel.Visibility == Visibility.Visible)
            {
                await SubmitCollectAsync();
            }
            else if (EditPanel.Visibility == Visibility.Visible)
            {
                await SubmitEditNotesAsync();
            }
            else if (CancelPanel.Visibility == Visibility.Visible)
            {
                await SubmitCancelAsync();
            }
            else if (ReprintPanel.Visibility == Visibility.Visible)
            {
                await PrintReceiptAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private async Task SubmitCollectAsync()
    {
        if (_situation.FeeTypeId is null)
        {
            ErrorText.Text = "Type de frais manquant.";
            return;
        }

        if (_installmentRows.Count == 0)
        {
            ErrorText.Text = "Aucune tranche configurée pour cette classe / catégorie / type de frais";
            return;
        }

        // Re-validate cascade and clamps before submit.
        InstallmentPaymentCascade.RefreshEditability(_installmentRows);
        foreach (var row in _installmentRows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name))
        {
            if (row.TodayPayment < 0 || row.TodayPayment > row.Remaining)
            {
                ErrorText.Text = $"Versement invalide pour la tranche « {row.Name} ».";
                return;
            }
        }

        InstallmentPaymentCascade.ValidateCascadeOrThrow(_installmentRows);

        var lines = _installmentRows
            .Where(r => r.TodayPayment > 0)
            .Select(r => new PaymentLineRequest(
                _situation.FeeTypeId.Value,
                r.TodayPayment,
                _situation.Currency,
                $"{_situation.FeeTypeName} — {r.Name}",
                r.FeeInstallmentId,
                string.IsNullOrWhiteSpace(r.PhysicalNumber) ? null : r.PhysicalNumber.Trim()))
            .ToList();

        if (lines.Count == 0)
        {
            ErrorText.Text = "Saisissez au moins un versement du jour.";
            return;
        }

        SetBusy(true, "Enregistrement du paiement…");
        try
        {
            await _paymentApi.CreateAsync(new CreatePaymentRequest(
                _situation.StudentId,
                _situation.AcademicYearId,
                null,
                _situation.Currency,
                string.IsNullOrWhiteSpace(CollectNotesBox.Text) ? null : CollectNotesBox.Text.Trim(),
                lines));

            NeedsRefresh = true;
            DialogResult = true;
            Close();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SubmitEditNotesAsync()
    {
        if (_selectedPayment is null)
        {
            ErrorText.Text = "Aucun paiement sélectionné.";
            return;
        }

        if (!decimal.TryParse(
                EditAmountBox.Text.Replace(" ", string.Empty),
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out var newAmount)
            && !decimal.TryParse(
                EditAmountBox.Text.Replace(" ", string.Empty).Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out newAmount))
        {
            ErrorText.Text = "Saisissez un montant valide.";
            return;
        }

        if (newAmount <= 0)
        {
            ErrorText.Text = "Le nouveau montant doit être supérieur à zéro.";
            return;
        }

        // Si l'UI édite le montant du type de frais (pas le total multi-frais), recalculer le total reçu.
        var amountForApi = newAmount;
        if (_editFeeAmountBaseline > 0
            && _selectedPayment.TotalAmount != _editFeeAmountBaseline)
        {
            amountForApi = _selectedPayment.TotalAmount - _editFeeAmountBaseline + newAmount;
            if (amountForApi <= 0)
            {
                ErrorText.Text = "Le nouveau montant est invalide pour ce reçu.";
                return;
            }
        }

        SetBusy(true, "Enregistrement du montant…");
        try
        {
            await _paymentApi.UpdateAmountAsync(
                _selectedPayment.Id,
                new UpdatePaymentAmountRequest(
                    amountForApi,
                    string.IsNullOrWhiteSpace(EditNotesBox.Text) ? null : EditNotesBox.Text.Trim()));

            NeedsRefresh = true;
            if (_mode == EncaissementActionMode.EditPayment)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                StatusText.Text = "Montant mis à jour.";
                await LoadPaymentsAsync();
                HideAllPanels();
                HistoryPanel.Visibility = Visibility.Visible;
                HistoryActionsPanel.Visibility = Visibility.Visible;
                PrimaryButton.Visibility = Visibility.Collapsed;
                TitleText.Text = "Historique des paiements";
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SubmitCancelAsync()
    {
        if (_selectedPayment is null)
        {
            ErrorText.Text = "Aucun paiement sélectionné.";
            return;
        }

        var reason = string.IsNullOrWhiteSpace(CancelReasonBox.Text)
            ? "Annulation du dernier versement"
            : CancelReasonBox.Text.Trim();

        SetBusy(true, "Annulation en cours…");
        try
        {
            await _paymentApi.CancelAsync(_selectedPayment.Id, new CancelPaymentRequest(reason));
            NeedsRefresh = true;

            if (_mode == EncaissementActionMode.CancelPayment)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                StatusText.Text = "Paiement annulé.";
                await LoadPaymentsAsync();
                HideAllPanels();
                HistoryPanel.Visibility = Visibility.Visible;
                HistoryActionsPanel.Visibility = Visibility.Visible;
                PrimaryButton.Visibility = Visibility.Collapsed;
                TitleText.Text = "Historique des paiements";
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task PrintReceiptAsync()
    {
        if (_receiptStatement is null)
        {
            ErrorText.Text = "Aucun relevé à imprimer.";
            return;
        }

        try
        {
            SetBusy(true, "Impression…");
            await _statementPrint.PrintAsync(_receiptStatement.PaymentId, _situation.FeeTypeId);
            StatusText.Text = "Relevé envoyé à l'imprimante.";
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ExportPdfButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_receiptStatement is null)
        {
            ErrorText.Text = "Aucun relevé à exporter.";
            return;
        }

        try
        {
            SetBusy(true, "Export PDF…");
            await _statementPrint.ExportPdfAsync(_receiptStatement.PaymentId, _situation.FeeTypeId);
            StatusText.Text = "PDF du relevé enregistré.";
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static string FormatStatementPreview(FeeTypeStatementDto s)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var currency = s.Currency.ToString();
        var history = s.PaymentHistory.Count == 0
            ? "  (aucun paiement)"
            : string.Join("\n", s.PaymentHistory.Select(l =>
                $"  {l.Number:00} | {l.InstallmentName} | {l.PaymentDate.ToLocalTime():dd/MM/yyyy} | {l.AmountPaid.ToString("N2", fr)} | {l.ReceiptNumber}"));

        var situations = s.InstallmentSituations.Count == 0
            ? "  (aucune tranche)"
            : string.Join("\n", s.InstallmentSituations.Select(l =>
                $"  {l.Number:00} | {l.InstallmentName} | prévu {l.AmountExpected.ToString("N2", fr)} | payé {l.AmountPaid.ToString("N2", fr)} | solde {l.Remaining.ToString("N2", fr)}"));

        return
            $"RELEVÉ DE {s.FeeTypeName.Trim().ToUpperInvariant()} n°{s.StatementNumber}\n" +
            $"────────────────────────────\n" +
            $"{s.SchoolName}\n" +
            $"Édité : {s.EditedAt:dd/MM/yyyy HH:mm}\n" +
            $"Reçu paiement : {s.ReceiptNumber}\n" +
            $"Nom complet : {s.StudentName}\n" +
            $"Matricule : {s.StudentRegistrationNumber ?? "—"}\n" +
            $"Classe : {s.ClassName}\n" +
            $"Année scolaire : {s.AcademicYearLabel}\n" +
            (string.IsNullOrWhiteSpace(s.CashierName) ? "" : $"Caissier : {s.CashierName}\n") +
            $"\nHISTORIQUE DES PAIEMENTS\n{history}\n\n" +
            $"SITUATION GLOBALE\n{situations}\n\n" +
            $"RÉCAPITULATIF\n" +
            $"  Total prévu   : {s.TotalExpected.ToString("N2", fr)} {currency}\n" +
            $"  Déjà payé     : {s.TotalPaid.ToString("N2", fr)} {currency}\n" +
            $"  Reste à payer : {s.TotalRemaining.ToString("N2", fr)} {currency}";
    }

    private static string FormatStatus(PaymentStatus status) => status switch
    {
        PaymentStatus.EnAttente => "En attente",
        PaymentStatus.Complet => "Complet",
        PaymentStatus.Annule => "Annulé",
        PaymentStatus.Rembourse => "Remboursé",
        _ => status.ToString()
    };

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_mode == EncaissementActionMode.PaymentHistory
            && (ReprintPanel.Visibility == Visibility.Visible
                || EditPanel.Visibility == Visibility.Visible
                || CancelPanel.Visibility == Visibility.Visible))
        {
            HideAllPanels();
            HistoryPanel.Visibility = Visibility.Visible;
            HistoryActionsPanel.Visibility = Visibility.Visible;
            PrimaryButton.Visibility = Visibility.Collapsed;
            ExportPdfButton.Visibility = Visibility.Collapsed;
            TitleText.Text = "Historique des paiements";
            ErrorText.Text = string.Empty;
            return;
        }

        if ((_mode is EncaissementActionMode.ReprintReceipt or EncaissementActionMode.EditPayment or EncaissementActionMode.CancelPayment)
            && (ReprintPanel.Visibility == Visibility.Visible
                || EditPanel.Visibility == Visibility.Visible
                || CancelPanel.Visibility == Visibility.Visible)
            && HistoryPanel.Visibility == Visibility.Collapsed)
        {
            HideAllPanels();
            HistoryPanel.Visibility = Visibility.Visible;
            PrimaryButton.IsEnabled = false;
            if (_mode == EncaissementActionMode.ReprintReceipt)
            {
                PrimaryButton.Content = "Imprimer";
                TitleText.Text = "Aperçu du reçu";
            }
            else if (_mode == EncaissementActionMode.EditPayment)
            {
                PrimaryButton.Content = "Enregistrer";
                TitleText.Text = "Modifier les notes";
            }
            else
            {
                PrimaryButton.Content = "Confirmer l'annulation";
                TitleText.Text = "Annuler un paiement";
            }

            ErrorText.Text = string.Empty;
            return;
        }

        DialogResult = NeedsRefresh ? true : false;
        Close();
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
        if (message is not null)
        {
            StatusText.Text = message;
        }
        else if (!busy)
        {
            StatusText.Text = string.Empty;
        }
    }

    private sealed class PaymentListItem
    {
        public PaymentListItem(PaymentDto dto, decimal? displayAmount = null)
        {
            Dto = dto;
            ReceiptNumber = dto.ReceiptNumber;
            PaymentDate = dto.PaymentDate;
            DisplayAmount = displayAmount ?? dto.TotalAmount;
            TotalAmount = DisplayAmount;
            Currency = dto.Currency.ToString();
            Status = dto.Status;
            StatusLabel = FormatStatus(dto.Status);
        }

        public PaymentDto Dto { get; }
        public string ReceiptNumber { get; }
        public DateTime PaymentDate { get; }
        /// <summary>Montant affiché (somme des lignes du type de frais courant, sinon total reçu).</summary>
        public decimal DisplayAmount { get; }
        public decimal TotalAmount { get; }
        public string Currency { get; }
        public PaymentStatus Status { get; }
        public string StatusLabel { get; }
    }
}
