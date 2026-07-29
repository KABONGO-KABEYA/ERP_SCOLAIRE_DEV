namespace SchoolManagement.Application.Academic.Interfaces;

using SchoolManagement.Application.Academic.DTOs;

public interface IAcademicService
{
    Task<IReadOnlyList<SectionDto>> GetSectionsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassRoomDto>> GetClassRoomsAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<ClassRoomDto> CreateClassRoomAsync(
        Guid schoolId,
        CreateClassRoomRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        Guid schoolId,
        Guid? classRoomId = null,
        CancellationToken cancellationToken = default);

    Task<CourseDto> CreateCourseAsync(
        Guid schoolId,
        CreateCourseRequest request,
        CancellationToken cancellationToken = default);

    Task<CourseDto> UpdateCourseAsync(
        Guid schoolId,
        Guid courseId,
        UpdateCourseRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteCourseAsync(Guid schoolId, Guid courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentDto>> GetEnrollmentsAsync(
        Guid schoolId,
        Guid? classRoomId = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<EnrollmentDto> CreateEnrollmentAsync(
        Guid schoolId,
        CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default);
}
