using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Application.CurrencyManagement.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Desktop.ViewModels;

public sealed partial class ExpenseMultiCurrencyAllocationLineViewModel : ObservableObject
{
    public ExpenseMultiCurrencyAllocationLineViewModel(
        Guid currencyId,
        string currencyCode,
        decimal availableAmount,
        bool isPrimary)
    {
        CurrencyId = currencyId;
        CurrencyCode = currencyCode;
        AvailableAmount = availableAmount;
        IsPrimary = isPrimary;
    }

    public Guid CurrencyId { get; }
    public string CurrencyCode { get; }
    public decimal AvailableAmount { get; }
    public bool IsPrimary { get; }

    public string AvailableDisplay => $"{AvailableAmount:N2} {CurrencyCode}";

    [ObservableProperty] private decimal _rate = 1m;
    [ObservableProperty] private string _usedAmountText = "0";
    [ObservableProperty] private decimal _equivalentAmount;
    [ObservableProperty] private string _rateDirectionLabel = string.Empty;
    [ObservableProperty] private Guid? _exchangeRateId;
    [ObservableProperty] private bool _isRateReadOnly = true;

    public decimal UsedAmount
    {
        get
        {
            if (decimal.TryParse(UsedAmountText.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var inv))
                return inv;
            if (decimal.TryParse(UsedAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var loc))
                return loc;
            return 0m;
        }
    }

    public decimal RemainingAmount => Math.Max(0, AvailableAmount - UsedAmount);
    public string RemainingDisplay => $"{RemainingAmount:N2} {CurrencyCode}";
    public string EquivalentDisplay => $"{EquivalentAmount:N2}";

    partial void OnUsedAmountTextChanged(string value)
    {
        OnPropertyChanged(nameof(UsedAmount));
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(RemainingDisplay));
    }

    partial void OnEquivalentAmountChanged(decimal value) => OnPropertyChanged(nameof(EquivalentDisplay));

    public void NotifyRemaining()
    {
        OnPropertyChanged(nameof(UsedAmount));
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(RemainingDisplay));
        OnPropertyChanged(nameof(EquivalentDisplay));
    }
}

/// <summary>Répartition automatique / manuelle d'une dépense sur plusieurs devises d'un même compte.</summary>
public sealed partial class ExpenseMultiCurrencyAllocationViewModel : ViewModelBase
{
    private readonly ICurrencyApiService _currencyApi;
    private readonly DateOnly _asOfDate;
    private bool _suppressRecalc;

    public ExpenseMultiCurrencyAllocationViewModel(
        string accountTitle,
        decimal expenseAmount,
        string primaryCurrencyCode,
        Guid? primaryCurrencyId,
        IReadOnlyList<ExpenseCurrencyBalanceLine> balances,
        DateOnly asOfDate,
        ICurrencyApiService currencyApi,
        IAuthSessionService authSession)
    {
        AccountTitle = accountTitle;
        ExpenseAmount = expenseAmount;
        PrimaryCurrencyCode = primaryCurrencyCode;
        PrimaryCurrencyId = primaryCurrencyId;
        _asOfDate = asOfDate;
        _currencyApi = currencyApi;

        CanOverrideRate = authSession.IsAdministrator
            || (authSession.CurrentUser?.Permissions.Any(p =>
                    string.Equals(p, Permissions.PaymentFxOverride, StringComparison.OrdinalIgnoreCase)) == true);

        foreach (var balance in balances
                     .Where(b => b.CurrencyId.HasValue)
                     .OrderByDescending(b => string.Equals(b.CurrencyCode, primaryCurrencyCode, StringComparison.OrdinalIgnoreCase))
                     .ThenBy(b => b.CurrencyCode))
        {
            var isPrimary = string.Equals(balance.CurrencyCode, primaryCurrencyCode, StringComparison.OrdinalIgnoreCase)
                            || (primaryCurrencyId.HasValue && balance.CurrencyId == primaryCurrencyId);
            Lines.Add(new ExpenseMultiCurrencyAllocationLineViewModel(
                balance.CurrencyId!.Value,
                balance.CurrencyCode,
                balance.AvailableAmount,
                isPrimary)
            {
                IsRateReadOnly = !CanOverrideRate || isPrimary
            });
        }
    }

