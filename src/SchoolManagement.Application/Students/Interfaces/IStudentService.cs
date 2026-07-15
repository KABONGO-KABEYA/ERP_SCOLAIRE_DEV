namespace SchoolManagement.Application.Students.Interfaces;

using SchoolManagement.Application.Students;
using SchoolManagement.Application.Students.DTOs;

public interface IStudentService
{
    Task<StudentDto?> GetByIdAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default);

    Task<StudentProfileDto?> GetProfileAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default);

    Task<StudentListDto> SearchAsync(Guid schoolId, StudentSearchRequest request, CancellationToken cancellationToken = default);

    Task<StudentDto> CreateAsync(Guid schoolId, CreateStudentRequest request, CancellationToken cancellationToken = default);

    Task<StudentDto> UpdateAsync(Guid schoolId, Guid studentId, UpdateStudentRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default);

    Task WithdrawFromCurrentYearAsync(
        Guid schoolId,
        Guid studentId,
        WithdrawFromCurrentYearRequest request,
        CancellationToken cancellationToken = default);

    WithdrawalReasonsDto GetWithdrawalReasons();

    Task<IReadOnlyList<StudentDossierFileDto>> ListDossierFilesAsync(
        Guid schoolId,
        Guid studentId,
        CancellationToken cancellationToken = default);
}
