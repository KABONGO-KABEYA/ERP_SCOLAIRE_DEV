using SchoolManagement.Application.Parent;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class ParentApiSchoolContextTests
{
    [Fact]
    public void EnsureResourceSchool_throws_when_mismatch()
    {
        var schoolA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var schoolB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Throws<UnauthorizedAccessException>(
            () => ParentApiSchoolContext.EnsureResourceSchool(schoolB, schoolA));
    }

    [Fact]
    public void EnsureResourceSchool_ok_when_match()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        ParentApiSchoolContext.EnsureResourceSchool(id, id);
    }
}
