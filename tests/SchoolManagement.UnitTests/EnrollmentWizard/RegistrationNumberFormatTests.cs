using FluentAssertions;
using SchoolManagement.Application.EnrollmentWizard;
using Xunit;

namespace SchoolManagement.UnitTests.EnrollmentWizard;

public sealed class RegistrationNumberFormatTests
{
    [Theory]
    [InlineData(2026, 1, "ELV-2026-00001")]
    [InlineData(2026, 5, "ELV-2026-00005")]
    [InlineData(2027, 1, "ELV-2027-00001")]
    [InlineData(2026, 42, "ELV-2026-00042")]
    public void Format_ProducesExpectedMatricule(int year, int seq, string expected)
    {
        RegistrationNumberFormat.Format(year, seq).Should().Be(expected);
    }

    [Theory]
    [InlineData("ELV-2026-00005", 2026, 5)]
    [InlineData("ELV-2026-001", 2026, 1)]
    [InlineData("elv-2027-00012", 2027, 12)]
    public void TryParse_AcceptsKnownFormats(string input, int year, int seq)
    {
        RegistrationNumberFormat.TryParse(input, out var y, out var s).Should().BeTrue();
        y.Should().Be(year);
        s.Should().Be(seq);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MAT-2026-00001")]
    [InlineData("ELV-26-00001")]
    [InlineData("invalid")]
    public void TryParse_RejectsUnexpectedFormats(string? input)
    {
        RegistrationNumberFormat.TryParse(input, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void ComputeNextValue_SequentialAllocations()
    {
        var numbers = new List<string>();
        var next = RegistrationNumberFormat.ComputeNextValue(numbers, 2026);
        next.Should().Be(1);

        numbers.Add(RegistrationNumberFormat.Format(2026, next));
        next = RegistrationNumberFormat.ComputeNextValue(numbers, 2026);
        next.Should().Be(2);

        numbers.Add(RegistrationNumberFormat.Format(2026, next));
        next = RegistrationNumberFormat.ComputeNextValue(numbers, 2026);
        next.Should().Be(3);
    }

    [Fact]
    public void ComputeNextValue_DoesNotReuseSoftDeletedSequence()
    {
        // Soft-deleted ELV-2026-00005 doit bloquer la réutilisation de 00005.
        var existing = new[] { "ELV-2026-00001", "ELV-2026-00005" };
        RegistrationNumberFormat.ComputeNextValue(existing, 2026).Should().Be(6);
    }

    [Fact]
    public void ComputeNextValue_GapsFollowMaxNotCount()
    {
        var existing = new[] { "ELV-2026-00001", "ELV-2026-00002", "ELV-2026-00005" };
        // COUNT serait 4 ; la stratégie suit MAX+1.
        RegistrationNumberFormat.ComputeNextValue(existing, 2026).Should().Be(6);
    }

    [Fact]
    public void ComputeNextValue_IsPerSchoolYearIndependent()
    {
        // Même séquence pour deux années / établissements côté format.
        var schoolA = new[] { "ELV-2026-00003" };
        var schoolB = Array.Empty<string>();
        RegistrationNumberFormat.ComputeNextValue(schoolA, 2026).Should().Be(4);
        RegistrationNumberFormat.ComputeNextValue(schoolB, 2026).Should().Be(1);
        RegistrationNumberFormat.Format(2026, 1).Should().Be("ELV-2026-00001");
        RegistrationNumberFormat.Format(2026, 1).Should().Be(
            RegistrationNumberFormat.Format(2026, RegistrationNumberFormat.ComputeNextValue([], 2026)));
    }

    [Fact]
    public void ComputeNextValue_NewYearStartsAtOne()
    {
        var existing = new[] { "ELV-2026-00099", "ELV-2026-00100" };
        RegistrationNumberFormat.ComputeNextValue(existing, 2027).Should().Be(1);
        RegistrationNumberFormat.Format(2027, 1).Should().Be("ELV-2027-00001");
    }

    [Fact]
    public void ComputeNextValue_IgnoresUnexpectedFormats_AndUsesMaxOfParsable()
    {
        var existing = new[]
        {
            "ELV-2026-00005",
            "ELV-2026-001", // anomalie seed (3 digits) → seq 1
            "XYZ-999",
            "MATRICULE"
        };
        RegistrationNumberFormat.ComputeNextValue(existing, 2026).Should().Be(6);
    }

    [Fact]
    public void ComputeNextValue_MatchesCurrentDevDataSeedExpectation()
    {
        // Mirror Dev : 00002..00005 + ELV-2026-001 → NextValue = 6
        var existing = new[]
        {
            "ELV-2026-00002",
            "ELV-2026-00003",
            "ELV-2026-00004",
            "ELV-2026-00005",
            "ELV-2026-001"
        };
        RegistrationNumberFormat.ComputeNextValue(existing, 2026).Should().Be(6);
    }
}
