using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Domain.Entities.Grades;

/// <summary>
/// Procès-verbal de délibération (conseil de classe) pour une classe / sous-période.
/// Ne modifie jamais les résultats scolaires — texte administratif uniquement.
/// </summary>
public class ClassPeriodDeliberationMinutes : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicPeriodId { get; set; }

    /// <summary>Observations générales du conseil.</summary>
    public string? GeneralObservations { get; set; }

    /// <summary>Décisions du Conseil (hors notes / moyennes).</summary>
    public string? CouncilDecisions { get; set; }

    /// <summary>Recommandations pédagogiques.</summary>
    public string? PedagogicalRecommendations { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? RecordedByUserId { get; set; }

    public string RecordedByUserName { get; set; } = string.Empty;

    public School School { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public AcademicPeriod AcademicPeriod { get; set; } = null!;
}
