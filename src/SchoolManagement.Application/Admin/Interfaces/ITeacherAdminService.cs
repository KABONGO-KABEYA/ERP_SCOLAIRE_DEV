namespace SchoolManagement.Application.Admin.Interfaces;

using SchoolManagement.Application.Admin.DTOs;

public interface ITeacherAdminService
{
    Task<IReadOnlyList<TeacherAdminDto>> GetTeachersAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<TeacherAdminDto> CreateTeacherAsync(
        Guid schoolId,
        CreateTeacherAdminRequest request,
        CancellationToken cancellationToken = default);

    Task<TeacherAdminDto> UpdateTeacherAsync(
        Guid schoolId,
        Guid teacherId,
        UpdateTeacherAdminRequest request,
        CancellationToken cancellationToken = default);
}
