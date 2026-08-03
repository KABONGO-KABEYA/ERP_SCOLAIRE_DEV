using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Deliberation;

/// <summary>
/// Décision officielle du Conseil de classe pour un élève / sous-période.
/// Ne modifie jamais les notes, moyennes, rangs ni mentions (PeriodResult).
/// </summary>
public class DeliberationDecision : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>Snapshot de la décision proposée par le moteur au moment de l'enregistrement.</summary>
    public ClassCouncilDecision ProposedDecision { get; set; } = ClassCouncilDecision.EnAttente;

    public FinalCouncilDecision FinalDecision { get; set; }

    public string? Observation { get; set; }

    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? DecidedByUserId { get; set; }

    public string DecidedByUserName { get; set; } = string.Empty;

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public AcademicPeriod AcademicPeriod { get; set; } = null!;

    public Student Student { get; set; } = null!;

    public ICollection<DeliberationDecisionEvent> Events { get; set; } = [];

    public StudentRemedialSession? RemedialSession { get; set; }

    public ICollection<CourseExemption> Exemptions { get; set; } = [];
}

/// <summary>Journal des décisions du Conseil.</summary>
public class DeliberationDecisionEvent : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid DecisionId { get; set; }

    public ClassCouncilDecision ProposedDecision { get; set; }

    public FinalCouncilDecision FinalDecision { get; set; }

    public string? Observation { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DeliberationDecision Decision { get; set; } = null!;
}

/// <summary>Session de repêchage (2ᵉ session) liée à une décision Repêchage.</summary>
public class StudentRemedialSession : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid DecisionId { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public EvaluationSessionKind SessionKind { get; set; } = EvaluationSessionKind.DeuxiemeSession;

    public DeliberationDecision Decision { get; set; } = null!;

    public Student Student { get; set; } = null!;

    public ICollection<StudentRemedialCourse> Courses { get; set; } = [];
}

/// <summary>Cours à coter en 2ᵉ session pour un élève.</summary>
public class StudentRemedialCourse : AuditableEntity, IAggregateRoot
{
    public Guid RemedialSessionId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? CourseAssignmentId { get; set; }

    public RemedialCourseStatus Status { get; set; } = RemedialCourseStatus.ACoter;

    public StudentRemedialSession RemedialSession { get; set; } = null!;

    public Course Course { get; set; } = null!;
}

/// <summary>Dispense de cours — n'altère jamais les notes existantes.</summary>
public class CourseExemption : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid DecisionId { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? CourseAssignmentId { get; set; }

    public string Motive { get; set; } = string.Empty;

    public string? Observation { get; set; }

    public DeliberationDecision Decision { get; set; } = null!;

    public Student Student { get; set; } = null!;

    public Course Course { get; set; } = null!;
}
