using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SchoolManagement.Desktop.Models;

/// <summary>Ligne de saisie d'encaissement par tranche (cascade SortOrder).</summary>
public sealed class InstallmentCollectRow : INotifyPropertyChanged
{
    private decimal _todayPayment;
    private string _todayPaymentText = "0";
    private string _physicalNumber = string.Empty;
    private bool _canEditTodayPayment;
    private bool _canEditPhysicalNumber;
    private decimal _lastPaymentAmount;
    private string _lastPaymentAmountText = "0";
    private bool _canEditLastPayment;

    public InstallmentCollectRow(
        Guid feeInstallmentId,
        string name,
        int sortOrder,
        decimal expected,
        decimal paid,
        decimal remaining)
    {
        FeeInstallmentId = feeInstallmentId;
        Name = name;
        SortOrder = sortOrder;
        Expected = expected;
        Paid = paid;
        Remaining = remaining;
    }

    public Guid FeeInstallmentId { get; }
    public string Name { get; }
    public int SortOrder { get; }
    public decimal Expected { get; }
    public decimal Paid { get; }
    public decimal Remaining { get; }

    /// <summary>Montant du dernier versement affecté à cette tranche (écran modification).</summary>
    public decimal LastPaymentAmount => _lastPaymentAmount;

    public string LastPaymentAmountText
    {
        get => _lastPaymentAmountText;
        set
        {
            if (_lastPaymentAmountText == value)
            {
                return;
            }

            _lastPaymentAmountText = value;
            OnPropertyChanged();
        }
    }

    public bool CanEditLastPayment
    {
        get => _canEditLastPayment;
        set
        {
            if (_canEditLastPayment == value)
            {
                return;
            }

            _canEditLastPayment = value;
            OnPropertyChanged();
        }
    }

    public void SetLastPaymentAmount(decimal amount, bool suppressNotify = false)
    {
        amount = Math.Max(0, amount);
        _lastPaymentAmount = amount;
        var text = amount.ToString("0.##", CultureInfo.InvariantCulture);
        if (_lastPaymentAmountText != text)
        {
            _lastPaymentAmountText = text;
            if (!suppressNotify)
            {
                OnPropertyChanged(nameof(LastPaymentAmountText));
            }
        }

        if (!suppressNotify)
        {
            OnPropertyChanged(nameof(LastPaymentAmount));
        }
    }

    /// <summary>Icône MaterialDesign selon le type de tranche (affichage uniquement).</summary>
    public string IconKind
    {
        get
        {
            var n = Name.ToLowerInvariant();
            if (n.Contains("acompt") || n.Contains("inscript"))
            {
                return "CashPlus";
            }

            if (n.Contains("1") || n.Contains("prem"))
            {
                return "Numeric1CircleOutline";
            }

            if (n.Contains("2") || n.Contains("deux"))
            {
                return "Numeric2CircleOutline";
            }

            if (n.Contains("3") || n.Contains("trois"))
            {
                return "Numeric3CircleOutline";
            }

            if (n.Contains("4") || n.Contains("quatr"))
            {
                return "Numeric4CircleOutline";
            }

            return SortOrder switch
            {
                1 => "Numeric1CircleOutline",
                2 => "Numeric2CircleOutline",
                3 => "Numeric3CircleOutline",
                4 => "Numeric4CircleOutline",
                _ => "CircleSlice8"
            };
        }
    }

    public decimal TodayPayment => _todayPayment;

    public string TodayPaymentText
    {
        get => _todayPaymentText;
        set
        {
            if (_todayPaymentText == value)
            {
                return;
            }

            _todayPaymentText = value;
            OnPropertyChanged();
        }
    }

    public string PhysicalNumber
    {
        get => _physicalNumber;
        set
        {
            if (_physicalNumber == value)
            {
                return;
            }

            _physicalNumber = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool CanEditTodayPayment
    {
        get => _canEditTodayPayment;
        set
        {
            if (_canEditTodayPayment == value)
            {
                return;
            }

            _canEditTodayPayment = value;
            OnPropertyChanged();
        }
    }

    public bool CanEditPhysicalNumber
    {
        get => _canEditPhysicalNumber;
        set
        {
            if (_canEditPhysicalNumber == value)
            {
                return;
            }

            _canEditPhysicalNumber = value;
            OnPropertyChanged();
        }
    }

    public void SetTodayPayment(decimal amount, bool suppressNotify)
    {
        amount = Math.Clamp(amount, 0, Remaining);
        _todayPayment = amount;
        var text = amount.ToString("0.##", CultureInfo.InvariantCulture);
        if (_todayPaymentText != text)
        {
            _todayPaymentText = text;
            if (!suppressNotify)
            {
                OnPropertyChanged(nameof(TodayPaymentText));
            }
        }

        if (!suppressNotify)
        {
            OnPropertyChanged(nameof(TodayPayment));
            OnPropertyChanged(nameof(CanEditPhysicalNumber));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
