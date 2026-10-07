using FluentAssertions;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.EnrollmentWizard.Services;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests.EnrollmentWizard;

public sealed class GuardianIdentityUpdateTests
{
    [Fact]
    public void ApplyGuardianIdentityFields_updates_name_phone_email_and_profession()
    {
        var guardian = new Guardian
        {
            Id = Guid.NewGuid(),
            FirstName = "Jean",
            LastName = "Mbala",
            Phone = "0811111111",
            Email = "old@test.cd",
            Profession = "Chauffeur",
            Gender = Gender.Masculin
        };

        var input = new GuardianInputDto(
            "Paul",
            "Kabila",
            "0822222222",
            "new@test.cd",
            null,
            "Commerçant",
            "Marché central",
            "Responsable",
            true,
            true,
            Gender.Masculin,
            true,
            guardian.Id);

        EnrollmentWizardService.ApplyGuardianIdentityFields(guardian, input);

        guardian.FirstName.Should().Be("Paul");
        guardian.LastName.Should().Be("Kabila");
        guardian.Phone.Should().Be("0822222222");
        guardian.Email.Should().Be("new@test.cd");
        guardian.Profession.Should().Be("Commerçant — Marché central");
        guardian.Gender.Should().Be(Gender.Masculin);
    }
}
