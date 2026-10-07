using System.Text.Json;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.ParentNotices.DTOs;
using SchoolManagement.Application.ParentNotices.Interfaces;
using SchoolManagement.Domain.Entities.Documents;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Application.ParentNotices.Services;

public sealed class ParentNoticeService : IParentNoticeService
{
    private readonly IRepository<ParentNotice> _notices;
    private readonly IRepository<AcademicYear> _years;
    private readonly IRepository<FeeType> _feeTypes;
    private readonly IUnitOfWork _unitOfWork;

    public ParentNoticeService(
        IRepository<ParentNotice> notices,
        IRepository<AcademicYear> years,
        IRepository<FeeType> feeTypes,
        IUnitOfWork unitOfWork)
    {
        _notices = notices;
        _years = years;
        _feeTypes = feeTypes;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ParentNoticeDto>> ListAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var notices = await _notices.FindAsync(n => n.SchoolId == schoolId, cancellationToken);
        return await MapManyAsync(schoolId, notices.OrderByDescending(n => n.CreatedAt), false, cancellationToken);
    }

    public async Task<ParentNoticeDto> GetAsync(Guid schoolId, Guid id, CancellationToken cancellationToken = default)
    {
        var notice = await FindAsync(schoolId, id, cancellationToken);
        return (await MapManyAsync(schoolId, [notice], true, cancellationToken))[0];
    }

    public async Task<ParentNoticeDto> CreateAsync(Guid schoolId, SaveParentNoticeRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(schoolId, request, cancellationToken);
        var notice = new ParentNotice { SchoolId = schoolId };
        Apply(notice, request);
        await _notices.AddAsync(notice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(schoolId, notice.Id, cancellationToken);
    }

    public async Task<ParentNoticeDto> UpdateAsync(Guid schoolId, Guid id, SaveParentNoticeRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(schoolId, request, cancellationToken);
        var notice = await FindAsync(schoolId, id, cancellationToken);
        Apply(notice, request);
        notice.Status = ParentNoticeStatus.Draft;
        notice.GeneratedAtUtc = null;
        notice.GeneratedSnapshotJson = "[]";
        notice.UpdatedAt = DateTime.UtcNow;
        await _notices.UpdateAsync(notice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(schoolId, notice.Id, cancellationToken);
    }

    public async Task<ParentNoticeDto> MarkGeneratedAsync(Guid schoolId, Guid id, GenerateParentNoticeRequest request, CancellationToken cancellationToken = default)
    {
        var notice = await FindAsync(schoolId, id, cancellationToken);
        var ids = request.RecipientStudentIds.Distinct().ToArray();
        if (ids.Length == 0) throw new InvalidOperationException("Sélectionnez au moins un élève.");
        notice.RecipientStudentIdsJson = JsonSerializer.Serialize(ids);
        notice.GeneratedSnapshotJson = JsonSerializer.Serialize(request.Snapshots);
        notice.RecipientCount = ids.Length;
        notice.Status = ParentNoticeStatus.Generated;
        notice.GeneratedAtUtc = DateTime.UtcNow;
        notice.UpdatedAt = DateTime.UtcNow;
        await _notices.UpdateAsync(notice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(schoolId, notice.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid schoolId, Guid id, CancellationToken cancellationToken = default)
    {
        var notice = await FindAsync(schoolId, id, cancellationToken);
        await _notices.DeleteAsync(notice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ParentNotice> FindAsync(Guid schoolId, Guid id, CancellationToken cancellationToken) =>
        (await _notices.FindAsync(n => n.Id == id && n.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Avis aux parents introuvable.");

    private async Task ValidateAsync(Guid schoolId, SaveParentNoticeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new InvalidOperationException("Le titre est obligatoire.");
        if (string.IsNullOrWhiteSpace(request.ContentRtfBase64)) throw new InvalidOperationException("Le contenu de l'avis est obligatoire.");
        if (!(await _years.FindAsync(y => y.Id == request.AcademicYearId && y.SchoolId == schoolId, cancellationToken)).Any())
            throw new InvalidOperationException("Année scolaire invalide.");
        if (!(await _feeTypes.FindAsync(f => f.Id == request.FeeTypeId && f.SchoolId == schoolId, cancellationToken)).Any())
            throw new InvalidOperationException("Type de frais invalide.");
    }

    private static void Apply(ParentNotice notice, SaveParentNoticeRequest request)
    {
        var ids = request.RecipientStudentIds.Distinct().ToArray();
        notice.Title = request.Title.Trim();
        notice.Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim();
        notice.AcademicYearId = request.AcademicYearId;
        notice.FeeTypeId = request.FeeTypeId;
        notice.SectionId = request.SectionId;
        notice.ClassRoomId = request.ClassRoomId;
        notice.TargetDescription = request.TargetDescription.Trim();
        notice.ContentRtfBase64 = request.ContentRtfBase64;
        notice.SelectedInstallmentIdsJson = JsonSerializer.Serialize(request.SelectedInstallmentIds.Distinct());
        notice.IncludeSchoolHeader = request.IncludeSchoolHeader;
        notice.PageLayout = request.PageLayout;
        notice.PageOrientation = request.PageLayout == ParentNoticePageLayout.TwoPerPage
            ? ParentNoticePageOrientation.Portrait
            : request.PageOrientation;
        notice.RecipientStudentIdsJson = JsonSerializer.Serialize(ids);
        notice.RecipientCount = ids.Length;
    }

    private async Task<IReadOnlyList<ParentNoticeDto>> MapManyAsync(
        Guid schoolId, IEnumerable<ParentNotice> source, bool includeContent, CancellationToken cancellationToken)
    {
        var notices = source.ToList();
        var years = (await _years.FindAsync(y => y.SchoolId == schoolId, cancellationToken)).ToDictionary(y => y.Id);
        var fees = (await _feeTypes.FindAsync(f => f.SchoolId == schoolId, cancellationToken)).ToDictionary(f => f.Id);
        return notices.Select(n => new ParentNoticeDto(
            n.Id, n.Title, n.Subject, n.AcademicYearId,
            years.TryGetValue(n.AcademicYearId, out var year) ? year.Label : "—",
            n.FeeTypeId, fees.TryGetValue(n.FeeTypeId, out var fee) ? fee.Name : "—",
            n.SectionId, n.ClassRoomId, n.TargetDescription, includeContent ? n.ContentRtfBase64 : string.Empty,
            includeContent ? DeserializeIds(n.SelectedInstallmentIdsJson) : [], n.IncludeSchoolHeader, n.PageLayout, n.PageOrientation,
            includeContent ? DeserializeIds(n.RecipientStudentIdsJson) : [],
            includeContent ? DeserializeSnapshots(n.GeneratedSnapshotJson) : [], n.RecipientCount, n.Status,
            n.CreatedAt, n.UpdatedAt, n.GeneratedAtUtc)).ToList();
    }

    private static IReadOnlyList<Guid> DeserializeIds(string json)
    {
        try { return JsonSerializer.Deserialize<Guid[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static IReadOnlyList<ParentNoticeRecipientSnapshotDto> DeserializeSnapshots(string json)
    {
        try { return JsonSerializer.Deserialize<ParentNoticeRecipientSnapshotDto[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
