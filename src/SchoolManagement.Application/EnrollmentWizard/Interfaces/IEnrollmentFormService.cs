namespace SchoolManagement.Application.EnrollmentWizard.Interfaces;

using SchoolManagement.Application.EnrollmentWizard.DTOs;

public interface IEnrollmentFormService
{
    Task<EnrollmentFormDocumentDto> GetFormAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default);

    Task<StoredEnrollmentFileDto> SaveToStudentDossierAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default);
}
