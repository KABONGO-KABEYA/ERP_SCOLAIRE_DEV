namespace SchoolManagement.Application.Teacher.Interfaces;

using SchoolManagement.Application.Teacher.DTOs;

public interface ITeacherService
{
    Task<IReadOnlyList<TeacherAssignmentDto>> GetMyAssignmentsAsync(
        Guid teacherId,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeacherStudentDto>> GetClassStudentsAsync(
        Guid teacherId,
        Guid schoolId,
        Guid classRoomId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeacherPeriodDto>> GetAcademicPeriodsAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);
}
