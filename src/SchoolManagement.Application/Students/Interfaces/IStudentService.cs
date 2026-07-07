namespace SchoolManagement.Application.Students.Interfaces;

using SchoolManagement.Application.Students.DTOs;

public interface IStudentService
{
    Task<StudentDto?> GetByIdAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default);

    Task<StudentListDto> SearchAsync(Guid schoolId, StudentSearchRequest request, CancellationToken cancellationToken = default);

    Task<StudentDto> CreateAsync(Guid schoolId, CreateStudentRequest request, CancellationToken cancellationToken = default);

    Task<StudentDto> UpdateAsync(Guid schoolId, Guid studentId, UpdateStudentRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default);
}
