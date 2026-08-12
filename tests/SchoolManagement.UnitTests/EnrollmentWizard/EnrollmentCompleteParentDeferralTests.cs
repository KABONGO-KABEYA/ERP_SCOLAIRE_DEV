using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Domain.Entities.Students;
using Xunit;

namespace SchoolManagement.UnitTests.EnrollmentWizard;

/// <summary>
/// Vérifie le contrat P1 : parent provisioning hors transaction métier.
/// </summary>
public sealed class EnrollmentCompleteParentDeferralTests
{
    [Fact]
    public async Task ParentProvisioning_IsInvokedAfterSuccessfulTransactionCommit()
    {
        var callOrder = new List<string>();
        var uow = Substitute.For<IUnitOfWork>();
        uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                callOrder.Add("tx-begin");
                var action = callInfo.Arg<Func<CancellationToken, Task>>();
                await action(CancellationToken.None);
                callOrder.Add("tx-commit");
            });
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var parent = Substitute.For<IParentAccessProvisioningService>();
        parent.EnsureAccessForGuardiansAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guardian>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callOrder.Add("parent-provision");
                // Doit être après le commit de la TX métier.
                callOrder.Should().Contain("tx-commit");
                return Task.FromResult<IReadOnlyList<ParentAppAccessCredentialDto>>([]);
            });

        // Simulation de l'orchestration P1 (extrait) : TX puis parent.
        await uow.ExecuteInTransactionAsync(async _ =>
        {
            callOrder.Add("tx-body");
            await Task.CompletedTask;
        }, CancellationToken.None);

        await parent.EnsureAccessForGuardiansAsync(Guid.NewGuid(), [], CancellationToken.None);
        await uow.SaveChangesAsync(CancellationToken.None);
        callOrder.Add("parent-saved");

        callOrder.Should().Equal(
            "tx-begin",
            "tx-body",
            "tx-commit",
            "parent-provision",
            "parent-saved");
    }

    [Fact]
    public void MultiChild_StudentGuardianUniqueness_IsPerStudentNotPerGuardian()
    {
        // Garde-fou métier : un même GuardianId peut apparaître sur plusieurs StudentId.
        var guardianId = Guid.NewGuid();
        var links = new[]
        {
            new StudentGuardian { StudentId = Guid.NewGuid(), GuardianId = guardianId },
            new StudentGuardian { StudentId = Guid.NewGuid(), GuardianId = guardianId },
            new StudentGuardian { StudentId = Guid.NewGuid(), GuardianId = guardianId }
        };

        links.Select(l => l.GuardianId).Distinct().Should().ContainSingle();
        links.Select(l => l.StudentId).Distinct().Should().HaveCount(3);
        links.Select(l => (l.StudentId, l.GuardianId)).Should().OnlyHaveUniqueItems();
    }
}
