using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Grades;

/// <summary>
/// Validation officielle des résultats d'une classe pour une sous-période.
/// Point de passage avant délibération, bulletins et publication.
/// </summary>
public class ClassPeriodResultValidation : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    public ResultValidationStatus Status { get; set; } = ResultValidationStatus.NonValide;

    public DateTime? ValidatedAtUtc { get; set; }

    public Guid? ValidatedByUserId { get; set; }

    public string? ValidatedByUserName { get; set; }

    public DateTime? LockedAtUtc { get; set; }

    public Guid? LockedByUserId { get; set; }

    public string? LockedByUserName { get; set; }

    public string? Observations { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public AcademicPeriod AcademicPeriod { get; set; } = null!;

    public ICollection<ClassPeriodResultValidationEvent> Events { get; set; } = [];
}

/// <summary>Journal des opérations de validation / verrouillage.</summary>
public class ClassPeriodResultValidationEvent : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid ValidationId { get; set; }

    public ResultValidationOperation Operation { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public string? Observations { get; set; }

    public ClassPeriodResultValidation Validation { get; set; } = null!;
}
