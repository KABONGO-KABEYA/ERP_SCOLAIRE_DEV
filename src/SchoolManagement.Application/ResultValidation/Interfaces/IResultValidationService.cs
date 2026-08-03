using SchoolManagement.Application.ResultValidation.DTOs;

namespace SchoolManagement.Application.ResultValidation.Interfaces;

public interface IResultValidationService
{
    Task<ResultValidationSheetDto> GetSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<ResultValidationReadinessDto> GetReadinessReportAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<ResultValidationSheetDto> ValidateAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultValidationSheetDto> CancelValidationAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultValidationSheetDto> LockAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultValidationSheetDto> UnlockAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Enregistre un événement « Calcul effectué » après persistance des PeriodResult.</summary>
    Task RecordCalculationAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>Refuse toute mutation de notes si la classe/sous-période est verrouillée.</summary>
    Task EnsureClassPeriodNotLockedAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);
}
