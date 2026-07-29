using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Grades;

/// <summary>
/// Type d'épreuve configurable par école (devoir, interrogation, examen…).
/// </summary>
public class EvaluationTypeDefinition : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;

    public ICollection<Evaluation> Evaluations { get; set; } = [];
}

public class Evaluation : AuditableEntity, IAggregateRoot
{
    public Guid? EnrollmentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public Guid CourseAssignmentId { get; set; }

    public Guid EvaluationTypeId { get; set; }

    public Guid CourseId { get; set; }

    public Guid ClassRoomId { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Weight { get; set; } = 1;

    public int MaxScore { get; set; } = 20;

    public DateOnly EvaluationDate { get; set; }

    public bool IsOpen { get; set; } = true;

    public bool IsPublished { get; set; }

    public Enrollment? Enrollment { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;

    public AcademicPeriod AcademicPeriod { get; set; } = null!;

    public CourseAssignment CourseAssignment { get; set; } = null!;

    public EvaluationTypeDefinition EvaluationType { get; set; } = null!;

    public Course Course { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public ICollection<GradeEntry> Grades { get; set; } = [];
}

public class GradeEntry : AuditableEntity, IAggregateRoot
{
    public Guid EvaluationId { get; set; }

    public Guid StudentId { get; set; }

    public decimal Score { get; set; }

    public string? Comment { get; set; }

    public bool IsAbsent { get; set; }

    public Evaluation Evaluation { get; set; } = null!;

    public Student Student { get; set; } = null!;
}

public class PeriodResult : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public Guid ClassRoomId { get; set; }

    public decimal Average { get; set; }

    public decimal Percentage { get; set; }

    public int Rank { get; set; }

    public int ClassSize { get; set; }

    public string? Appreciation { get; set; }

    public ClassCouncilDecision CouncilDecision { get; set; } = ClassCouncilDecision.EnAttente;

    public bool IsPublished { get; set; }

    public Student Student { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public AcademicPeriod AcademicPeriod { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public ICollection<ReportCardDetail> ReportCardDetails { get; set; } = [];
}

public class ReportCard : AuditableEntity, IAggregateRoot
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public string ReportNumber { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }

    public string? PdfPath { get; set; }

    public Student Student { get; set; } = null!;

    public ICollection<ReportCardDetail> Details { get; set; } = [];
}

public class ReportCardDetail : AuditableEntity
{
    public Guid ReportCardId { get; set; }

    public Guid? PeriodResultId { get; set; }

    public Guid CourseId { get; set; }

    public decimal Average { get; set; }

    public decimal Coefficient { get; set; }

    public string? TeacherComment { get; set; }

    public ReportCard ReportCard { get; set; } = null!;

    public PeriodResult? PeriodResult { get; set; }

    public Course Course { get; set; } = null!;
}
