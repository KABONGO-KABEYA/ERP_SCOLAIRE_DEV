using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Documents;

public enum ParentNoticeStatus
{
    Draft = 1,
    Generated = 2
}

public enum ParentNoticePageLayout
{
    FullPage = 1,
    TwoPerPage = 2
}

public enum ParentNoticePageOrientation
{
    Portrait = 1,
    Landscape = 2
}

/// <summary>Modèle de publipostage et campagne d'avis adressée aux parents.</summary>
public sealed class ParentNotice : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid FeeTypeId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid? ClassRoomId { get; set; }
    public string TargetDescription { get; set; } = string.Empty;
    public string ContentRtfBase64 { get; set; } = string.Empty;
    public string SelectedInstallmentIdsJson { get; set; } = "[]";
    public bool IncludeSchoolHeader { get; set; } = true;
    public ParentNoticePageLayout PageLayout { get; set; } = ParentNoticePageLayout.FullPage;
    public ParentNoticePageOrientation PageOrientation { get; set; } = ParentNoticePageOrientation.Portrait;
    public string RecipientStudentIdsJson { get; set; } = "[]";
    public string GeneratedSnapshotJson { get; set; } = "[]";
    public int RecipientCount { get; set; }
    public ParentNoticeStatus Status { get; set; } = ParentNoticeStatus.Draft;
    public DateTime? GeneratedAtUtc { get; set; }
}
