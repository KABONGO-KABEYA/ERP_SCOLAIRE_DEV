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

    public decimal DefaultAmount { get; set; }

    public Currency Currency { get; set; } = Currency.CDF;

    public bool IsMandatory { get; set; }

    public bool IsRecurring { get; set; }

    public School School { get; set; } = null!;
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
