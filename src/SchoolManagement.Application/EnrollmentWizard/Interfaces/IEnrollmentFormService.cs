namespace SchoolManagement.Application.EnrollmentWizard.Interfaces;

using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Parent.DTOs;

public interface IEnrollmentFormService
{
    Task<EnrollmentFormDocumentDto> GetFormAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default);

    Task<StoredEnrollmentFileDto> SaveToStudentDossierAsync(
        Guid schoolId,
        Guid enrollmentId,
        IReadOnlyList<ParentAppAccessCredentialDto>? parentAccessAccounts = null,
        CancellationToken cancellationToken = default);
}
