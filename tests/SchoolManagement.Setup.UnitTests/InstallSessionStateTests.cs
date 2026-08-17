using FluentAssertions;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

public sealed class InstallSessionStateTests
{
    [Fact]
    public void CanStartInstall_is_true_initially()
    {
        var session = new InstallSessionState();
        session.CanStartInstall.Should().BeTrue();
    }

    [Fact]
    public void MarkCompleted_prevents_reinstall()
    {
        var session = new InstallSessionState();
        session.MarkCompleted();
        session.CanStartInstall.Should().BeFalse();
        session.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void SetBusy_prevents_install_while_running()
    {
        var session = new InstallSessionState();
        session.SetBusy(true);
        session.CanStartInstall.Should().BeFalse();
        session.IsBusy.Should().BeTrue();
    }

    [Fact]
    public void PrimaryButtonLabel_shows_Fermer_after_completion_on_step_5()
    {
        var session = new InstallSessionState();
        session.PrimaryButtonLabel(5, isServer: true).Should().Be("Terminer");
        session.MarkCompleted();
        session.PrimaryButtonLabel(5, isServer: true).Should().Be("Fermer");
        session.PrimaryButtonLabel(4, isServer: true).Should().Be("Suivant");
    }
}
