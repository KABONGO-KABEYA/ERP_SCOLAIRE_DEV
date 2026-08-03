using SchoolManagement.Application.Mentions.DTOs;

namespace SchoolManagement.Application.Mentions.Interfaces;

public interface IResultMentionService
{
    Task<IReadOnlyList<ResultMentionDto>> GetAllAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<ResultMentionDto> CreateAsync(
        Guid schoolId,
        CreateResultMentionRequest request,
        CancellationToken cancellationToken = default);

    Task<ResultMentionDto> UpdateAsync(
        Guid schoolId,
        Guid id,
        UpdateResultMentionRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid schoolId,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Crée les mentions par défaut si aucune n'existe pour l'établissement.</summary>
    Task EnsureDefaultsAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);
}
