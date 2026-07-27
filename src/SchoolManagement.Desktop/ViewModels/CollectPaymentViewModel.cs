using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.CurrencyManagement.DTOs;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Desktop.Helpers;
using SchoolManagement.Desktop.Models;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Desktop.ViewModels;

public partial class CollectPaymentViewModel : ObservableObject
{
    private readonly StudentPaymentSituationDto _situation;
    private readonly IPaymentApiService _paymentApi;
    private readonly IFinanceApiService _financeApi;
    private readonly ICurrencyApiService _currencyApi;
    private readonly IAuthSessionService _authSession;
    private readonly IStudentDossierPathResolver _dossierPathResolver;
    private readonly IFeeTypeStatementPrintService _statementPrint;
    private bool _suppressDistribute;
    private bool _suppressTodayPaymentSync;
    private Guid? _feeCurrencyId;

    public CollectPaymentViewModel(
        StudentPaymentSituationDto situation,
        IPaymentApiService paymentApi,
        IFinanceApiService financeApi,
        ICurrencyApiService currencyApi,
        IAuthSessionService authSession,
        IStudentDossierPathResolver dossierPathResolver,
        IFeeTypeStatementPrintService statementPrint)
    {
        _situation = situation;
        _paymentApi = paymentApi;
        _financeApi = financeApi;
        _currencyApi = currencyApi;
        _authSession = authSession;
        _dossierPathResolver = dossierPathResolver;
        _statementPrint = statementPrint;

        FullName = situation.FullName;
        AcademicYearLabel = situation.AcademicYearLabel;
        CurrencyLabel = situation.Currency.ToString();
        FeeCurrencyLabel = situation.Currency.ToString();
        PaymentCurrencyLabel = situation.Currency.ToString();
        ClassName = situation.ClassName;
        FeePricingCategoryName = situation.FeePricingCategoryName;
        RegistrationNumber = situation.RegistrationNumber;
        FeeTypeName = situation.FeeTypeName;
        AmountExpected = situation.AmountExpected;
        AmountPaid = situation.AmountPaid;
        GlobalBalance = Math.Max(0, situation.AmountExpected - situation.AmountPaid);

        var user = authSession.CurrentUser;
        RecordedByName = user?.FullName ?? "—";
        RecordedByRole = user?.Roles.FirstOrDefault() ?? string.Empty;
        CanOverrideExchangeRate = authSession.IsAdministrator
            || (user?.Permissions.Any(p => string.Equals(p, Permissions.PaymentFxOverride, StringComparison.OrdinalIgnoreCase)) == true);

        PhotoSource = ResolvePhoto(situation.PhotoPath);
        HasPhoto = PhotoSource is not null;
    }

    public ObservableCollection<InstallmentCollectRow> InstallmentRows { get; } = [];
    public ObservableCollection<SchoolCurrencyDto> PaymentCurrencies { get; } = [];

    public string FullName { get; }
    public string AcademicYearLabel { get; }
    public string CurrencyLabel { get; }
    public string FeeCurrencyLabel { get; }
    public string ClassName { get; }
    public string FeePricingCategoryName { get; }
    public string RegistrationNumber { get; }
    public string FeeTypeName { get; }
    public decimal AmountExpected { get; }
    public decimal AmountPaid { get; }
    public decimal GlobalBalance { get; }
    public string RecordedByName { get; }
    public string RecordedByRole { get; }
    public bool CanOverrideExchangeRate { get; }
    public bool IsRateReadOnly => !CanOverrideExchangeRate;
    public BitmapImage? PhotoSource { get; }
    public bool HasPhoto { get; }

    public string StudentDisplayLabel => $"{FullName} ({ClassName})";

