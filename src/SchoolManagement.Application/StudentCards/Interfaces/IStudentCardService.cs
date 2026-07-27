using SchoolManagement.Application.StudentCards.DTOs;

namespace SchoolManagement.Application.StudentCards.Interfaces;

/// <summary>Module métier cartes élèves — cycle de vie, QR, historique, impressions.</summary>
public interface IStudentCardService
{
    Task<StudentCardDashboardDto> GetDashboardAsync(Guid schoolId, Guid? academicYearId, CancellationToken cancellationToken = default);

    Task<StudentCardPagedResult> SearchAsync(Guid schoolId, StudentCardSearchRequest request, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> GetByIdAsync(Guid schoolId, Guid cardId, CancellationToken cancellationToken = default);

    Task<ResolvedStudentCardDto?> ResolveByQrAsync(Guid schoolId, ResolveCardByQrRequest request, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> CreateAsync(Guid schoolId, CreateStudentCardRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<BulkCreateStudentCardsResult> BulkCreateAsync(Guid schoolId, BulkCreateStudentCardsRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> UpdateAsync(Guid schoolId, Guid cardId, UpdateStudentCardRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid schoolId, Guid cardId, Guid userId, CancellationToken cancellationToken = default);

    Task<PrintStudentCardsResult> PrintAsync(Guid schoolId, PrintStudentCardsRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> ReprintAsync(Guid schoolId, Guid cardId, ReprintStudentCardRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> RenewAsync(Guid schoolId, Guid cardId, RenewStudentCardRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> DeclareLostAsync(Guid schoolId, Guid cardId, DeclareCardIncidentRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> DeclareStolenAsync(Guid schoolId, Guid cardId, DeclareCardIncidentRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<StudentCardDetailDto> DeactivateAsync(Guid schoolId, Guid cardId, DeactivateStudentCardRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CardTemplateDto>> ListTemplatesAsync(Guid schoolId, bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<CardTemplateDto> GetTemplateAsync(Guid schoolId, Guid templateId, CancellationToken cancellationToken = default);

    Task<CardTemplateDto> CreateTemplateAsync(Guid schoolId, SaveCardTemplateRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task<CardTemplateDto> UpdateTemplateAsync(Guid schoolId, Guid templateId, SaveCardTemplateRequest request, Guid userId, CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(Guid schoolId, Guid templateId, CancellationToken cancellationToken = default);

    Task<CardTemplateDto> PreviewTemplateAsync(Guid schoolId, SaveCardTemplateRequest request, CancellationToken cancellationToken = default);

    Task<CardSchoolSettingsDto> GetSettingsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<CardSchoolSettingsDto> SaveSettingsAsync(Guid schoolId, SaveCardSchoolSettingsRequest request, Guid userId, CancellationToken cancellationToken = default);
}
