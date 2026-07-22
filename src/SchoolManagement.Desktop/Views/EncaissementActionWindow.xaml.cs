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
    private readonly List<PaymentLineListItem> _feePaymentLines = [];
    private readonly ObservableCollection<InstallmentCollectRow> _installmentRows = [];
    private readonly ObservableCollection<PaymentDetailEditRow> _paymentEditRows = [];
    private PaymentDto? _selectedPayment;
    private decimal _editFeeAmountBaseline;
    private PaymentMutationGateDto? _mutationGate;
    private FeeTypeStatementDto? _receiptStatement;
    private bool _busy;
    private bool _suppressDistribute;
    private bool _suppressTodayPaymentSync;
    private int _collectWithholdingPreviewVersion;

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
                Width = 980;
                Height = 760;
                MinWidth = 820;
                MinHeight = 600;
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
                Width = 980;
                Height = 760;
                MinWidth = 820;
                MinHeight = 600;
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
        _ = RefreshCollectWithholdingsAsync();
    }

    /// <summary>
    /// Aperçu des retenues qui seront appliquées à l'enregistrement
    /// (même logique que le serveur : par tranche / catégorie / config active).
    /// </summary>
    private async Task RefreshCollectWithholdingsAsync()
    {
        if (_mode != EncaissementActionMode.CollectPayment || _situation.FeeTypeId is null)
        {
            CollectWithholdingsBox.Visibility = Visibility.Collapsed;
            return;
        }

        var version = Interlocked.Increment(ref _collectWithholdingPreviewVersion);
        var linesToPay = _installmentRows.Where(r => r.TodayPayment > 0).ToList();
        if (linesToPay.Count == 0)
        {
            if (version == _collectWithholdingPreviewVersion)
            {
                CollectWithholdingsBox.Visibility = Visibility.Collapsed;
                CollectWithholdingsGrid.ItemsSource = null;
            }

            return;
        }

        try
        {
            var aggregated = new Dictionary<Guid, (string Code, string Name, decimal Amount)>();
            decimal gross = 0;
            decimal totalWithheld = 0;

            foreach (var row in linesToPay)
            {
                var result = await _withholdingApi.CalculateAsync(new WithholdingCalculateRequest(
                    row.TodayPayment,
                    new WithholdingResolveContext(
                        _situation.AcademicYearId,
                        _situation.FeeTypeId.Value,
                        row.FeeInstallmentId,
                        _situation.FeePricingCategoryId,
                        _situation.StudentId)));

                if (version != _collectWithholdingPreviewVersion)
                {
                    return;
                }

                gross += result.GrossAmount;
                totalWithheld += result.TotalWithheld;
                foreach (var line in result.Lines.Where(l => l.WithheldAmount > 0))
                {
                    if (aggregated.TryGetValue(line.WithholdingTypeId, out var existing))
                    {
                        aggregated[line.WithholdingTypeId] =
                            (existing.Code, existing.Name, existing.Amount + line.WithheldAmount);
                    }
                    else
                    {
                        aggregated[line.WithholdingTypeId] =
                            (line.WithholdingTypeCode, line.WithholdingTypeName, line.WithheldAmount);
                    }
                }
            }

            if (version != _collectWithholdingPreviewVersion)
            {
                return;
            }

            if (totalWithheld <= 0 || aggregated.Count == 0)
            {
                CollectWithholdingsBox.Visibility = Visibility.Collapsed;
                CollectWithholdingsGrid.ItemsSource = null;
                return;
            }

            var net = gross - totalWithheld;
            CollectWithholdingsText.Text =
                $"Sur {gross:N0} {_situation.Currency} encaissés : " +
                $"retenues {totalWithheld:N0} · net réparti {net:N0}. " +
                "Montant fixe : une fois par rubrique. Pourcentage : à chaque versement jusqu'au solde de la rubrique.";
            CollectWithholdingsGrid.ItemsSource = aggregated
                .OrderBy(kv => kv.Value.Name)
                .Select(kv => new
                {
                    WithholdingTypeCode = kv.Value.Code,
                    WithholdingTypeName = kv.Value.Name,
                    WithheldAmount = kv.Value.Amount
                })
                .ToList();
            CollectWithholdingsBox.Visibility = Visibility.Visible;
        }
        catch
        {
            if (version == _collectWithholdingPreviewVersion)
            {
                CollectWithholdingsBox.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async Task LoadPaymentsAsync()
    {
        var result = await _paymentApi.SearchAsync(new PaymentSearchRequest(_situation.StudentId, null, null, 1, 200));
        _allPayments.Clear();
        _feePaymentLines.Clear();
        _mutationGate = null;

        var feeTypeId = _situation.FeeTypeId;
        var filterByFeeType = feeTypeId.HasValue
            && _mode is EncaissementActionMode.EditPayment or EncaissementActionMode.CancelPayment;

        if (filterByFeeType)
        {
            try
            {
                _mutationGate = await _paymentApi.GetMutationGateAsync(
                    _situation.AcademicYearId,
                    feeTypeId!.Value);
            }
            catch
            {
                _mutationGate = null;
            }
        }

        foreach (var p in result.Items
                     .OrderByDescending(x => x.CreatedAt ?? x.PaymentDate)
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
                if (detail.AcademicYearId != _situation.AcademicYearId)
                {
                    continue;
                }

                if (detail.Status != PaymentStatus.Complet)
                {
                    continue;
                }

                var feeLines = detail.Lines
                    .Where(l => l.FeeTypeId == feeTypeId!.Value && l.Amount > 0)
                    .OrderByDescending(l => l.Id)
                    .ToList();
                if (feeLines.Count == 0)
                {
                    continue;
                }

                var paymentCreatedAt = detail.CreatedAt ?? p.CreatedAt ?? p.PaymentDate;
                var physical = string.Join(
                    ", ",
                    feeLines
                        .Select(l => l.PhysicalReceiptNumber?.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Cast<string>()
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                _allPayments.Add(new PaymentListItem(
                    p,
                    feeLines.Sum(l => l.Amount),
                    physical,
                    paymentCreatedAt));

                foreach (var line in feeLines)
                {
                    _feePaymentLines.Add(new PaymentLineListItem(
                        p,
                        line.Id,
                        p.ReceiptNumber,
                        p.PaymentDate,
                        paymentCreatedAt,
                        line.Amount,
                        line.PhysicalReceiptNumber,
                        detail.Status,
                        detail.Currency));
                }
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
                CancelPaymentsGrid.ItemsSource = null;
                PrimaryButton.IsEnabled = false;
                StatusText.Text = "Aucun versement complet à annuler.";
                LoadCancelPaymentHistory();
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
                EditPaymentsGrid.ItemsSource = null;
                PrimaryButton.IsEnabled = false;
                StatusText.Text = "Aucun versement complet à modifier.";
            }
            else
            {
                await ShowEditPanelAsync(latestCompleted.Dto, latestCompleted.DisplayAmount);
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

    private PaymentListItem? GetLatestCompletedPayment()
    {
        // Verrou global : seul le dernier encaissement du type de frais (tous élèves) est mutable.
        if (_mutationGate?.LatestPaymentId is Guid gateId)
        {
            return _allPayments.FirstOrDefault(p =>
                p.Dto.Id == gateId && p.Status == PaymentStatus.Complet);
        }

        return _allPayments
            .Where(p => p.Status == PaymentStatus.Complet)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Dto.Id)
            .FirstOrDefault();
    }

    private bool IsLatestCompletedPayment(PaymentDto payment)
    {
        if (_mutationGate?.LatestPaymentId is Guid gateId)
        {
            return payment.Id == gateId;
        }

        var latest = GetLatestCompletedPayment();
        return latest is not null && latest.Dto.Id == payment.Id;
    }

    private bool IsSchoolWideMutablePayment(Guid paymentId) =>
        _canMutatePaidPayments
        && _mutationGate?.LatestPaymentId is Guid gateId
        && gateId == paymentId;

    private string RetrogradeBlockedMessage(bool forCancel)
    {
        var action = forCancel ? "annuler" : "modifier";
        if (_mutationGate?.LatestPaymentDate is DateTime gateDate)
        {
            var who = string.IsNullOrWhiteSpace(_mutationGate.LatestStudentName)
                ? "un autre élève"
                : _mutationGate.LatestStudentName;
            var receipt = string.IsNullOrWhiteSpace(_mutationGate.LatestReceiptNumber)
                ? string.Empty
                : $" ({_mutationGate.LatestReceiptNumber})";
            return
                $"Impossible de {action} : un encaissement plus récent existe déjà pour ce type de frais " +
                $"le {gateDate:dd/MM/yyyy} — {who}{receipt}. " +
                "Traitez d'abord ce versement (ordre rétrograde, tous élèves confondus).";
        }

        return forCancel
            ? "Impossible d'annuler : un encaissement à une date plus récente existe déjà pour ce type de frais. " +
              "Annulez d'abord le versement le plus récent."
            : "Impossible de modifier : un encaissement à une date plus récente existe déjà pour ce type de frais. " +
              "Modifiez ou annulez d'abord le versement le plus récent.";
    }

    private IReadOnlyList<PaymentLineListItem> GetOrderedFeePaymentLines() =>
        _feePaymentLines
            .OrderByDescending(l => l.PaymentDate)
            .ThenByDescending(l => l.PaymentCreatedAt)
            .ThenByDescending(l => l.LineId)
            .ToList();

    private void PopulateMutationHistoryRows()
    {
        _paymentEditRows.Clear();
        var ordered = GetOrderedFeePaymentLines();
        var number = 1;

        foreach (var item in ordered)
        {
            var canAct = IsSchoolWideMutablePayment(item.PaymentId)
                && item.Status == PaymentStatus.Complet;

            _paymentEditRows.Add(new PaymentDetailEditRow(
                item.PaymentId,
                item.LineId,
                number++,
                item.ReceiptNumber,
                item.PaymentDate,
                item.PaymentCreatedAt,
                item.Amount,
                item.Currency,
                item.Status,
                item.PhysicalReceiptNumber,
                canAct));
        }
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
            Page: 1,
            PageSize: 100));
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

        // Aperçu informatif : retenues restantes à appliquer (une fois par rubrique).
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
                _situation.FeePricingCategoryId,
                _situation.StudentId)));

        WithholdingTotalsText.Text =
            $"Aperçu sur {result.GrossAmount:N0} {_situation.Currency}  ·  " +
            $"Retenues restantes : {result.TotalWithheld:N0}  ·  " +
            $"Net : {result.NetAmount:N0}" +
            Environment.NewLine +
            "Chaque retenue fixe s'applique une seule fois par rubrique. " +
            "Le pourcentage s'applique à chaque versement jusqu'au solde de la rubrique.";
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
            await ShowEditPanelAsync(item.Dto, amount);
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

    private async Task ShowEditPanelAsync(PaymentDto payment, decimal? displayAmount = null)
    {
        if (!_canMutatePaidPayments)
        {
            ErrorText.Text = "Seul l'administrateur peut modifier un frais déjà payé.";
            PrimaryButton.IsEnabled = false;
        }

        ErrorText.Text = string.Empty;
        HistoryPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        TitleText.Text = "Modifier le paiement";
        PrimaryButton.Content = "Enregistrer les modifications";

        LoadEditPaymentHistory(payment, displayAmount);
        await Task.CompletedTask;
    }

    private void LoadEditPaymentHistory(PaymentDto? preferredPayment = null, decimal? preferredDisplayAmount = null)
    {
        EditPaymentsGrid.ItemsSource = null;
        EditPaymentsTotalText.Text = "0";
        PopulateMutationHistoryRows();
        EditPaymentsGrid.ItemsSource = _paymentEditRows.ToList();
        UpdateEditPaymentTotals();

        var editable = _paymentEditRows.Where(r => r.CanEdit).ToList();
        if (editable.Count > 0)
        {
            var source = _allPayments.First(p => p.Dto.Id == editable[0].PaymentId);
            _selectedPayment = source.Dto;
            _editFeeAmountBaseline = preferredPayment?.Id == editable[0].PaymentId
                && preferredDisplayAmount.HasValue
                    ? preferredDisplayAmount.Value
                    : source.DisplayAmount;
            PrimaryButton.IsEnabled = true;
            ErrorText.Text = string.Empty;
            return;
        }

        _selectedPayment = preferredPayment ?? GetOrderedFeePaymentLines().FirstOrDefault()?.Dto;
        _editFeeAmountBaseline = preferredDisplayAmount ?? 0;
        PrimaryButton.IsEnabled = false;

        if (!_canMutatePaidPayments)
        {
            ErrorText.Text = "Seul l'administrateur peut modifier un frais déjà payé.";
        }
        else if (_paymentEditRows.Count > 0)
        {
            ErrorText.Text = RetrogradeBlockedMessage(forCancel: false);
        }
        else
        {
            ErrorText.Text = "Aucun détail de versement pour ce type de frais / année scolaire.";
        }
    }

    private void UpdateEditPaymentTotals()
    {
        var currency = _situation.Currency.ToString();
        EditPaymentsTotalText.Text = $"{_paymentEditRows.Sum(r => r.Amount):N0} {currency}";
    }

    private void EditPaymentAmountBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: PaymentDetailEditRow row } || !row.CanEdit)
        {
            return;
        }

        if (!InstallmentPaymentCascade.TryParseDecimal(row.AmountText, out var amount) || amount < 0)
        {
            amount = 0;
        }

        row.SetAmount(amount, suppressNotify: true);
        UpdateEditPaymentTotals();
    }

    private void EditPaymentAmountBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: PaymentDetailEditRow row } || !row.CanEdit)
        {
            return;
        }

        if (!InstallmentPaymentCascade.TryParseDecimal(row.AmountText, out var amount) || amount < 0)
        {
            amount = 0;
        }

        row.SetAmount(amount, suppressNotify: false);
        UpdateEditPaymentTotals();
    }

    private void ShowEditPanel(PaymentDto payment, decimal? displayAmount = null) =>
        _ = ShowEditPanelAsync(payment, displayAmount);

    private void ShowCancelPanel(PaymentDto payment, decimal? displayAmount = null)
    {
        if (!_canMutatePaidPayments)
        {
            ErrorText.Text = "Seul l'administrateur peut supprimer un frais déjà payé.";
            PrimaryButton.IsEnabled = false;
        }

        ErrorText.Text = string.Empty;
        HistoryPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Collapsed;
        CancelPanel.Visibility = Visibility.Visible;
        TitleText.Text = "Annuler le paiement";
        PrimaryButton.Content = "Confirmer l'annulation";
        PrimaryButton.Style = (Style)FindResource("ErpDangerButton");

        if (string.IsNullOrWhiteSpace(CancelReasonBox.Text))
        {
            CancelReasonBox.Text = "Annulation du dernier versement";
        }

        LoadCancelPaymentHistory(payment, displayAmount);
    }

    private void LoadCancelPaymentHistory(PaymentDto? preferredPayment = null, decimal? preferredDisplayAmount = null)
    {
        CancelPaymentsGrid.ItemsSource = null;
        CancelPaymentsTotalText.Text = "0";
        PopulateMutationHistoryRows();
        CancelPaymentsGrid.ItemsSource = _paymentEditRows.ToList();

        var currency = _situation.Currency.ToString();
        CancelPaymentsTotalText.Text = $"{_paymentEditRows.Sum(r => r.Amount):N0} {currency}";

        var targetRows = _paymentEditRows.Where(r => r.CanEdit).ToList();
        if (targetRows.Count > 0)
        {
            var source = _allPayments.First(p => p.Dto.Id == targetRows[0].PaymentId);
            _selectedPayment = source.Dto;
            CancelPaymentsGrid.SelectedItem = targetRows[0];
            PrimaryButton.IsEnabled = true;
            ErrorText.Text = string.Empty;
            _ = preferredDisplayAmount;
            return;
        }

        _selectedPayment = preferredPayment ?? GetOrderedFeePaymentLines().FirstOrDefault()?.Dto;
        PrimaryButton.IsEnabled = false;

        if (!_canMutatePaidPayments)
        {
            ErrorText.Text = "Seul l'administrateur peut supprimer un frais déjà payé.";
        }
        else if (_paymentEditRows.Count > 0)
        {
            ErrorText.Text = RetrogradeBlockedMessage(forCancel: true);
        }
        else
        {
            ErrorText.Text = "Aucun détail de versement pour ce type de frais / année scolaire.";
        }

        _ = preferredDisplayAmount;
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

    private async void HistoryEditBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedPayment is null)
        {
            ErrorText.Text = "Sélectionnez un paiement.";
            return;
        }

        await ShowEditPanelAsync(_selectedPayment);
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
        var editableRows = _paymentEditRows.Where(r => r.CanEdit).ToList();
        if (editableRows.Count == 0)
        {
            ErrorText.Text = "Aucun paiement modifiable.";
            return;
        }

        var paymentId = editableRows[0].PaymentId;
        var source = _allPayments.FirstOrDefault(p => p.Dto.Id == paymentId);
        if (source is null)
        {
            ErrorText.Text = "Paiement introuvable.";
            return;
        }

        _selectedPayment = source.Dto;

        if (!IsLatestCompletedPayment(_selectedPayment))
        {
            ErrorText.Text = RetrogradeBlockedMessage(forCancel: false);
            return;
        }

        foreach (var row in editableRows)
        {
            if (!InstallmentPaymentCascade.TryParseDecimal(row.AmountText, out var parsed) || parsed < 0)
            {
                parsed = 0;
            }

            row.SetAmount(parsed, suppressNotify: false);
        }

        var newFeeAmount = editableRows.Sum(r => r.Amount);
        if (newFeeAmount <= 0)
        {
            ErrorText.Text = "Le nouveau montant doit être supérieur à zéro.";
            return;
        }

        var amountForApi = newFeeAmount;
        if (_editFeeAmountBaseline > 0
            && _selectedPayment.TotalAmount != _editFeeAmountBaseline)
        {
            amountForApi = _selectedPayment.TotalAmount - _editFeeAmountBaseline + newFeeAmount;
            if (amountForApi <= 0)
            {
                ErrorText.Text = "Le nouveau montant est invalide pour ce reçu.";
                return;
            }
        }

        var lineUpdates = editableRows
            .Select(r => new UpdatePaymentLineAmountRequest(
                r.LineId,
                r.Amount,
                r.PhysicalNumber.Trim()))
            .ToList();

        SetBusy(true, "Enregistrement des modifications…");
        try
        {
            await _paymentApi.UpdateAmountAsync(
                _selectedPayment.Id,
                new UpdatePaymentAmountRequest(amountForApi, null, null, lineUpdates));

            NeedsRefresh = true;
            if (_mode == EncaissementActionMode.EditPayment)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                StatusText.Text = "Paiement mis à jour.";
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
        var target = _paymentEditRows.FirstOrDefault(r => r.CanEdit);
        if (target is not null)
        {
            var source = _allPayments.FirstOrDefault(p => p.Dto.Id == target.PaymentId);
            if (source is not null)
            {
                _selectedPayment = source.Dto;
            }
        }

        if (_selectedPayment is null)
        {
            ErrorText.Text = "Aucun paiement sélectionné.";
            return;
        }

        if (!_canMutatePaidPayments)
        {
            ErrorText.Text = "Seul l'administrateur peut supprimer un frais déjà payé.";
            return;
        }

        if (_selectedPayment.Status != PaymentStatus.Complet)
        {
            ErrorText.Text = "Seuls les paiements complets peuvent être annulés.";
            return;
        }

        if (!IsLatestCompletedPayment(_selectedPayment))
        {
            ErrorText.Text = RetrogradeBlockedMessage(forCancel: true);
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

    private sealed class PaymentLineListItem
    {
        public PaymentLineListItem(
            PaymentDto dto,
            Guid lineId,
            string receiptNumber,
            DateTime paymentDate,
            DateTime paymentCreatedAt,
            decimal amount,
            string? physicalReceiptNumber,
            PaymentStatus status,
            Currency currency)
        {
            Dto = dto;
            PaymentId = dto.Id;
            LineId = lineId;
            ReceiptNumber = receiptNumber;
            PaymentDate = paymentDate;
            PaymentCreatedAt = paymentCreatedAt;
            Amount = amount;
            PhysicalReceiptNumber = physicalReceiptNumber;
            Status = status;
            Currency = currency;
        }

        public PaymentDto Dto { get; }
        public Guid PaymentId { get; }
        public Guid LineId { get; }
        public string ReceiptNumber { get; }
        public DateTime PaymentDate { get; }
        public DateTime PaymentCreatedAt { get; }
        public decimal Amount { get; }
        public string? PhysicalReceiptNumber { get; }
        public PaymentStatus Status { get; }
        public Currency Currency { get; }
    }

    private sealed class PaymentListItem
    {
        public PaymentListItem(
            PaymentDto dto,
            decimal? displayAmount = null,
            string? physicalReceiptNumber = null,
            DateTime? createdAt = null)
        {
            Dto = dto;
            ReceiptNumber = dto.ReceiptNumber;
            PaymentDate = dto.PaymentDate;
            CreatedAt = createdAt ?? dto.CreatedAt ?? dto.PaymentDate;
            DisplayAmount = displayAmount ?? dto.TotalAmount;
            TotalAmount = DisplayAmount;
            Currency = dto.Currency.ToString();
            Status = dto.Status;
            StatusLabel = FormatStatus(dto.Status);
            PhysicalReceiptNumber = physicalReceiptNumber;
        }

        public PaymentDto Dto { get; }
        public string ReceiptNumber { get; }
        public DateTime PaymentDate { get; }
        public DateTime CreatedAt { get; }
        /// <summary>Montant affiché (somme des lignes du type de frais courant, sinon total reçu).</summary>
        public decimal DisplayAmount { get; }
        public decimal TotalAmount { get; }
        public string Currency { get; }
        public PaymentStatus Status { get; }
        public string StatusLabel { get; }
        public string? PhysicalReceiptNumber { get; }
    }
}
