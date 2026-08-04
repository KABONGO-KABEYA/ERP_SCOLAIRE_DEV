namespace SchoolManagement.Application.Parent.Interfaces;

using SchoolManagement.Application.Parent.DTOs;

public interface IParentService
{
    Task<IReadOnlyList<ParentChildDto>> GetMyChildrenAsync(
        Guid schoolId,
        Guid guardianId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentPaymentDto>> GetChildPaymentsAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<ParentPaymentSummaryDto> GetChildPaymentSummaryAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<ParentFeeSituationsResultDto> GetChildFeeSituationsAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentBulletinSummaryDto>> GetChildBulletinsAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<ParentGradesOverviewDto> GetChildGradesAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportChildBulletinPdfAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentAttendanceDayDto>> GetChildAttendanceAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentCommunicationDto>> GetChildCommunicationsAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string FileName, string MimeType)?> OpenChildPhotoAsync(
        Guid schoolId,
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportChildPaymentReceiptPdfAsync(
        Guid schoolId,
        Guid guardianId,
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);
}
