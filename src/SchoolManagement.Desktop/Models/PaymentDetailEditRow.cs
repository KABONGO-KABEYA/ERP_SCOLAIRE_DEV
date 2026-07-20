using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.Models;

/// <summary>Ligne de détail de versement (PaymentLine) pour modification / annulation.</summary>
public sealed class PaymentDetailEditRow : INotifyPropertyChanged
{
    private decimal _amount;
    private string _amountText = "0";
    private string _physicalNumber = string.Empty;
    private bool _canEdit;

    public PaymentDetailEditRow(
        Guid paymentId,
        Guid lineId,
        int number,
        string receiptNumber,
        DateTime paymentDate,
        DateTime createdAt,
        decimal amount,
        Currency currency,
        PaymentStatus status,
        string? physicalNumber,
        bool canEdit)
    {
        PaymentId = paymentId;
        LineId = lineId;
        Number = number;
        ReceiptNumber = receiptNumber;
        PaymentDate = paymentDate;
        CreatedAt = createdAt;
        Currency = currency;
        Status = status;
        _canEdit = canEdit;
        SetAmount(amount, suppressNotify: true);
        PhysicalNumber = physicalNumber ?? string.Empty;
    }

    public Guid PaymentId { get; }
    public Guid LineId { get; }
    public int Number { get; }
    public string ReceiptNumber { get; }
    public DateTime PaymentDate { get; }
    public DateTime CreatedAt { get; }
    public Currency Currency { get; }
    public PaymentStatus Status { get; }

    public decimal Amount => _amount;

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (_amountText == value)
            {
                return;
            }

            _amountText = value;
            OnPropertyChanged();
        }
    }

    public string PhysicalNumber
    {
        get => _physicalNumber;
        set
        {
            var next = value ?? string.Empty;
            if (_physicalNumber == next)
            {
                return;
            }

            _physicalNumber = next;
            OnPropertyChanged();
        }
    }

    public bool CanEdit
    {
        get => _canEdit;
        set
        {
            if (_canEdit == value)
            {
                return;
            }

            _canEdit = value;
            OnPropertyChanged();
        }
    }

    public void SetAmount(decimal amount, bool suppressNotify = false)
    {
        amount = Math.Max(0, amount);
        _amount = amount;
        var text = amount.ToString("0.##", CultureInfo.InvariantCulture);
        if (_amountText != text)
        {
            _amountText = text;
            if (!suppressNotify)
            {
                OnPropertyChanged(nameof(AmountText));
            }
        }

        if (!suppressNotify)
        {
            OnPropertyChanged(nameof(Amount));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
