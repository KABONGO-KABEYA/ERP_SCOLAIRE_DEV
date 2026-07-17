using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Desktop.Helpers;
using SchoolManagement.Desktop.Models;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class CollectPaymentViewModel : ObservableObject
{
    private readonly StudentPaymentSituationDto _situation;
    private readonly IPaymentApiService _paymentApi;
    private readonly IFinanceApiService _financeApi;
    private readonly IAuthSessionService _authSession;
    private readonly IStudentDossierPathResolver _dossierPathResolver;
    private readonly IFeeTypeStatementPrintService _statementPrint;
    private bool _suppressDistribute;
    private bool _suppressTodayPaymentSync;

    public CollectPaymentViewModel(
        StudentPaymentSituationDto situation,
        IPaymentApiService paymentApi,
        IFinanceApiService financeApi,
        IAuthSessionService authSession,
        IStudentDossierPathResolver dossierPathResolver,
        IFeeTypeStatementPrintService statementPrint)
    {
        _situation = situation;
        _paymentApi = paymentApi;
        _financeApi = financeApi;
        _authSession = authSession;
        _dossierPathResolver = dossierPathResolver;
        _statementPrint = statementPrint;

        FullName = situation.FullName;
        AcademicYearLabel = situation.AcademicYearLabel;
        CurrencyLabel = situation.Currency.ToString();
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

        PhotoSource = ResolvePhoto(situation.PhotoPath);
        HasPhoto = PhotoSource is not null;
    }

    public ObservableCollection<InstallmentCollectRow> InstallmentRows { get; } = [];

    public string FullName { get; }
    public string AcademicYearLabel { get; }
    public string CurrencyLabel { get; }
    public string ClassName { get; }
    public string FeePricingCategoryName { get; }
    public string RegistrationNumber { get; }
    public string FeeTypeName { get; }
    public decimal AmountExpected { get; }
    public decimal AmountPaid { get; }
    public decimal GlobalBalance { get; }
    public string RecordedByName { get; }
    public string RecordedByRole { get; }
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

    public event Action? RequestCloseSuccess;
    public event Action? RequestCloseCancel;

    partial void OnPaymentDateChanged(DateTime? value) => RecalculateCanSubmit();
    partial void OnIsBusyChanged(bool value) => RecalculateCanSubmit();

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

        if (!InstallmentPaymentCascade.TryParseDecimal(value, out var total) || total < 0)
        {
            total = 0;
        }

        _suppressTodayPaymentSync = true;
        try
        {
            InstallmentPaymentCascade.Redistribute(InstallmentRows, total);
        }
        finally
        {
            _suppressTodayPaymentSync = false;
        }

        InstallmentPaymentCascade.RefreshEditability(InstallmentRows);
        UpdateSummaryDisplays();
        RecalculateCanSubmit();
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
        var sum = InstallmentRows.Sum(r => r.TodayPayment);
        _suppressDistribute = true;
        try
        {
            AmountToDistributeText = sum.ToString("0.##", CultureInfo.InvariantCulture);
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
                PaymentDate?.Date ?? DateTime.Today));

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
