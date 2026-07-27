using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.Application.StudentCards.DTOs;

public sealed record CardTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    decimal WidthMm,
    decimal HeightMm,
    CardTemplateOrientation Orientation,
    CardTemplateKind Kind,
    string? LayoutJsonFront,
    string? LayoutJsonBack,
    bool IsActive);

public sealed record SaveCardTemplateRequest(
    string Name,
    string? Description,
    decimal WidthMm,
    decimal HeightMm,
    CardTemplateOrientation Orientation,
    CardTemplateKind Kind,
    string? LayoutJsonFront,
    string? LayoutJsonBack,
    bool IsActive = true);

public sealed record CardSchoolSettingsDto(
    Guid Id,
    string CardNumberPrefix,
    int DefaultValidityMonths,
    bool KeepQrOnRenewal,
    int NextSequence);

public sealed record SaveCardSchoolSettingsRequest(
    string CardNumberPrefix,
    int DefaultValidityMonths,
    bool KeepQrOnRenewal);

public sealed record StudentCardListItemDto(
    Guid Id,
    Guid StudentId,
    string StudentFullName,
    string? StudentPhotoPath,
    string? ClassName,
    string CardNumber,
    StudentCardStatus Status,
    DateTime? PrintedAt,
    DateTime? ExpiresAt,
    int PrintCount,
    int Version);

public sealed record StudentCardDetailDto(
    Guid Id,
    Guid StudentId,
    string StudentFullName,
    string? StudentPhotoPath,
    string StudentLastName,
    string StudentFirstName,
    string? StudentMiddleName,
    string RegistrationNumber,
    string GenderLabel,
    string DateOfBirth,
    string? ClassName,
    string? StudyOption,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid TemplateId,
    string TemplateName,
    string CardNumber,
    string QrToken,
    string QrPayload,
    DateTime IssuedAt,
    DateTime? PrintedAt,
    DateTime? ExpiresAt,
    StudentCardStatus Status,
    string? DeactivationReason,
    int PrintCount,
    int Version,
    Guid? ReplacesCardId,
    IReadOnlyList<StudentCardHistoryDto> Histories,
    IReadOnlyList<StudentCardPrintLogDto> PrintLogs);

public sealed record StudentCardHistoryDto(
    Guid Id,
    StudentCardHistoryAction Action,
    Guid? UserId,
    DateTime OccurredAt,
    string? OldValue,
    string? NewValue,
    string? Notes);

public sealed record StudentCardPrintLogDto(
    Guid Id,
    DateTime PrintedAt,
    Guid? PrintedBy,
    string? Reason,
    bool IsReprint);

public sealed record StudentCardDashboardDto(
    int ActiveCount,
    int ExpiredCount,
    int LostCount,
    int StolenCount,
    int ToRenewCount,
    IReadOnlyList<StudentCardListItemDto> RecentPrints);

public sealed record StudentCardSearchRequest(
    Guid? AcademicYearId = null,
    Guid? ClassRoomId = null,
    Guid? SectionId = null,
    StudentCardStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);

public sealed record CreateStudentCardRequest(
    Guid StudentId,
    Guid AcademicYearId,
    Guid TemplateId,
    DateTime? ExpiresAt = null,
    bool ActivateImmediately = true);

public sealed record BulkCreateStudentCardsRequest(
    Guid AcademicYearId,
    Guid TemplateId,
    Guid? ClassRoomId = null,
    Guid? SectionId = null,
    bool EntireSchool = false,
    DateTime? ExpiresAt = null,
    bool ActivateImmediately = true,
    bool SkipExistingActive = true);

public sealed record BulkCreateStudentCardsResult(
    int TargetStudentCount,
    int CreatedCount,
    int SkippedCount,
    IReadOnlyList<Guid> CreatedCardIds,
    string Summary);

public sealed record UpdateStudentCardRequest(
    Guid TemplateId,
    DateTime? ExpiresAt,
    string? Notes = null);

public sealed record PrintStudentCardsRequest(
    IReadOnlyList<Guid>? CardIds = null,
    Guid? ClassRoomId = null,
    Guid? AcademicYearId = null,
    bool EntireSchool = false,
    string? Reason = null);

public sealed record PrintStudentCardsResult(
    int PrintedCount,
    IReadOnlyList<Guid> CardIds);

public sealed record ReprintStudentCardRequest(string? Reason);

public sealed record RenewStudentCardRequest(
    Guid? TemplateId = null,
    DateTime? ExpiresAt = null,
    bool? KeepQrToken = null);

public sealed record DeclareCardIncidentRequest(string? Reason);

public sealed record DeactivateStudentCardRequest(string Reason);

public sealed record ResolveCardByQrRequest(string QrPayloadOrToken);

public sealed record ResolvedStudentCardDto(
    Guid CardId,
    string CardNumber,
    Guid StudentId,
    Guid AcademicYearId,
    StudentCardStatus Status,
    DateTime? ExpiresAt,
    bool IsUsable);

public sealed record StudentCardPagedResult(
    IReadOnlyList<StudentCardListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public PagedResult<StudentCardListItemDto> ToPagedResult() => new()
    {
        Items = Items,
        Page = Page,
        PageSize = PageSize,
        TotalCount = TotalCount
    };
}
