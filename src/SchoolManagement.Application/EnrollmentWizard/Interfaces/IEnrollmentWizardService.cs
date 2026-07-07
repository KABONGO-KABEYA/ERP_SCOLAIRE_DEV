namespace SchoolManagement.Application.EnrollmentWizard.Interfaces;

using SchoolManagement.Application.EnrollmentWizard.DTOs;

public interface IEnrollmentWizardService
{
    Task<EnrollmentPrerequisitesDto> GetPrerequisitesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<GeneratedRegistrationNumberDto> GenerateRegistrationNumberAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentStudentSearchResultDto>> SearchStudentsAsync(
        Guid schoolId,
        string search,
        CancellationToken cancellationToken = default);

    Task<EnrollmentStructureOptionsDto> GetStructureOptionsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<ClassCapacityDto> GetClassCapacityAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentFeeSummaryDto> CalculateFeesAsync(
        Guid schoolId,
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
}
