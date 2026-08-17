using System.ServiceProcess;
using FluentAssertions;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

public sealed class WindowsServiceLifecycleTests
{
    [Theory]
    [InlineData(ServiceControllerStatus.Running, true)]
    [InlineData(ServiceControllerStatus.StartPending, true)]
    [InlineData(ServiceControllerStatus.StopPending, true)]
    [InlineData(ServiceControllerStatus.Paused, true)]
    [InlineData(ServiceControllerStatus.Stopped, false)]
    public void ShouldStopService_reflects_active_states(ServiceControllerStatus status, bool expected)
    {
        WindowsServiceLifecycle.ShouldStopService(status).Should().Be(expected);
    }

    [Fact]
    public void Api_and_desktop_process_names_match_installer_constants()
    {
        WindowsServiceLifecycle.ApiProcessName.Should().Be("SchoolManagement.API");
        WindowsServiceLifecycle.DesktopProcessName.Should().Be("SchoolManagement.Desktop");
        InstallerEngine.ServiceName.Should().Be("ErpScolaireApi");
    }

    [Theory]
    [InlineData(1072, "", true)]
    [InlineData(0, "1072", true)]
    [InlineData(1, "Le service spécifié a été marqué pour suppression.", true)]
    [InlineData(1, "The specified service has been marked for deletion.", true)]
    [InlineData(0, "CreateService SUCCESS", false)]
    [InlineData(1060, "The specified service does not exist", false)]
    public void IsMarkedForDeleteError_detects_1072(int exitCode, string output, bool expected)
    {
        WindowsServiceLifecycle.IsMarkedForDeleteError(exitCode, output).Should().Be(expected);
    }

    [Fact]
    public void ProbeRegistration_absent_for_unknown_service()
    {
        WindowsServiceLifecycle.ProbeRegistration("ErpScolaireApi_DefinitelyNotInstalled_1072")
            .Should().Be(ServiceRegistrationState.Absent);
        WindowsServiceLifecycle.ServiceRegistryKeyExists("ErpScolaireApi_DefinitelyNotInstalled_1072")
            .Should().BeFalse();
        WindowsServiceLifecycle.ServiceExists("ErpScolaireApi_DefinitelyNotInstalled_1072")
            .Should().BeFalse();
    }

    [Fact]
    public async Task WaitUntilServiceAbsent_default_probe_uses_registry_not_GetServices()
    {
        WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = true;
        try
        {
            await WindowsServiceLifecycle.WaitUntilServiceAbsentAsync(
                "ErpScolaireApi_DefinitelyNotInstalled_1072",
                _ => { },
                CancellationToken.None,
                timeout: TimeSpan.FromSeconds(2));
        }
        finally
        {
            WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = false;
        }
    }

    [Fact]
    public void OpenService_is_blocked_during_delete_wait()
    {
        WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = true;
        try
        {
            var act = () => WindowsServiceLifecycle.OpenService("ErpScolaireApi");
            act.Should().Throw<InvalidOperationException>().WithMessage("*interdit pendant l'attente*");
        }
        finally
        {
            WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = false;
        }
    }

    [Fact]
    public async Task WaitUntilServiceAbsent_returns_when_probe_reports_gone()
    {
        var n = 0;
        await WindowsServiceLifecycle.WaitUntilServiceAbsentAsync(
            "Any",
            _ => { },
            CancellationToken.None,
            timeout: TimeSpan.FromSeconds(5),
            exists: _ => Interlocked.Increment(ref n) < 3);

        n.Should().Be(3);
    }

    [Fact]
    public void FormatDeleteServiceLog_exit_0_means_marked_by_this_call()
    {
        var log = WindowsServiceLifecycle.FormatDeleteServiceLog(0, "[SC] DeleteService SUCCESS");
        log.Should().Contain("DeleteService OK : service marqué maintenant.");
        WindowsServiceLifecycle.FormatDeleteMarkedDetail(0, "")
            .Should().Be("[SERVICE] Service marqué pour suppression par cet appel.");
        log.Should().NotContain("déjà marqué");
    }

    [Fact]
    public void FormatDeleteServiceLog_exit_1072_means_already_marked()
    {
        var log = WindowsServiceLifecycle.FormatDeleteServiceLog(1072, "Le service spécifié a été marqué pour suppression.");
        log.Should().Contain("DeleteService = 1072 : service déjà marqué pour suppression.");
        WindowsServiceLifecycle.FormatDeleteMarkedDetail(1072, "")
            .Should().Be("[SERVICE] Service déjà marqué pour suppression avant cet appel (1072).");
        log.Should().NotContain("marqué maintenant");
    }

    [Fact]
    public void FormatDeleteTimeout_does_not_claim_handle_certainty()
    {
        var msg = WindowsServiceLifecycle.FormatDeleteTimeoutMessage(
            "ErpScolaireApi", TimeSpan.FromSeconds(45), alreadyMarkedBeforeThisCall: true);
        msg.Should().Contain("encore présent dans le registre");
        msg.Should().Contain("n'ouvre aucun handle SCM");
        msg.Should().Contain("peut appartenir");
        msg.Should().Contain("probablement d'un handle externe");
        msg.Should().NotContain("un handle est encore ouvert");
    }

    [Fact]
    public async Task WaitUntilServiceAbsent_throws_if_still_present()
    {
        var act = () => WindowsServiceLifecycle.WaitUntilServiceAbsentAsync(
            "Stuck",
            _ => { },
            CancellationToken.None,
            timeout: TimeSpan.FromMilliseconds(400),
            exists: _ => true,
            alreadyMarkedBeforeThisCall: true);

        var ex = await act.Should().ThrowAsync<System.TimeoutException>();
        ex.Which.Message.Should().Contain("n'ouvre aucun handle SCM");
        ex.Which.Message.Should().Contain("handle externe");
    }

    [Fact]
    public void ServiceRegistrationState_distinguishes_absent_stopped_running()
    {
        Enum.GetNames<ServiceRegistrationState>().Should().Contain(
            new[] { "Absent", "Stopped", "Running", "Busy" });
    }
}
