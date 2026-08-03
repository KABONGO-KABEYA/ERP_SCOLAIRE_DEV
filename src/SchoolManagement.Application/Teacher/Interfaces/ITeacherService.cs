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

    /// <summary>
    /// Sous-périodes ouvertes pour la classe (même règle que Cotation Desktop).
    /// Liste vide = aucune période ouverte → saisie désactivée.
    /// </summary>
    Task<IReadOnlyList<TeacherPeriodDto>> GetOpenCotationPeriodsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken = default);
}