    [ObservableProperty] private DateTime? _paymentDate = DateTime.Today;
    [ObservableProperty] private string _amountToDistributeText = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canSubmit = true;
    [ObservableProperty] private bool _hasNoInstallments;
    [ObservableProperty] private string _totalTodayDisplay = "0";
    [ObservableProperty] private string _remainingAfterDisplay = "0";
    [ObservableProperty] private SchoolCurrencyDto? _selectedPaymentCurrency;
    [ObservableProperty] private string _paymentCurrencyLabel = string.Empty;
    [ObservableProperty] private string _appliedRateText = "1";
    [ObservableProperty] private string _convertedAmountText = "0";
    [ObservableProperty] private string _exchangeRateHint = string.Empty;
    [ObservableProperty] private bool _showFxPanel;

    private decimal _databaseRate = 1m;
    private bool _suppressRateReload;
    private Guid? _activeExchangeRateId;

    public event Action? RequestCloseSuccess;
    public event Action? RequestCloseCancel;

    partial void OnPaymentDateChanged(DateTime? value) => RecalculateCanSubmit();
    partial void OnIsBusyChanged(bool value) => RecalculateCanSubmit();
    partial void OnSelectedPaymentCurrencyChanged(SchoolCurrencyDto? value)
    {
        PaymentCurrencyLabel = value?.CurrencyCode ?? FeeCurrencyLabel;
        _ = RefreshFxAndRedistributeAsync(reloadRateFromDatabase: true);
    }
    partial void OnAppliedRateTextChanged(string value)
    {
        if (_suppressRateReload) return;
        _ = RefreshFxAndRedistributeAsync(reloadRateFromDatabase: false);
    }

