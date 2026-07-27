namespace SchoolManagement.Application.Parent.Interfaces;

using SchoolManagement.Application.Parent.DTOs;

public interface IParentService
{
    Task<IReadOnlyList<ParentChildDto>> GetMyChildrenAsync(Guid guardianId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentPaymentDto>> GetChildPaymentsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<ParentPaymentSummaryDto> GetChildPaymentSummaryAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<ParentFeeSituationsResultDto> GetChildFeeSituationsAsync(
        Guid guardianId,
        Guid studentId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentBulletinSummaryDto>> GetChildBulletinsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<ParentGradesOverviewDto> GetChildGradesAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportChildBulletinPdfAsync(
        Guid guardianId,
        Guid studentId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentAttendanceDayDto>> GetChildAttendanceAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentCommunicationDto>> GetChildCommunicationsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string FileName, string MimeType)?> OpenChildPhotoAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportChildPaymentReceiptPdfAsync(
        Guid guardianId,
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);
}
