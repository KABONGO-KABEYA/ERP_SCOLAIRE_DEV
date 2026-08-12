using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Common.Storage;
using SchoolManagement.Application.Configuration.FileStorage;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Services;
using Xunit;

namespace SchoolManagement.UnitTests.EnrollmentWizard;

public sealed class StudentDossierStorageP3Tests : IDisposable
{
    private readonly string _root;
    private readonly StudentDossierStorageService _storage;

    public StudentDossierStorageP3Tests()
    {
        _root = Path.Combine(Path.GetTempPath(), "erp_p3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var configDir = Path.Combine(_root, "cfg");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(
            Path.Combine(configDir, FileStorageConfigurationManager.FileName),
            $"RACINE={_root}{Environment.NewLine}");
        var mgr = new FileStorageConfigurationManager(configDir);
        _storage = new StudentDossierStorageService(mgr, NullLogger<StudentDossierStorageService>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public async Task T01_StoreDraft_WritesUnderTempDraftId()
    {
        var draftId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        await using var stream = new MemoryStream("photo-a"u8.ToArray());
        var saved = await _storage.SaveDraftFileAsync(
            draftId, schoolId, Guid.NewGuid(), "Photo", "PHOTO.jpg", stream);

        saved.StoragePath.Should().Be($"temp/{draftId:D}/PHOTO.jpg");
        File.Exists(Path.Combine(_root, "temp", draftId.ToString("D"), "PHOTO.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task T02_TwoDrafts_DoNotMixFiles()
    {
        var schoolId = Guid.NewGuid();
        var draftA = Guid.NewGuid();
        var draftB = Guid.NewGuid();
        await using (var s = new MemoryStream("A"u8.ToArray()))
        {
            await _storage.SaveDraftFileAsync(draftA, schoolId, null, "Photo", "PHOTO.jpg", s);
        }

        await using (var s = new MemoryStream("B"u8.ToArray()))
        {
            await _storage.SaveDraftFileAsync(draftB, schoolId, null, "Photo", "PHOTO.jpg", s);
        }

        File.ReadAllText(Path.Combine(_root, "temp", draftA.ToString("D"), "PHOTO.jpg")).Should().Be("A");
        File.ReadAllText(Path.Combine(_root, "temp", draftB.ToString("D"), "PHOTO.jpg")).Should().Be("B");
    }

    [Fact]
    public async Task T03_KabeyaLegacyFolder_IsNeverUsedForNewWrite()
    {
        var year = "2026-2027";
        var kabeya = Path.Combine(_root, year, "KABEYA_GLORIA_ELV_2026_00005");
        Directory.CreateDirectory(kabeya);
        await File.WriteAllTextAsync(Path.Combine(kabeya, "PHOTO.jpg"), "KABEYA");

        var draftId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        await using var stream = new MemoryStream("NDAYA"u8.ToArray());
        var saved = await _storage.SaveDraftFileAsync(
            draftId, schoolId, null, "Photo", "PHOTO.jpg", stream);

        saved.StoragePath.Should().StartWith($"temp/{draftId:D}/");
        File.ReadAllText(Path.Combine(kabeya, "PHOTO.jpg")).Should().Be("KABEYA");
        File.ReadAllText(Path.Combine(_root, "temp", draftId.ToString("D"), "PHOTO.jpg")).Should().Be("NDAYA");
    }

    [Fact]
    public async Task T06_PromoteDraft_MovesToStudentsStudentId()
    {
        var draftId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await using (var s = new MemoryStream("NDAYA"u8.ToArray()))
        {
            await _storage.SaveDraftFileAsync(draftId, schoolId, null, "Photo", "PHOTO.jpg", s);
        }

        var result = await _storage.PromoteDraftToStudentAsync(
            draftId, schoolId, studentId, "2026-2027");

        result.Succeeded.Should().BeTrue();
        var final = Path.Combine(_root, "2026-2027", "students", studentId.ToString("D"), "PHOTO.jpg");
        File.Exists(final).Should().BeTrue();
        File.ReadAllText(final).Should().Be("NDAYA");
        Directory.Exists(Path.Combine(_root, "temp", draftId.ToString("D"))).Should().BeFalse();
    }

    [Fact]
    public async Task T05_RollbackSimulation_KeepsTempWhenNotPromoted()
    {
        var draftId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        await using (var s = new MemoryStream("KEEP"u8.ToArray()))
        {
            await _storage.SaveDraftFileAsync(draftId, schoolId, null, "Photo", "PHOTO.jpg", s);
        }

        // Simulate SQL rollback: no PromoteDraft call.
        File.Exists(Path.Combine(_root, "temp", draftId.ToString("D"), "PHOTO.jpg")).Should().BeTrue();
        Directory.Exists(Path.Combine(_root, "2026-2027", "students")).Should().BeFalse();
    }

    [Fact]
    public void T09_OtherSchool_CannotAccessDraft()
    {
        var draftId = Guid.NewGuid();
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        _storage.EnsureDraft(draftId, schoolA, Guid.NewGuid());

        var act = () => _storage.AssertDraftAccess(draftId, schoolB, null);
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task T07_PromotionFailure_KeepsTemp_MarksPending_NotPurged()
    {
        var draftId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await using (var s = new MemoryStream("KEEP"u8.ToArray()))
        {
            await _storage.SaveDraftFileAsync(draftId, schoolId, null, "Photo", "PHOTO.jpg", s);
        }

        // Bloque la création du dossier students/{StudentId} (fichier à la place du répertoire).
        var studentsRoot = Path.Combine(_root, "2026-2027", "students");
        Directory.CreateDirectory(studentsRoot);
        await File.WriteAllTextAsync(Path.Combine(studentsRoot, studentId.ToString("D")), "BLOCK");

        var result = await _storage.PromoteDraftToStudentAsync(
            draftId, schoolId, studentId, "2026-2027");

        result.Succeeded.Should().BeFalse();
        File.Exists(Path.Combine(_root, "temp", draftId.ToString("D"), "PHOTO.jpg")).Should().BeTrue();

        var metaPath = Path.Combine(_root, "temp", draftId.ToString("D"), StudentDossierPathHelper.DraftMetaFileName);
        var meta = StudentDossierPathHelper.DeserializeDraftMeta(File.ReadAllText(metaPath))!;
        meta.PendingPromotionStudentId.Should().Be(studentId);
        meta.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddDays(1));

        // Même expiré artificiellement : pending ne doit pas être purgé.
        meta.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        File.WriteAllText(metaPath, StudentDossierPathHelper.SerializeDraftMeta(meta));
        _storage.PurgeExpiredDrafts(DateTime.UtcNow).Should().Be(0);
        File.Exists(Path.Combine(_root, "temp", draftId.ToString("D"), "PHOTO.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task T10_MultiStudents_GetIndependentFolders()
    {
        var schoolId = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var studentC = Guid.NewGuid();

        foreach (var (draft, student, content) in new[]
                 {
                     (Guid.NewGuid(), studentA, "A"),
                     (Guid.NewGuid(), studentB, "B"),
                     (Guid.NewGuid(), studentC, "C")
                 })
        {
            await using var s = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await _storage.SaveDraftFileAsync(draft, schoolId, null, "Photo", "PHOTO.jpg", s);
            var promo = await _storage.PromoteDraftToStudentAsync(draft, schoolId, student, "2026-2027");
            promo.Succeeded.Should().BeTrue();
            File.ReadAllText(Path.Combine(_root, "2026-2027", "students", student.ToString("D"), "PHOTO.jpg"))
                .Should().Be(content);
        }

        Directory.GetDirectories(Path.Combine(_root, "2026-2027", "students")).Should().HaveCount(3);
    }

    [Fact]
    public async Task T08_ExpiredDraft_IsPurged_DefinitiveUntouched()
    {
        var draftId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();
        await using (var s = new MemoryStream("X"u8.ToArray()))
        {
            await _storage.SaveDraftFileAsync(draftId, schoolId, null, "Photo", "PHOTO.jpg", s);
        }

        var studentId = Guid.NewGuid();
        var definitive = Path.Combine(_root, "2026-2027", "students", studentId.ToString("D"));
        Directory.CreateDirectory(definitive);
        await File.WriteAllTextAsync(Path.Combine(definitive, "PHOTO.jpg"), "KEEP");

        var metaPath = Path.Combine(_root, "temp", draftId.ToString("D"), StudentDossierPathHelper.DraftMetaFileName);
        var meta = StudentDossierPathHelper.DeserializeDraftMeta(File.ReadAllText(metaPath))!;
        meta.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        File.WriteAllText(metaPath, StudentDossierPathHelper.SerializeDraftMeta(meta));

        var purged = _storage.PurgeExpiredDrafts(DateTime.UtcNow);
        purged.Should().BeGreaterThan(0);
        Directory.Exists(Path.Combine(_root, "temp", draftId.ToString("D"))).Should().BeFalse();
        File.ReadAllText(Path.Combine(definitive, "PHOTO.jpg")).Should().Be("KEEP");
    }
}

