using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Finance;

/// <summary>Destination financière / compte de répartition (Salaire, Fonctionnement…).</summary>
public class RevenueAllocationDestination : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>
/// Clé de répartition : une par type de frais <em>ou</em> type de retenue et année scolaire.
/// Ouverte tant que <see cref="EndDate"/> est null. Si jamais utilisée, elle peut être supprimée ;
/// sinon l'historique des paiements est conservé.
/// </summary>
public class RevenueAllocationKey : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>Renseigné pour une répartition sur type de frais (sinon null).</summary>
    public Guid? FeeTypeId { get; set; }

    /// <summary>Renseigné pour une répartition sur type de retenue (sinon null).</summary>
    public Guid? WithholdingTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>Date à partir de laquelle la répartition s'applique.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Null tant que la répartition n'est pas clôturée.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>True tant que la répartition n'est pas clôturée (<see cref="EndDate"/> null).</summary>
    public bool IsActive { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;

    public FeeType? FeeType { get; set; }

    public WithholdingType? WithholdingType { get; set; }

    public School School { get; set; } = null!;

    public ICollection<RevenueAllocationKeyDetail> Details { get; set; } = [];

    public bool IsOpen => EndDate is null;

    public RevenueAllocationSourceKind SourceKind =>
        WithholdingTypeId.HasValue
            ? RevenueAllocationSourceKind.Withholding
            : RevenueAllocationSourceKind.FeeType;
}

/// <summary>Ligne d'une clé : destination + pourcentage.</summary>
public class RevenueAllocationKeyDetail : AuditableEntity
{
    public Guid AllocationKeyId { get; set; }

    public Guid DestinationId { get; set; }

    public AllocationCalculationType CalculationType { get; set; } = AllocationCalculationType.Pourcentage;

    /// <summary>Pourcentage (0–100). Doit totaliser 100 % sur la clé.</summary>
    public decimal Value { get; set; }

    public int SortOrder { get; set; }

    public RevenueAllocationKey AllocationKey { get; set; } = null!;

    public RevenueAllocationDestination Destination { get; set; } = null!;
}

/// <summary>
/// Historique définitif de répartition pour un paiement.
/// Ne jamais recalculer ni modifier après création.
/// </summary>
public class RevenueAllocationEntry : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid PaymentId { get; set; }

    /// <summary>Null si répartition par défaut sur le Compte principal (aucune clé configurée).</summary>
    public Guid? AllocationKeyId { get; set; }

    public Guid DestinationId { get; set; }

    public Guid? FeeTypeId { get; set; }

    public Guid? WithholdingTypeId { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Devise du montant réparti (référentiel FinDevise) — snapshot.
    /// Sépare les fonds d'un même compte selon les devises des frais.
    /// </summary>
    public Guid? CurrencyId { get; set; }

    public decimal Amount { get; set; }

    public decimal? AppliedPercentage { get; set; }

    public AllocationCalculationType CalculationType { get; set; }

    public DateTime AllocatedAt { get; set; }

    public Guid? AllocatedByUserId { get; set; }

    public Payment Payment { get; set; } = null!;

    public RevenueAllocationKey? AllocationKey { get; set; }

    public RevenueAllocationDestination Destination { get; set; } = null!;

    public FeeType? FeeType { get; set; }

    public WithholdingType? WithholdingType { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;

    public School School { get; set; } = null!;

    public CurrencyDefinition? Currency { get; set; }
}
