using SchoolManagement.Application.Parent;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class ParentChildAccessTests
{
    private static readonly Guid SchoolA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SchoolB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Enfant_lie_meme_ecole_autorise()
    {
        ParentApiSchoolContext.EnsureChildAccess(
            hasGuardianLink: true,
            studentSchoolId: SchoolA,
            schoolId: SchoolA);
    }

    [Fact]
    public void Enfant_non_lie_refuse()
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            ParentApiSchoolContext.EnsureChildAccess(
                hasGuardianLink: false,
                studentSchoolId: SchoolA,
                schoolId: SchoolA));
        Assert.Contains("non autorisé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enfant_mauvais_etablissement_refuse()
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            ParentApiSchoolContext.EnsureChildAccess(
                hasGuardianLink: true,
                studentSchoolId: SchoolB,
                schoolId: SchoolA));
        Assert.Contains("hors contexte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eleve_introuvable_refuse()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            ParentApiSchoolContext.EnsureChildAccess(
                hasGuardianLink: true,
                studentSchoolId: null,
                schoolId: SchoolA));
    }
}
