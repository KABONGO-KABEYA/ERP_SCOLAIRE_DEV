using FluentAssertions;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Schools.Catalog;
using Xunit;

namespace SchoolManagement.UnitTests;

public class RdcCurriculumCatalogTests
{
    [Fact]
    public void Maternelle_profile_has_expected_courses()
    {
        var courses = RdcCurriculumCatalog.GetCoursesForTemplate("MAT-1");

        courses.Should().HaveCount(10);
        courses.Should().Contain(c => c.Name == "Langage" && c.BranchCode == null);
        courses.Should().Contain(c => c.Name == "Chant" && c.BranchCode == "EDU-ART");
        courses.Should().Contain(c => c.Name == "Éducation morale" && c.BranchCode == null);
    }

    [Fact]
    public void Primaire_profile_has_branches_and_standalone_education_physique()
    {
        var courses = RdcCurriculumCatalog.GetCoursesForTemplate("PRI-3");

        courses.Should().HaveCount(29);
        courses.Should().Contain(c => c.Name == "Expression écrite" && c.BranchCode == "FR");
        courses.Should().Contain(c => c.Name == "Civisme" && c.BranchCode == "EDCIV");
        courses.Should().Contain(c => c.Name == "Éducation physique" && c.BranchCode == null);
        courses.Should().Contain(c => c.Name == "Histoire" && c.BranchCode == null);
        courses.Should().Contain(c => c.Name == "Géographie" && c.BranchCode == null);
        courses.Should().Contain(c => c.Name == "Religion" && c.BranchCode == null);
        courses.Where(c => c.Name == "Éducation physique").Should().OnlyContain(c => c.BranchCode == null);
    }

    [Theory]
    [InlineData("PRI-1", 27)]
    [InlineData("PRI-2", 27)]
    [InlineData("PRI-3", 29)]
    [InlineData("PRI-4", 29)]
    [InlineData("PRI-5", 30)]
    [InlineData("PRI-6", 30)]
    public void Primaire_levels_have_expected_course_counts(string templateCode, int expectedCount)
    {
        RdcCurriculumCatalog.GetCoursesForTemplate(templateCode).Should().HaveCount(expectedCount);
    }

    [Fact]
    public void Primaire_lower_levels_do_not_include_histoire()
    {
        RdcCurriculumCatalog.GetCoursesForTemplate("PRI-1")
            .Should()
            .NotContain(c => c.Name == "Histoire");
    }

    [Fact]
    public void Primaire_upper_levels_include_informatique()
    {
        RdcCurriculumCatalog.GetCoursesForTemplate("PRI-5")
            .Should()
            .Contain(c => c.Name == "Informatique");
    }

    [Fact]
    public void All_primaire_levels_share_distinct_course_codes()
    {
        RdcCurriculumCatalog.GetCoursesForTemplate("PRI-1")
            .Select(c => c.Code)
            .Should()
            .OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("MAT-1")]
    [InlineData("MAT-2")]
    [InlineData("MAT-3")]
    public void All_maternelle_levels_share_same_curriculum(string templateCode)
    {
        RdcCurriculumCatalog.GetCoursesForTemplate(templateCode)
            .Select(c => c.Code)
            .Should()
            .BeEquivalentTo(RdcCurriculumCatalog.GetCoursesForTemplate("MAT-1").Select(c => c.Code));
    }

    [Fact]
    public void All_distinct_course_codes_respect_database_limit()
    {
        foreach (var course in RdcCurriculumCatalog.GetAllDistinctCourses())
        {
            course.Code.Length.Should().BeLessOrEqualTo(
                CourseCodeConstraints.MaxCodeLength,
                because: $"code '{course.Code}' is too long");
        }
    }

    [Fact]
    public void All_branch_codes_respect_database_limit()
    {
        foreach (var branch in RdcCurriculumCatalog.GetBranches())
        {
            branch.Code.Length.Should().BeLessOrEqualTo(
                CourseCodeConstraints.MaxCodeLength,
                because: $"code '{branch.Code}' is too long");
        }
    }

    [Fact]
    public void Humanities_commercial_option_uses_full_branch_slug_in_course_code()
    {
        var courses = RdcCurriculumCatalog.GetCoursesForTemplate("HUM-COMMERCIALE-COMMERCIALEETGESTION");

        courses.Should().Contain(c =>
            c.Code == "COMMERCIALEETGESTION-COMPTA"
            && c.Name == "Comptabilité");
    }
}
