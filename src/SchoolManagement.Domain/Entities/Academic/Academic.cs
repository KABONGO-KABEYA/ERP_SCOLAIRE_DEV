using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;

namespace SchoolManagement.Domain.Entities.Academic;

public class Teacher : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public Guid? AddressId { get; set; }

    public PostalAddress? ResidenceAddress { get; set; }

    public string? Specialization { get; set; }

    public DateOnly? HireDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CourseAssignment> CourseAssignments { get; set; } = [];
}

public class CourseAssignment : AuditableEntity, IAggregateRoot
{
    public Guid? TeacherId { get; set; }

    public Guid CourseId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid PedagogicalClassId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Maximum du cours pour la salle et l'année (Max/P tableau droit).</summary>
    public int MaxScore { get; set; } = 20;

    public Teacher? Teacher { get; set; }

    public Course Course { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;

    public AcademicYear AcademicYear { get; set; } = null!;

    public PedagogicalClass PedagogicalClass { get; set; } = null!;

    public ICollection<Evaluation> Evaluations { get; set; } = [];
}

public class ScheduleSlot : AuditableEntity, IAggregateRoot
{
    public Guid CourseAssignmentId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }

    public CourseAssignment CourseAssignment { get; set; } = null!;
}

public class StudentAttendance : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid? CourseAssignmentId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public bool IsPresent { get; set; }

    public bool IsLate { get; set; }

    public string? Justification { get; set; }

    public Student Student { get; set; } = null!;

    public ClassRoom ClassRoom { get; set; } = null!;
}

public class TeacherAttendance : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid TeacherId { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public bool IsPresent { get; set; }

    public string? Notes { get; set; }

    public Teacher Teacher { get; set; } = null!;
}

public class CalendarEvent : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Description { get; set; }

    public bool IsHoliday { get; set; }

    public School School { get; set; } = null!;

    public AcademicYear? AcademicYear { get; set; }
}

public class DisciplineRecord : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public DateOnly IncidentDate { get; set; }

    public string IncidentType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Sanction { get; set; }

    public Guid? ReportedByUserId { get; set; }

    public Student Student { get; set; } = null!;
}

public class MeritRecord : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public DateOnly AwardDate { get; set; }

    public string MeritType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Student Student { get; set; } = null!;
}

public class Announcement : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string TargetAudience { get; set; } = "All";

    public DateTime PublishedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsPublished { get; set; }

    public School School { get; set; } = null!;
}
