using SchoolManagement.Domain.Entities.Documents;

namespace SchoolManagement.Application.ParentNotices.DTOs;

public sealed record ParentNoticeDto(
    Guid Id,
    string Title,
    string? Subject,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid FeeTypeId,
    string FeeTypeName,
    Guid? SectionId,
    Guid? ClassRoomId,
    string TargetDescription,
    string ContentRtfBase64,
    IReadOnlyList<Guid> SelectedInstallmentIds,
    bool IncludeSchoolHeader,
    ParentNoticePageLayout PageLayout,
    ParentNoticePageOrientation PageOrientation,
    IReadOnlyList<Guid> RecipientStudentIds,
    IReadOnlyList<ParentNoticeRecipientSnapshotDto> GeneratedSnapshots,
    int RecipientCount,
    ParentNoticeStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? GeneratedAtUtc);

public sealed record ParentNoticeRecipientSnapshotDto(
    Guid StudentId,
    string StudentName,
    IReadOnlyDictionary<string, string> Values);

public sealed record SaveParentNoticeRequest(
    string Title,
    string? Subject,
    Guid AcademicYearId,
    Guid FeeTypeId,
    Guid? SectionId,
    Guid? ClassRoomId,
    string TargetDescription,
    string ContentRtfBase64,
    IReadOnlyList<Guid> SelectedInstallmentIds,
    bool IncludeSchoolHeader,
    ParentNoticePageLayout PageLayout,
    ParentNoticePageOrientation PageOrientation,
    IReadOnlyList<Guid> RecipientStudentIds);

public sealed record GenerateParentNoticeRequest(
    IReadOnlyList<Guid> RecipientStudentIds,
    IReadOnlyList<ParentNoticeRecipientSnapshotDto> Snapshots);
