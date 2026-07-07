namespace SchoolManagement.Application.Parent.Interfaces;

using SchoolManagement.Application.Parent.DTOs;

public interface IParentService
{
    Task<IReadOnlyList<ParentChildDto>> GetMyChildrenAsync(Guid guardianId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentPaymentDto>> GetChildPaymentsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentBulletinSummaryDto>> GetChildBulletinsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default);
}
