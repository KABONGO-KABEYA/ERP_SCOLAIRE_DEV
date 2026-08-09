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

    /// <summary>Deprecated — caisse non gérée ; rester nullable pour l'historique.</summary>
    public Guid? CashRegisterId { get; set; }

    public Guid? BankId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; }

    public decimal TotalAmount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public PaymentStatus Status { get; set; } = PaymentStatus.Complet;

    /// <summary>Deprecated — mode de paiement retiré du produit ; nullable pour l'historique.</summary>
    public string? PaymentMethod { get; set; }

    public string? Notes { get; set; }

    public Guid? ReceivedByUserId { get; set; }

    /// <summary>Devise du frais (référentiel FinDevise) — snapshot au moment du paiement.</summary>
    public Guid? FeeCurrencyId { get; set; }

    /// <summary>Devise réellement utilisée pour le paiement.</summary>
    public Guid? PaymentCurrencyId { get; set; }

    /// <summary>Taux de change appliqué (null si même devise).</summary>
    public Guid? ExchangeRateId { get; set; }

    /// <summary>Montant exprimé dans la devise du frais.</summary>
    public decimal? FeeCurrencyAmount { get; set; }

    /// <summary>Montant exprimé dans la devise de paiement.</summary>
    public decimal? PaymentCurrencyAmount { get; set; }

    /// <summary>Taux utilisé (figé, jamais recalculé).</summary>
    public decimal? AppliedExchangeRate { get; set; }

    public Student Student { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public CashRegister? CashRegister { get; set; }

    public Bank? Bank { get; set; }

    public ICollection<PaymentLine> Lines { get; set; } = [];

    public PaymentReversal? Reversal { get; set; }
}

public class PaymentLine : AuditableEntity
{
    public Guid PaymentId { get; set; }

    public Guid FeeTypeId { get; set; }

    public Guid? FeeInstallmentId { get; set; }

    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public string? Description { get; set; }

    public string? PhysicalReceiptNumber { get; set; }

    public Payment Payment { get; set; } = null!;

    public FeeType FeeType { get; set; } = null!;

    public FeeInstallment? FeeInstallment { get; set; }
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

public class CashMovement : AuditableEntity, IAggregateRoot, ISchoolScoped
{
    public Guid SchoolId { get; set; }

    /// <summary>Deprecated — mouvements caisse non créés sans registre ; nullable pour l'historique.</summary>
    public Guid? CashRegisterId { get; set; }

    public Guid? PaymentId { get; set; }

    public DateTime MovementDate { get; set; }

    public string MovementType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public decimal BalanceAfter { get; set; }

    public string? Description { get; set; }

    public Guid? UserId { get; set; }

    public CashRegister? CashRegister { get; set; }

    public Payment? Payment { get; set; }
}

/// <summary>
/// État financier d'un élève pour une ligne de tarif configurée (<see cref="ClassFeeAmount"/>).
/// <see cref="AmountDue"/> est figé à la création (historique) et ne doit plus suivre les changements de tarif.
/// </summary>
public class StudentFeeBalance : AuditableEntity, IAggregateRoot
{
    public Guid StudentId { get; set; }

    /// <summary>Référence principale vers la configuration officielle (année, classe, catégorie, type, tranche).</summary>
    public Guid ClassFeeAmountId { get; set; }

    /// <summary>Montant attendu figé à la génération du solde (copie historique du tarif).</summary>
    public decimal AmountDue { get; set; }

    public decimal AmountPaid { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public Student Student { get; set; } = null!;

    public ClassFeeAmount ClassFeeAmount { get; set; } = null!;
}
