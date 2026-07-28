using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Finance;

/// <summary>Demande de paiement / bon de décaissement.</summary>
public class ExpenseRequest : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid DestinationId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal RequestedAmount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public DateOnly RequestDate { get; set; }

    public ExpenseRequestStatus Status { get; set; } = ExpenseRequestStatus.Brouillon;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public RevenueAllocationDestination Destination { get; set; } = null!;
}

/// <summary>Dépense effective imputée sur un compte bénéficiaire.</summary>
public class ExpensePayment : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid DestinationId { get; set; }

    public Guid? ExpenseRequestId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Nom de la personne ou structure qui reçoit le paiement.</summary>
    public string BeneficiaryName { get; set; } = string.Empty;

    /// <summary>Nom de la personne ayant autorisé la dépense.</summary>
    public string AuthorizedByName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public DateOnly ExpenseDate { get; set; }

    /// <summary>Devise principale de la dépense (catalog FinDevise). Complète l'enum legacy <see cref="Currency"/>.</summary>
    public Guid? PrimaryCurrencyId { get; set; }

    /// <summary>Référence / N° pièce fourni par l'utilisateur (distinct de la référence système).</summary>
    public string? ExternalReference { get; set; }

    /// <summary>Catégorie métier (fonctionnement, pédagogie, …).</summary>
    public string? Category { get; set; }

    public string? Observations { get; set; }

    public string? AttachmentFileName { get; set; }

    /// <summary>Chemin relatif sous le stockage fichiers (ex. depenses/...).</summary>
    public string? AttachmentStoragePath { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public RevenueAllocationDestination Destination { get; set; } = null!;

    public ExpenseRequest? ExpenseRequest { get; set; }

    public CurrencyDefinition? PrimaryCurrency { get; set; }

    public ICollection<ExpensePaymentAllocation> Allocations { get; set; } = new List<ExpensePaymentAllocation>();
}

/// <summary>Mouvement de financement d'une dépense dans une devise donnée.</summary>
public class ExpensePaymentAllocation : AuditableEntity
{
    public Guid SchoolId { get; set; }

    public Guid ExpensePaymentId { get; set; }

    /// <summary>Devise prélevée sur le compte.</summary>
    public Guid CurrencyId { get; set; }

    /// <summary>Montant prélevé dans <see cref="CurrencyId"/>.</summary>
    public decimal Amount { get; set; }

    public Guid? ExchangeRateId { get; set; }

    /// <summary>Taux appliqué : 1 unité de CurrencyId → combien d'unités de la devise principale de la dépense.</summary>
    public decimal AppliedExchangeRate { get; set; } = 1m;

    /// <summary>Équivalent dans la devise principale de la dépense.</summary>
    public decimal EquivalentInPrimaryCurrency { get; set; }

    public int SortOrder { get; set; }

    public School School { get; set; } = null!;

    public ExpensePayment ExpensePayment { get; set; } = null!;

    public CurrencyDefinition Currency { get; set; } = null!;

    public ExchangeRate? ExchangeRate { get; set; }
}
