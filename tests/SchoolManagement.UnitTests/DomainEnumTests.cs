using FluentAssertions;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class DomainEnumTests
{
    [Fact]
    public void EducationCycle_Should_Contain_Primaire_And_Secondaire()
    {
        Enum.GetNames<EducationCycle>().Should().Contain(["Primaire", "Secondaire"]);
    }

    [Fact]
    public void Currency_Should_Support_CDF_And_USD()
    {
        Enum.GetNames<Currency>().Should().Contain(["CDF", "USD"]);
    }
}