    public async Task InitializeAsync()
    {
        if (_situation.FeeTypeId is null)
        {
            ErrorMessage = "Aucun type de frais associé à cette situation.";
            CanSubmit = false;
            return;
        }

        IsBusy = true;
        StatusMessage = "Chargement du plan de tranches…";
        try
        {
            await LoadCurrenciesAsync();

            var plan = await _financeApi.GetInstallmentPaymentPlanAsync(
                _situation.EnrollmentId,
                _situation.FeeTypeId.Value);

            InstallmentRows.Clear();
            foreach (var line in plan.Lines.OrderBy(l => l.SortOrder).ThenBy(l => l.InstallmentName))
            {
                InstallmentRows.Add(new InstallmentCollectRow(
                    line.FeeInstallmentId,
                    line.InstallmentName,
                    line.SortOrder,
                    line.AmountExpected,
                    line.AmountPaid,
                    line.Remaining));
            }

            if (InstallmentRows.Count == 0)
            {
                HasNoInstallments = true;
                CanSubmit = false;
                UpdateSummaryDisplays();
                RecalculateCanSubmit();
                return;
            }

            HasNoInstallments = false;
            _suppressDistribute = true;
            AmountToDistributeText = string.Empty;
            _suppressDistribute = false;

            _suppressTodayPaymentSync = true;
            try
            {
                InstallmentPaymentCascade.Redistribute(InstallmentRows, 0);
            }
            finally
            {
                _suppressTodayPaymentSync = false;
            }

            InstallmentPaymentCascade.RefreshEditability(InstallmentRows);
            UpdateSummaryDisplays();
            RecalculateCanSubmit();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CanSubmit = false;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }

    partial void OnAmountToDistributeTextChanged(string value)
    {
        if (_suppressDistribute)
        {
            return;
        }

        _ = RefreshFxAndRedistributeAsync(reloadRateFromDatabase: false);
    }

    public void OnTodayPaymentTextChanged(InstallmentCollectRow row)
    {
        if (_suppressTodayPaymentSync || _suppressDistribute)
        {
            return;
        }

        InstallmentPaymentCascade.ApplyTodayPaymentEdit(InstallmentRows, row, commitClamp: false);
        SyncTotalFromRows();
        UpdateSummaryDisplays();
        RecalculateCanSubmit();
    }

    public void OnTodayPaymentLostFocus(InstallmentCollectRow row)
    {
        InstallmentPaymentCascade.ApplyTodayPaymentEdit(InstallmentRows, row, commitClamp: true);
        SyncTotalFromRows();
        UpdateSummaryDisplays();
        RecalculateCanSubmit();
    }

    private void SyncTotalFromRows()
    {
        var feeSum = InstallmentRows.Sum(r => r.TodayPayment);
        var rate = _databaseRate > 0 ? _databaseRate : 1m;
        if (decimal.TryParse(AppliedRateText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var displayedRate)
            && displayedRate > 0)
        {
            rate = displayedRate;
        }

        var paymentSum = Math.Round(feeSum * rate, 2, MidpointRounding.AwayFromZero);
        _suppressDistribute = true;
        try
        {
            AmountToDistributeText = paymentSum.ToString("0.##", CultureInfo.InvariantCulture);
            ConvertedAmountText = feeSum.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));
        }
        finally
        {
            _suppressDistribute = false;
        }
    }

    private void UpdateSummaryDisplays()
    {
        var totalToday = InstallmentRows.Sum(r => r.TodayPayment);
        var remainingAfter = Math.Max(0, GlobalBalance - totalToday);
        TotalTodayDisplay = $"{totalToday:N0} {CurrencyLabel}";
        RemainingAfterDisplay = $"{remainingAfter:N0} {CurrencyLabel}";
    }

    private void RecalculateCanSubmit()
    {
        if (IsBusy || HasNoInstallments || _situation.FeeTypeId is null || PaymentDate is null || InstallmentRows.Count == 0)
        {
            CanSubmit = false;
            return;
        }

        var hasPositiveLine = false;
        foreach (var row in InstallmentRows)
        {
            if (row.TodayPayment < 0 || row.TodayPayment > row.Remaining)
            {
                CanSubmit = false;
                return;
            }

            if (row.TodayPayment > 0)
            {
                hasPositiveLine = true;
            }
        }

        if (!hasPositiveLine)
        {
            CanSubmit = false;
            return;
        }

        try
        {
            InstallmentPaymentCascade.ValidateCascadeOrThrow(InstallmentRows);
            CanSubmit = true;
        }
        catch
        {
            CanSubmit = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        RecalculateCanSubmit();
        if (!CanSubmit)
        {
            if (PaymentDate is null)
            {
                ErrorMessage = "La date du paiement est obligatoire.";
            }
            return;
        }

        if (_situation.FeeTypeId is null)
        {
            ErrorMessage = "Type de frais manquant.";
            return;
        }

        if (InstallmentRows.Count == 0)
        {
            ErrorMessage = "Aucune tranche configurée pour cette classe / catégorie / type de frais";
            return;
        }

        InstallmentPaymentCascade.RefreshEditability(InstallmentRows);
        var totalFee = InstallmentRows.Sum(r => r.TodayPayment);
        var maxFee = InstallmentRows.Sum(r => r.Remaining);
        if (totalFee > maxFee + 0.0000001m || totalFee > GlobalBalance + 0.0000001m)
        {
            ErrorMessage = $"Le montant saisi dépasse le reste à payer ({Math.Min(maxFee, GlobalBalance):N2} {FeeCurrencyLabel}).";
            return;
        }

        foreach (var row in InstallmentRows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name))
        {
            if (row.TodayPayment < 0 || row.TodayPayment > row.Remaining)
            {
                ErrorMessage = $"Versement invalide pour la tranche « {row.Name} ».";
                return;
            }
        }

        try
        {
            InstallmentPaymentCascade.ValidateCascadeOrThrow(InstallmentRows);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        var lines = InstallmentRows
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
            ErrorMessage = "Saisissez au moins un versement du jour.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Enregistrement du paiement…";
        RecalculateCanSubmit();
        try
        {
            var created = await _paymentApi.CreateAsync(new CreatePaymentRequest(
                _situation.StudentId,
                _situation.AcademicYearId,
                null,
                _situation.Currency,
                null,
                lines,
                PaymentDate?.Date ?? DateTime.Today,
                SelectedPaymentCurrency?.CurrencyId,
                _feeCurrencyId,
                ResolveOverrideRateForSave()));

            StatusMessage = "Impression du relevé…";
            try
            {
                await _statementPrint.PrintAsync(created.Id, _situation.FeeTypeId);
            }
            catch (Exception printEx)
            {
                // Paiement OK — l'impression est optionnelle.
                ErrorMessage = $"Paiement enregistré. Impression : {printEx.Message}";
            }

            RequestCloseSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
            RecalculateCanSubmit();
        }
    }

    [RelayCommand]
    private void Cancel() => RequestCloseCancel?.Invoke();

    private async Task LoadCurrenciesAsync()
    {
        var catalog = await _currencyApi.SearchCurrenciesAsync(activeOnly: true);
        var fee = catalog.FirstOrDefault(c =>
            string.Equals(c.Code, _situation.Currency.ToString(), StringComparison.OrdinalIgnoreCase));
        _feeCurrencyId = fee?.Id;

        var allowed = await _currencyApi.GetSchoolCurrenciesAsync(paymentOnly: true);
        PaymentCurrencies.Clear();
        foreach (var item in allowed)
            PaymentCurrencies.Add(item);

        if (PaymentCurrencies.Count == 0 && fee is not null)
        {
            PaymentCurrencies.Add(new SchoolCurrencyDto(
                Guid.Empty, fee.Id, fee.Code, fee.Name, fee.Symbol, true, true));
        }

        SelectedPaymentCurrency =
            PaymentCurrencies.FirstOrDefault(c => c.CurrencyId == _feeCurrencyId)
            ?? PaymentCurrencies.FirstOrDefault(c => c.IsPrimary)
            ?? PaymentCurrencies.FirstOrDefault();

        PaymentCurrencyLabel = SelectedPaymentCurrency?.CurrencyCode ?? FeeCurrencyLabel;
        ShowFxPanel = true;
        await RefreshFxAndRedistributeAsync(reloadRateFromDatabase: true);
    }

    /// <summary>
    /// Le montant saisi est en devise de paiement.
    /// Conversion vers la devise du frais (taux DB, inverse auto) puis répartition sur les tranches.
    /// </summary>
    private async Task RefreshFxAndRedistributeAsync(bool reloadRateFromDatabase)
    {
        if (!_feeCurrencyId.HasValue || SelectedPaymentCurrency is null)
        {
            ConvertedAmountText = "0";
            ExchangeRateHint = string.Empty;
            return;
        }

        if (!InstallmentPaymentCascade.TryParseDecimal(AmountToDistributeText, out var paymentAmount) || paymentAmount < 0)
            paymentAmount = 0;

        try
        {
            decimal feeToPaymentRate;
            if (SelectedPaymentCurrency.CurrencyId == _feeCurrencyId.Value)
            {
                feeToPaymentRate = 1m;
                _activeExchangeRateId = null;
                _databaseRate = 1m;
                SetRateText("1");
                ExchangeRateHint = "Même devise — taux = 1.";
            }
            else if (reloadRateFromDatabase || !CanOverrideExchangeRate)
            {
                // Taux officiel : 1 devise frais = X devise paiement (inverse auto si seul l'autre sens est en base)
                var unit = await _currencyApi.ConvertAsync(new CurrencyConversionRequest(
                    _feeCurrencyId.Value,
                    SelectedPaymentCurrency.CurrencyId,
                    1m));
                feeToPaymentRate = unit.AppliedRate;
                _databaseRate = feeToPaymentRate;
                _activeExchangeRateId = unit.ExchangeRateId;
                SetRateText(feeToPaymentRate.ToString("0.########", CultureInfo.InvariantCulture));
                ExchangeRateHint =
                    $"1 {FeeCurrencyLabel} = {feeToPaymentRate.ToString("N6", CultureInfo.GetCultureInfo("fr-FR"))} {SelectedPaymentCurrency.CurrencyCode} (taux en base)";
            }
            else
            {
                if (!decimal.TryParse(AppliedRateText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out feeToPaymentRate)
                    || feeToPaymentRate <= 0)
                {
                    feeToPaymentRate = _databaseRate > 0 ? _databaseRate : 1m;
                }

                ExchangeRateHint =
                    $"1 {FeeCurrencyLabel} = {feeToPaymentRate.ToString("N6", CultureInfo.GetCultureInfo("fr-FR"))} {SelectedPaymentCurrency.CurrencyCode} (taux modifié)";
            }

            var maxFeePayable = InstallmentRows.Count > 0
                ? InstallmentRows.Sum(r => r.Remaining)
                : GlobalBalance;
            if (maxFeePayable < 0)
                maxFeePayable = 0;

            var maxPaymentPayable = feeToPaymentRate <= 0
                ? 0m
                : Math.Round(maxFeePayable * feeToPaymentRate, 2, MidpointRounding.AwayFromZero);

            var exceeded = paymentAmount > maxPaymentPayable + 0.0000001m;
            if (exceeded)
            {
                paymentAmount = maxPaymentPayable;
                _suppressDistribute = true;
                try
                {
                    AmountToDistributeText = maxPaymentPayable.ToString("0.##", CultureInfo.InvariantCulture);
                }
                finally
                {
                    _suppressDistribute = false;
                }

                ErrorMessage =
                    $"Le montant ne peut pas dépasser le reste à payer ({maxPaymentPayable.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))} {SelectedPaymentCurrency.CurrencyCode} / {maxFeePayable.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))} {FeeCurrencyLabel}).";
            }
            else if (!string.IsNullOrWhiteSpace(ErrorMessage)
                     && ErrorMessage.StartsWith("Le montant ne peut pas dépasser", StringComparison.Ordinal))
            {
                ErrorMessage = string.Empty;
            }

            var feeAmount = feeToPaymentRate <= 0
                ? 0m
                : Math.Round(paymentAmount / feeToPaymentRate, 2, MidpointRounding.AwayFromZero);
            if (feeAmount > maxFeePayable)
                feeAmount = maxFeePayable;

            ConvertedAmountText = feeAmount.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));

            if (!_suppressDistribute)
            {
                _suppressTodayPaymentSync = true;
                try
                {
                    InstallmentPaymentCascade.Redistribute(InstallmentRows, feeAmount);
                }
                finally
                {
                    _suppressTodayPaymentSync = false;
                }

                InstallmentPaymentCascade.RefreshEditability(InstallmentRows);
                UpdateSummaryDisplays();
                RecalculateCanSubmit();
            }
        }
        catch (Exception ex)
        {
            ConvertedAmountText = "—";
            ExchangeRateHint = ex.Message;
            ErrorMessage = ex.Message;
        }
    }

    private void SetRateText(string value)
    {
        _suppressRateReload = true;
        try
        {
            AppliedRateText = value;
        }
        finally
        {
            _suppressRateReload = false;
        }
    }

    private decimal? ResolveOverrideRateForSave()
    {
        if (!CanOverrideExchangeRate)
            return null;
        if (!decimal.TryParse(AppliedRateText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var rate)
            || rate <= 0)
            return null;
        if (Math.Abs(rate - _databaseRate) < 0.0000001m)
            return null;
        if (_feeCurrencyId.HasValue
            && SelectedPaymentCurrency is not null
            && SelectedPaymentCurrency.CurrencyId != _feeCurrencyId.Value)
            return rate;
        return null;
    }

    private BitmapImage? ResolvePhoto(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return null;
        }

        try
        {
            var absolute = Path.IsPathRooted(photoPath) && File.Exists(photoPath)
                ? photoPath
                : _dossierPathResolver.ResolveAbsolutePath(photoPath);

            if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
