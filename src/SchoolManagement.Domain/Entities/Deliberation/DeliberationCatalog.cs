using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;

namespace SchoolManagement.Domain.Entities.Deliberation;

/// <summary>
/// Mention paramétrable par établissement (ex. Satisfaction 55–69 %).
/// Utilisée par le moteur de calcul — jamais codée en dur dans l'UI.
/// </summary>
public class ResultMentionDefinition : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Label { get; set; } = string.Empty;

    public decimal MinPercentageInclusive { get; set; }

    public decimal MaxPercentageInclusive { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>Conduite paramétrable par établissement (Excellent, Très bon…).</summary>
public class ConductDefinition : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;
}

/// <summary>Conduite d'un élève pour une classe / sous-période (saisie en conseil).</summary>
public class StudentPeriodConduct : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public Guid StudentId { get; set; }

    public Guid ConductDefinitionId { get; set; }

    public string? Observation { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public string RecordedByUserName { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public AcademicPeriod AcademicPeriod { get; set; } = null!;

    public Student Student { get; set; } = null!;

    public ConductDefinition ConductDefinition { get; set; } = null!;
}

/// <summary>
/// Bonus pédagogique exceptionnel du Conseil — n'altère pas les GradeEntry d'origine.
/// Après enregistrement, le service déclenche le recalcul officiel PeriodResult.
/// </summary>
public class PedagogicalBonusPoint : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? CourseAssignmentId { get; set; }

    public decimal PointsAdded { get; set; }

    public string Motive { get; set; } = string.Empty;

    public Guid? RecordedByUserId { get; set; }

    public string RecordedByUserName { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsCancelled { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public AcademicPeriod AcademicPeriod { get; set; } = null!;

    public Student Student { get; set; } = null!;

    public Course Course { get; set; } = null!;
}

/// <summary>Journal d'audit générique du conseil de classe.</summary>
public class DeliberationAuditEntry : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public Guid? StudentId { get; set; }

    /// <summary>BonusPoints | Conduct | Decision | ClassValidation | RemedialPeriod</summary>
    public string ActionCode { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? Observation { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
