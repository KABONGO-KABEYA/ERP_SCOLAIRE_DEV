using FluentAssertions;
using SchoolManagement.Application.Common.Storage;
using Xunit;

namespace SchoolManagement.UnitTests.EnrollmentWizard;

public sealed class StudentDossierPathHelperP3Tests
{
    [Fact]
    public void T01_DraftRelativeDirectory_IsUnderTemp()
    {
        var draftId = Guid.Parse("7f0d5e4e-1111-2222-3333-444444444444");
        var path = StudentDossierPathHelper.BuildDraftRelativeDirectory(draftId);
        path.Should().Be("temp/7f0d5e4e-1111-2222-3333-444444444444");
        StudentDossierPathHelper.IsTempDraftPath(path + "/PHOTO.jpg").Should().BeTrue();
    }

    [Fact]
    public void T03_FindExisting_MustNotBeUsedForFinalWrites_StudentIdPathIsIndependent()
    {
        var studentId = Guid.Parse("631CDB63-51CD-4AF0-9908-C4B8BE7F4FD8");
        var finalPath = StudentDossierPathHelper.BuildStudentIdRelativeFilePath(
            "2026-2027", studentId, "PHOTO.jpg");

        finalPath.Should().Be("2026-2027/students/631cdb63-51cd-4af0-9908-c4b8be7f4fd8/PHOTO.jpg");
        finalPath.Should().NotContain("KABEYA");
        finalPath.Should().NotContain("ELV_2026_00005");
        StudentDossierPathHelper.IsStudentIdStoragePath(finalPath).Should().BeTrue();
    }

    [Fact]
    public void T04_TwoStudentsSameName_GetDistinctFolders()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var pathA = StudentDossierPathHelper.BuildStudentIdRelativeDirectory("2026-2027", a);
        var pathB = StudentDossierPathHelper.BuildStudentIdRelativeDirectory("2026-2027", b);
        pathA.Should().NotBe(pathB);
        pathA.Should().Contain(a.ToString("D"));
        pathB.Should().Contain(b.ToString("D"));
    }

    [Fact]
    public void T02_TwoDrafts_AreIsolated()
    {
        var draftA = Guid.NewGuid();
        var draftB = Guid.NewGuid();
        StudentDossierPathHelper.BuildDraftRelativeDirectory(draftA)
            .Should().NotBe(StudentDossierPathHelper.BuildDraftRelativeDirectory(draftB));
    }

    [Fact]
    public void TryParseDraftId_FromTempPath()
    {
        var id = Guid.NewGuid();
        var ok = StudentDossierPathHelper.TryParseDraftIdFromPath(
            $"temp/{id:D}/PHOTO.jpg", out var parsed);
        ok.Should().BeTrue();
        parsed.Should().Be(id);
    }

    [Fact]
    public void LegacyKabeyaFolder_IsNotStudentIdPath()
    {
        const string legacy = "2026-2027/KABEYA_GLORIA_ELV_2026_00005/PHOTO.jpg";
        StudentDossierPathHelper.IsStudentIdStoragePath(legacy).Should().BeFalse();
        StudentDossierPathHelper.IsTempDraftPath(legacy).Should().BeFalse();
        StudentDossierPathHelper.IsServerStoragePath(legacy).Should().BeTrue();
    }
}
