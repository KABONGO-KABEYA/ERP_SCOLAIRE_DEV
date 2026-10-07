namespace SchoolManagement.Application.ControllerControl.Interfaces;

using SchoolManagement.Application.ControllerControl.DTOs;

public interface IControllerControlService
{
    Task<ControllerSetupDto> GetSetupAsync(Guid schoolId, Guid? academicYearId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ControllerInstallmentDto>> GetInstallmentsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<ControllerStudentSearchResultDto> SearchStudentsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid feeTypeId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ControllerCheckResultDto> CheckByQrAsync(
        Guid schoolId,
        ControllerCheckByQrRequest request,
        CancellationToken cancellationToken = default);

    Task<ControllerCheckResultDto> CheckByStudentAsync(
        Guid schoolId,
        ControllerCheckByStudentRequest request,
        CancellationToken cancellationToken = default);
}