    public string AccountTitle { get; }
    public decimal ExpenseAmount { get; }
    public string PrimaryCurrencyCode { get; }
    public Guid? PrimaryCurrencyId { get; }
    public bool CanOverrideRate { get; }
    public ObservableCollection<ExpenseMultiCurrencyAllocationLineViewModel> Lines { get; } = [];

    public bool Confirmed { get; private set; }
    public IReadOnlyList<CreateExpensePaymentAllocationLine> ConfirmedLines { get; private set; } = [];

    public event Action? RequestCloseSuccess;
    public event Action? RequestCloseCancel;

    [ObservableProperty] private string _statusMessage = "Répartition automatique en cours…";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private decimal _coveredEquivalent;
    [ObservableProperty] private decimal _remainingToCover;

    public string ExpenseAmountDisplay => $"{ExpenseAmount:N2} {PrimaryCurrencyCode}";
    public string CoveredDisplay => $"{CoveredEquivalent:N2} {PrimaryCurrencyCode}";
    public string RemainingToCoverDisplay => $"{RemainingToCover:N2} {PrimaryCurrencyCode}";
    public bool IsFullyCovered =>
        Math.Abs(CoveredEquivalent - ExpenseAmount) <= 0.05m
        && CoveredEquivalent + 0.009m >= ExpenseAmount;
    public bool CanConfirm => IsFullyCovered && !IsBusy && Lines.Any(l => l.UsedAmount > 0);

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await LoadRatesAsync();
            await AutoAllocateAsync();
            StatusMessage = IsFullyCovered
                ? "Répartition proposée. Vous pouvez ajuster les montants utilisés."
                : "Solde cumulé insuffisant pour couvrir entièrement la dépense.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyTotals();
        }
    }

    [RelayCommand]
    private async Task AutoAllocateAsync()
    {
        _suppressRecalc = true;
        try
        {
            foreach (var line in Lines)
            {
                line.UsedAmountText = "0";
                line.EquivalentAmount = 0;
            }

            var remaining = ExpenseAmount;
            var ordered = Lines
                .OrderByDescending(l => l.IsPrimary)
                .ThenByDescending(l => l.AvailableAmount * (l.Rate <= 0 ? 0 : l.Rate))
                .ToList();

            foreach (var line in ordered)
            {
                if (remaining <= 0.009m)
                    break;

                if (line.AvailableAmount <= 0)
                    continue;

                if (line.IsPrimary || line.Rate <= 0)
                {
                    var usePrimary = Math.Min(line.AvailableAmount, remaining);
                    usePrimary = decimal.Round(usePrimary, 2, MidpointRounding.AwayFromZero);
                    line.UsedAmountText = usePrimary.ToString("0.##", CultureInfo.CurrentCulture);
                    line.EquivalentAmount = usePrimary;
                    line.Rate = 1m;
                }
                else
                {
                    // Calcule le montant devise pour couvrir exactement le reste,
                    // sans dépasser le disponible ni l'équivalent demandé (évite 30 008 pour 30 000).
                    await FitSecondaryLineToRemainingAsync(line, remaining);
                }

                remaining = decimal.Round(
                    ExpenseAmount - Lines.Sum(l => l.EquivalentAmount),
                    2,
                    MidpointRounding.AwayFromZero);
            }

            await SnapCoveredToExpenseAmountAsync();
        }
        finally
        {
            _suppressRecalc = false;
            foreach (var line in Lines)
                line.NotifyRemaining();
            NotifyTotals();
        }
    }

    /// <summary>
    /// Place sur une devise secondaire le montant nécessaire pour couvrir <paramref name="remainingPrimary"/>,
    /// en forçant l'équivalent à ce reste (taux effectif) pour éviter les dépassements d'arrondi.
    /// </summary>
    private async Task FitSecondaryLineToRemainingAsync(
        ExpenseMultiCurrencyAllocationLineViewModel line,
        decimal remainingPrimary)
    {
        if (remainingPrimary <= 0 || line.Rate <= 0)
        {
            line.UsedAmountText = "0";
            line.EquivalentAmount = 0;
            return;
        }

        // Quantité théorique pour couvrir exactement le reste.
        var rawNeeded = remainingPrimary / line.Rate;
        var useInCurrency = decimal.Round(rawNeeded, 2, MidpointRounding.AwayFromZero);
        useInCurrency = Math.Min(line.AvailableAmount, useInCurrency);

        if (useInCurrency <= 0)
        {
            line.UsedAmountText = "0";
            line.EquivalentAmount = 0;
            return;
        }

        // Si l'arrondi à 2 décimales sous-couvre, tenter +0,01 si disponible.
        var marketEquivalent = decimal.Round(useInCurrency * line.Rate, 2, MidpointRounding.AwayFromZero);
        while (marketEquivalent + 0.009m < remainingPrimary
               && useInCurrency + 0.01m <= line.AvailableAmount + 0.0000001m)
        {
            useInCurrency = decimal.Round(useInCurrency + 0.01m, 2, MidpointRounding.AwayFromZero);
            marketEquivalent = decimal.Round(useInCurrency * line.Rate, 2, MidpointRounding.AwayFromZero);
        }

        // Cap : on ne crédite jamais plus que le reste à couvrir (corrige 1,75×2290 → 4008).
        var exactEquivalent = Math.Min(marketEquivalent, remainingPrimary);
        exactEquivalent = decimal.Round(exactEquivalent, 2, MidpointRounding.AwayFromZero);

        // Taux effectif pour que Montant × Taux = équivalent exact (persisté côté API).
        var effectiveRate = useInCurrency > 0
            ? decimal.Round(exactEquivalent / useInCurrency, 8, MidpointRounding.AwayFromZero)
            : line.Rate;

        line.UsedAmountText = useInCurrency.ToString("0.##", CultureInfo.CurrentCulture);
        line.EquivalentAmount = exactEquivalent;
        line.Rate = effectiveRate;
        line.RateDirectionLabel =
            $"1 {line.CurrencyCode} = {effectiveRate:N8} {PrimaryCurrencyCode}"
            + (Math.Abs(marketEquivalent - exactEquivalent) > 0.009m
                ? " (ajusté pour coller au montant)"
                : string.Empty);

        // Garde le taux marché en mémoire via ConvertAsync si pas d'ajustement.
        if (Math.Abs(marketEquivalent - exactEquivalent) <= 0.009m && PrimaryCurrencyId is not null)
        {
            try
            {
                var conversion = await _currencyApi.ConvertAsync(new CurrencyConversionRequest(
                    line.CurrencyId,
                    PrimaryCurrencyId.Value,
                    useInCurrency,
                    AsOfDate: _asOfDate));
                line.ExchangeRateId = conversion.ExchangeRateId;
                line.Rate = conversion.AppliedRate;
                line.EquivalentAmount = decimal.Round(conversion.TargetAmount, 2, MidpointRounding.AwayFromZero);
                // Re-cap au cas où l'API arrondit encore trop haut.
                if (line.EquivalentAmount > remainingPrimary)
                {
                    line.EquivalentAmount = remainingPrimary;
                    line.Rate = decimal.Round(remainingPrimary / useInCurrency, 8, MidpointRounding.AwayFromZero);
                    line.RateDirectionLabel =
                        $"1 {line.CurrencyCode} = {line.Rate:N8} {PrimaryCurrencyCode} (ajusté pour coller au montant)";
                }
                else
                {
                    line.RateDirectionLabel =
                        $"1 {line.CurrencyCode} = {conversion.AppliedRate:N8} {PrimaryCurrencyCode}";
                }
            }
            catch
            {
                // Conservé : taux effectif déjà posé.
            }
        }
    }

    /// <summary>Corrige un éventuel dépassement / sous-couverture résiduelle après allocation.</summary>
    private Task SnapCoveredToExpenseAmountAsync()
    {
        var covered = decimal.Round(Lines.Sum(l => l.EquivalentAmount), 2, MidpointRounding.AwayFromZero);
        var delta = decimal.Round(ExpenseAmount - covered, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(delta) <= 0.009m)
            return Task.CompletedTask;

        // Réduit le dépassement sur la dernière devise complémentaire utilisée.
        if (delta < 0)
        {
            var excess = -delta;
            foreach (var line in Lines.Where(l => !l.IsPrimary && l.UsedAmount > 0).Reverse())
            {
                var shrink = Math.Min(excess, line.EquivalentAmount);
                if (shrink <= 0)
                    continue;

                line.EquivalentAmount = decimal.Round(line.EquivalentAmount - shrink, 2, MidpointRounding.AwayFromZero);
                if (line.UsedAmount > 0 && line.EquivalentAmount > 0)
                {
                    line.Rate = decimal.Round(line.EquivalentAmount / line.UsedAmount, 8, MidpointRounding.AwayFromZero);
                    line.RateDirectionLabel =
                        $"1 {line.CurrencyCode} = {line.Rate:N8} {PrimaryCurrencyCode} (ajusté pour coller au montant)";
                }
                else if (line.EquivalentAmount <= 0)
                {
                    line.UsedAmountText = "0";
                    line.EquivalentAmount = 0;
                }

                excess -= shrink;
                if (excess <= 0.009m)
                    break;
            }
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RecalculateAllAsync()
    {
        foreach (var line in Lines.Where(l => l.UsedAmount > 0))
            await RecalculateLineEquivalentAsync(line, line.UsedAmount);
        NotifyTotals();
    }

    public async Task OnLineAmountEditedAsync(ExpenseMultiCurrencyAllocationLineViewModel line)
    {
        if (_suppressRecalc || IsBusy)
            return;

        if (line.UsedAmount > line.AvailableAmount + 0.009m)
        {
            line.UsedAmountText = line.AvailableAmount.ToString("0.##", CultureInfo.CurrentCulture);
            StatusMessage = $"Montant plafonné au disponible ({line.AvailableDisplay}).";
        }

        await RecalculateLineEquivalentAsync(line, line.UsedAmount);
        NotifyTotals();
    }

    public async Task OnLineRateEditedAsync(ExpenseMultiCurrencyAllocationLineViewModel line)
    {
        if (_suppressRecalc || IsBusy || line.IsPrimary)
            return;

        await RecalculateLineEquivalentAsync(line, line.UsedAmount);
        NotifyTotals();
    }

    [RelayCommand]
    private void Confirm()
    {
        NotifyTotals();
        if (!CanConfirm)
        {
            StatusMessage = IsFullyCovered
                ? "Saisissez au moins un montant utilisé."
                : $"Solde cumulé insuffisant : il reste {RemainingToCoverDisplay} à couvrir. Aucun mouvement ne sera enregistré.";
            return;
        }

        foreach (var line in Lines)
        {
            if (line.UsedAmount > line.AvailableAmount + 0.009m)
            {
                StatusMessage = $"Montant utilisé en {line.CurrencyCode} supérieur au disponible.";
                return;
            }
        }

        ConfirmedLines = Lines
            .Where(l => l.UsedAmount > 0)
            .Select(l => new CreateExpensePaymentAllocationLine(
                l.CurrencyId,
                decimal.Round(l.UsedAmount, 2, MidpointRounding.AwayFromZero),
                // Taux effectif des devises complémentaires → équivalent exact côté API.
                l.IsPrimary ? null : l.Rate))
            .ToList();
        Confirmed = true;
        RequestCloseSuccess?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestCloseCancel?.Invoke();

    private async Task LoadRatesAsync()
    {
        if (PrimaryCurrencyId is null)
        {
            foreach (var line in Lines)
            {
                line.Rate = 1m;
                line.RateDirectionLabel = line.IsPrimary
                    ? $"Devise principale ({PrimaryCurrencyCode})"
                    : "Taux indisponible (devise principale introuvable)";
            }

            return;
        }

        foreach (var line in Lines)
        {
            if (line.IsPrimary || line.CurrencyId == PrimaryCurrencyId)
            {
                line.Rate = 1m;
                line.ExchangeRateId = null;
                line.RateDirectionLabel = $"1 {line.CurrencyCode} = 1 {PrimaryCurrencyCode}";
                continue;
            }

            try
            {
                var unit = await _currencyApi.ConvertAsync(new CurrencyConversionRequest(
                    line.CurrencyId,
                    PrimaryCurrencyId.Value,
                    1m,
                    AsOfDate: _asOfDate));
                line.Rate = unit.AppliedRate;
                line.ExchangeRateId = unit.ExchangeRateId;
                line.RateDirectionLabel =
                    $"1 {line.CurrencyCode} = {unit.AppliedRate:N8} {PrimaryCurrencyCode}"
                    + (unit.EffectiveDate is { } d ? $" (taux du {d:dd/MM/yyyy})" : string.Empty);
            }
            catch (Exception ex)
            {
                line.Rate = 0m;
                line.RateDirectionLabel = $"Taux introuvable : {ex.Message}";
            }
        }
    }

    private async Task RecalculateLineEquivalentAsync(ExpenseMultiCurrencyAllocationLineViewModel line, decimal usedAmount)
    {
        if (usedAmount <= 0)
        {
            line.EquivalentAmount = 0;
            line.NotifyRemaining();
            return;
        }

        if (line.IsPrimary || PrimaryCurrencyId is null || line.CurrencyId == PrimaryCurrencyId)
        {
            line.EquivalentAmount = decimal.Round(usedAmount, 2, MidpointRounding.AwayFromZero);
            line.NotifyRemaining();
            return;
        }

        try
        {
            decimal? overrideRate = CanOverrideRate && !line.IsRateReadOnly ? line.Rate : null;
            var conversion = await _currencyApi.ConvertAsync(new CurrencyConversionRequest(
                line.CurrencyId,
                PrimaryCurrencyId.Value,
                usedAmount,
                AsOfDate: _asOfDate,
                OverrideRate: overrideRate));
            line.Rate = conversion.AppliedRate;
            line.ExchangeRateId = conversion.ExchangeRateId;
            line.EquivalentAmount = decimal.Round(conversion.TargetAmount, 2, MidpointRounding.AwayFromZero);
            line.RateDirectionLabel =
                $"1 {line.CurrencyCode} = {conversion.AppliedRate:N8} {PrimaryCurrencyCode}";
        }
        catch (Exception ex)
        {
            // Fallback local si API convert échoue mais taux déjà chargé.
            if (line.Rate > 0)
            {
                line.EquivalentAmount = decimal.Round(usedAmount * line.Rate, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                line.EquivalentAmount = 0;
                StatusMessage = ex.Message;
            }
        }

        line.NotifyRemaining();
    }

    private void NotifyTotals()
    {
        CoveredEquivalent = decimal.Round(Lines.Sum(l => l.EquivalentAmount), 2, MidpointRounding.AwayFromZero);
        var gap = decimal.Round(ExpenseAmount - CoveredEquivalent, 2, MidpointRounding.AwayFromZero);
        RemainingToCover = gap > 0 ? gap : 0m;
        OnPropertyChanged(nameof(ExpenseAmountDisplay));
        OnPropertyChanged(nameof(CoveredDisplay));
        OnPropertyChanged(nameof(RemainingToCoverDisplay));
        OnPropertyChanged(nameof(IsFullyCovered));
        OnPropertyChanged(nameof(CanConfirm));

        if (CoveredEquivalent > ExpenseAmount + 0.05m)
        {
            StatusMessage =
                $"Attention : la couverture ({CoveredDisplay}) dépasse la dépense ({ExpenseAmountDisplay}). Ajustez les montants.";
        }
    }
}
