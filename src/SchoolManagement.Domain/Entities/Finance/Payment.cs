using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Finance;

public class Payment : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid CashRegisterId { get; set; }

    public Guid? BankId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; }

    public decimal TotalAmount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public PaymentStatus Status { get; set; } = PaymentStatus.Complet;

    public string PaymentMethod { get; set; } = "Cash";

    public string? Notes { get; set; }

    public Guid? ReceivedByUserId { get; set; }

    public Student Student { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public CashRegister CashRegister { get; set; } = null!;

    public Bank? Bank { get; set; }

    public ICollection<PaymentLine> Lines { get; set; } = [];

    public PaymentReversal? Reversal { get; set; }
}

public class PaymentLine : AuditableEntity
{
    public Guid PaymentId { get; set; }

    public Guid FeeTypeId { get; set; }

    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public string? Description { get; set; }

    public Payment Payment { get; set; } = null!;

    public FeeType FeeType { get; set; } = null!;
}

public class PaymentReversal : AuditableEntity, IAggregateRoot
{
    public Guid PaymentId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime ReversedAt { get; set; }

    public Guid ReversedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public bool IsApproved { get; set; }

    public Payment Payment { get; set; } = null!;
}

public class CashMovement : AuditableEntity, IAggregateRoot
{
    public Guid CashRegisterId { get; set; }

    public Guid? PaymentId { get; set; }

    public DateTime MovementDate { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public decimal BalanceAfter { get; set; }

    public string? Description { get; set; }

    public Guid? UserId { get; set; }

    public CashRegister CashRegister { get; set; } = null!;

    public Payment? Payment { get; set; }
}

public class StudentFeeBalance : AuditableEntity, IAggregateRoot
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid FeeTypeId { get; set; }

    public decimal AmountDue { get; set; }

    public decimal AmountPaid { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public Student Student { get; set; } = null!;

    public FeeType FeeType { get; set; } = null!;
}
