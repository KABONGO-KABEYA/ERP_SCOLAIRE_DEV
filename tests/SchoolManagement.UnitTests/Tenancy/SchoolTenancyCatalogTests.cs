namespace SchoolManagement.UnitTests.Tenancy;

using FluentAssertions;
using SchoolManagement.Application.Common.Tenancy;
using Xunit;

public sealed class SchoolTenancyCatalogTests
{
    [Fact]
    public void Indirect_entities_are_documented()
    {
        SchoolTenancyCatalog.IndirectOwnershipChains.Should().ContainKey(nameof(Domain.Entities.Grades.Evaluation));
        SchoolTenancyCatalog.IndirectOwnershipChains.Should().ContainKey(nameof(Domain.Entities.Students.Enrollment));
        SchoolTenancyCatalog.IndirectOwnershipChains.Should().ContainKey(nameof(Domain.Entities.Deliberation.StudentRemedialCourse));
    }

    [Fact]
    public void Every_indirect_entity_in_catalog_has_expected_chain_syntax()
    {
        foreach (var (_, chain) in SchoolTenancyCatalog.IndirectOwnershipChains)
        {
            chain.Should().Contain("SchoolId");
        }
    }

    [Fact]
    public void Global_entities_exclude_tenant_scoped_types()
    {
        SchoolTenancyCatalog.IsGlobalEntity(typeof(Domain.Entities.Security.Permission)).Should().BeTrue();
        SchoolTenancyCatalog.IsGlobalEntity(typeof(Domain.Entities.Students.Student)).Should().BeFalse();
    }
}
