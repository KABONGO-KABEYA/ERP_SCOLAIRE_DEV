namespace SchoolManagement.Application.Teacher.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Teacher.DTOs;
using SchoolManagement.Application.Teacher.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;

public sealed class TeacherService : ITeacherService
{
    private readonly IRepository<CourseAssignment> _assignmentRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<AcademicPeriod> _periodRepository;

    public TeacherService(
        IRepository<CourseAssignment> assignmentRepository,
        IRepository<Course> courseRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<Student> studentRepository,
        IRepository<AcademicPeriod> periodRepository)
    {
        _assignmentRepository = assignmentRepository;
        _courseRepository = courseRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _yearRepository = yearRepository;
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _periodRepository = periodRepository;
    }

    public async Task<IReadOnlyList<TeacherAssignmentDto>> GetMyAssignmentsAsync(
        Guid teacherId,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _assignmentRepository.FindAsync(a => a.TeacherId == teacherId, cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        var courseIds = assignments.Select(a => a.CourseId).Distinct().ToList();
        var classIds = assignments.Select(a => a.ClassRoomId).Distinct().ToList();
        var yearIds = assignments.Select(a => a.AcademicYearId).Distinct().ToList();

        var courses = await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);
        var classes = await _classRoomRepository.FindAsync(c => classIds.Contains(c.Id) && c.SchoolId == schoolId, cancellationToken);
        var years = await _yearRepository.FindAsync(y => yearIds.Contains(y.Id) && y.SchoolId == schoolId, cancellationToken);
        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(_pedagogicalClassRepository, schoolId, cancellationToken);
        classes = classes.Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap)).ToList();

        var courseMap = courses.ToDictionary(c => c.Id);
        var classMap = classes.ToDictionary(c => c.Id);
        var yearMap = years.ToDictionary(y => y.Id);

        return assignments
            .Where(a => courseMap.ContainsKey(a.CourseId) && classMap.ContainsKey(a.ClassRoomId))
            .Select(a => new TeacherAssignmentDto(
                a.Id,
                a.CourseId,
                courseMap[a.CourseId].Name,
                a.ClassRoomId,
                classMap[a.ClassRoomId].Name,
                a.AcademicYearId,
                yearMap.GetValueOrDefault(a.AcademicYearId)?.Label ?? "—"))
            .ToList();
    }

    public async Task<IReadOnlyList<TeacherStudentDto>> GetClassStudentsAsync(
        Guid teacherId,
        Guid schoolId,
        Guid classRoomId,
        CancellationToken cancellationToken = default)
    {
        var hasAccess = await _assignmentRepository.FindAsync(
            a => a.TeacherId == teacherId && a.ClassRoomId == classRoomId, cancellationToken);

        if (hasAccess.Count == 0)
        {
            throw new UnauthorizedAccessException("Vous n'enseignez pas dans cette classe.");
        }

        await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository,
            _pedagogicalClassRepository,
            _yearRepository,
            schoolId,
            classRoomId,
            cancellationToken);

        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == classRoomId && e.IsActive, cancellationToken);

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var students = await _studentRepository.FindAsync(
            s => studentIds.Contains(s.Id) && s.SchoolId == schoolId && !s.IsArchived, cancellationToken);

        return students
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Select(s => new TeacherStudentDto(s.Id, s.RegistrationNumber, StudentDisplayName.Format(s)))
            .ToList();
    }

    public async Task<IReadOnlyList<TeacherPeriodDto>> GetAcademicPeriodsAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        var year = await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            academicYearId,
            cancellationToken);

        var periods = await _periodRepository.FindAsync(p => p.AcademicYearId == year.Id, cancellationToken);
        return periods
            .OrderBy(p => p.OrderIndex)
            .Select(p => new TeacherPeriodDto(p.Id, p.Name, p.OrderIndex))
            .ToList();
    }
}
