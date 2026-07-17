using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Finance;

/// <summary>Type de retenue (Contribution diocésaine, Fonds social…).</summary>
public class WithholdingType : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>
/// Configuration de retenue versionnée par année scolaire.
/// Tranche et catégorie nulles = configuration générale.
/// </summary>
public class WithholdingConfiguration : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid WithholdingTypeId { get; set; }

    public Guid FeeTypeId { get; set; }

    /// <summary>Null = toutes les tranches.</summary>
    public Guid? FeeInstallmentId { get; set; }

    /// <summary>Null = toutes les catégories tarifaires.</summary>
    public Guid? PricingCategoryId { get; set; }

    public WithholdingCalculationMode CalculationMode { get; set; }

    public decimal Value { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public WithholdingType WithholdingType { get; set; } = null!;

    public FeeType FeeType { get; set; } = null!;

    public FeeInstallment? FeeInstallment { get; set; }

    public FeePricingCategory? PricingCategory { get; set; }
}
