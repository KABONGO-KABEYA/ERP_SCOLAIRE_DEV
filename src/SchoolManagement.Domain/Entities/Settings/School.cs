using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Settings;

public class School : AuditableEntity, IAggregateRoot
{
    public string Name { get; set; } = string.Empty;

    public string? LegalName { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Province { get; set; }

    public string? Country { get; set; } = "RDC";

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? LogoPath { get; set; }

    public string? DocumentHeader { get; set; }

    public Currency DefaultCurrency { get; set; } = Currency.CDF;

    public bool IsActive { get; set; } = true;

    public ICollection<AcademicYear> AcademicYears { get; set; } = [];
}

public class AcademicYear : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Label { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsClosed { get; set; }

    public School School { get; set; } = null!;

    public ICollection<AcademicPeriod> Periods { get; set; } = [];

    public ICollection<ClassRoom> ClassRooms { get; set; } = [];
}

public class Section : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public EducationCycle Cycle { get; set; }

    public string? Description { get; set; }

    public School School { get; set; } = null!;

    public ICollection<ClassRoom> ClassRooms { get; set; } = [];
}

public class StudyOption : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public EducationCycle Cycle { get; set; }

    public string? HumanitiesSection { get; set; }

    public School School { get; set; } = null!;

    public ICollection<ClassRoom> ClassRooms { get; set; } = [];
}

/// <summary>
/// Classe pédagogique configurée par école (niveau officiel RDC activable).
/// </summary>
public class PedagogicalClass : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    /// <summary>Code du modèle officiel (ex. PRI-6, HUM-COM-IG-1).</summary>
    public string TemplateCode { get; set; } = string.Empty;

    public SchoolProgram Program { get; set; }

    public int LevelOrder { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? HumanitiesSection { get; set; }

    public string? StudyOption { get; set; }

    public int? MinAge { get; set; }

    public int? MaxAge { get; set; }

    public bool IsEnabled { get; set; }

    public School School { get; set; } = null!;

    public ICollection<ClassRoom> Locals { get; set; } = [];
}

/// <summary>
/// Local physique d'une classe pédagogique (A, B, Salle 1…).
/// </summary>
public class ClassRoom : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid? PedagogicalClassId { get; set; }

    public Guid SectionId { get; set; }

    public Guid? StudyOptionId { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>Nom du local (A, B, Salle 1…).</summary>
    public string Name { get; set; } = string.Empty;

    public int Level { get; set; }

    public int? MaxCapacity { get; set; }

    public string? Observations { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public PedagogicalClass? PedagogicalClass { get; set; }

    public Section Section { get; set; } = null!;

    public StudyOption? StudyOption { get; set; }

    public ICollection<Course> Courses { get; set; } = [];
}

public class Course : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid? ClassRoomId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Coefficient { get; set; } = 1;

    public int MaxScore { get; set; } = 20;

    public bool IsOptional { get; set; }

    public School School { get; set; } = null!;

    public ClassRoom? ClassRoom { get; set; }
}

public class AcademicPeriod : AuditableEntity, IAggregateRoot
{
    public Guid AcademicYearId { get; set; }

    public string Name { get; set; } = string.Empty;

    public AcademicPeriodType PeriodType { get; set; }

    public int OrderIndex { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsClosed { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;
}

public class FeeType : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Currency Currency { get; set; } = Currency.CDF;

    public bool IsMandatory { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>Tranche affectée à un type de frais, avec ordre propre au type.</summary>
public class FeeTypeInstallment : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid FeeTypeId { get; set; }

    public Guid FeeInstallmentId { get; set; }

    public int SortOrder { get; set; }

    public School School { get; set; } = null!;

    public FeeType FeeType { get; set; } = null!;

    public FeeInstallment FeeInstallment { get; set; } = null!;
}

/// <summary>Tranche de paiement définie librement par l'établissement (1ère tranche, Inscription…).</summary>
public class FeeInstallment : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>Catégorie tarifaire configurable par l'établissement (Général, Boursier…).</summary>
public class FeePricingCategory : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>Montant d'un type de frais pour une année, une classe, une catégorie et une tranche.</summary>
public class ClassFeeAmount : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid PedagogicalClassId { get; set; }

    public Guid FeePricingCategoryId { get; set; }

    public Guid FeeTypeId { get; set; }

    public Guid FeeInstallmentId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>Ordre de priorité de la tranche pour ce type de frais (1, 2, 3…).</summary>
    public int SortOrder { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public PedagogicalClass PedagogicalClass { get; set; } = null!;

    public FeePricingCategory FeePricingCategory { get; set; } = null!;

    public FeeType FeeType { get; set; } = null!;

    public FeeInstallment FeeInstallment { get; set; } = null!;
}

public class Bank : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? AccountNumber { get; set; }

    public string? Branch { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>
/// Caisse — table dépréciée / non utilisée par le produit (historique uniquement).
/// Les encaissements n'écrivent plus de CashRegisterId.
/// </summary>
public class CashRegister : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Currency Currency { get; set; } = Currency.CDF;

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

public class AppConfiguration : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public School School { get; set; } = null!;
}
