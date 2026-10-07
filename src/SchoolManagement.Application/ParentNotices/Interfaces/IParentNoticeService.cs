using SchoolManagement.Application.ParentNotices.DTOs;

namespace SchoolManagement.Application.ParentNotices.Interfaces;

public interface IParentNoticeService
{
    Task<IReadOnlyList<ParentNoticeDto>> ListAsync(Guid schoolId, CancellationToken cancellationToken = default);
    Task<ParentNoticeDto> GetAsync(Guid schoolId, Guid id, CancellationToken cancellationToken = default);
    Task<ParentNoticeDto> CreateAsync(Guid schoolId, SaveParentNoticeRequest request, CancellationToken cancellationToken = default);
    Task<ParentNoticeDto> UpdateAsync(Guid schoolId, Guid id, SaveParentNoticeRequest request, CancellationToken cancellationToken = default);
    Task<ParentNoticeDto> MarkGeneratedAsync(Guid schoolId, Guid id, GenerateParentNoticeRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid schoolId, Guid id, CancellationToken cancellationToken = default);
}
