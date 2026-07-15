namespace SchoolManagement.Application.EnrollmentWizard.Interfaces;

using SchoolManagement.Application.EnrollmentWizard.DTOs;

public interface IEnrollmentWizardService
{
    Task<EnrollmentPrerequisitesDto> GetPrerequisitesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<GeneratedRegistrationNumberDto> GenerateRegistrationNumberAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentStudentSearchResultDto>> SearchStudentsAsync(
        Guid schoolId,
        string search,
        bool forReinscription = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentGuardianSearchResultDto>> SearchGuardiansAsync(
        Guid schoolId,
        string search,
        CancellationToken cancellationToken = default);

    Task<StoredEnrollmentFileDto> StoreEnrollmentFileAsync(
        Guid schoolId,
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel,
        string documentType,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<EnrollmentStructureOptionsDto> GetStructureOptionsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<ClassCapacityDto> GetClassCapacityAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentFeeSummaryDto> CalculateFeesAsync(
        Guid schoolId,
        Guid? pedagogicalClassId = null,
        Guid? academicYearId = null,
        IReadOnlyList<Guid>? selectedFeeTypeIds = null,
        IReadOnlyDictionary<Guid, decimal>? discounts = null,
        CancellationToken cancellationToken = default);

    Task<EnrollmentValidationResultDto> ValidateAsync(
        Guid schoolId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<CompleteEnrollmentResultDto> CompleteAsync(
        Guid schoolId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<StudentDossierEditDto> GetStudentDossierForEditAsync(
        Guid schoolId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentValidationResultDto> ValidateStudentDossierUpdateAsync(
        Guid schoolId,
        Guid enrollmentId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateStudentDossierResultDto> UpdateStudentDossierAsync(
        Guid schoolId,
        Guid enrollmentId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);
}
