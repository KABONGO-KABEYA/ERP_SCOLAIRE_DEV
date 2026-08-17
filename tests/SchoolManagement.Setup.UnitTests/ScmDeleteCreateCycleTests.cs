using FluentAssertions;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

/// <summary>
/// Cycle SCM réel sur un service jetable (pas ErpScolaireApi, pas Production).
/// Prérequis : PowerShell administrateur.
/// Health 200 de l'API réelle reste couvert par ReinstallIntegrationTests.
/// </summary>
public sealed class ScmDeleteCreateCycleTests
{
    private const string TestServiceName = "ErpScolaireSetupScm1072Test";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_waits_until_absent_then_create_succeeds()
    {
        if (!IsAdministrator())
        {
            throw new InvalidOperationException(
                "Ce test SCM exige une session administrateur. " +
                "Exécutez scripts/_test-reinstall-locks.ps1 ou lancez dotnet test en administrateur.");
        }

        var logs = new List<string>();
        void Log(string m) => logs.Add(m);
        var binPath = Environment.ProcessPath
            ?? Path.Combine(Environment.SystemDirectory, "svchost.exe");

        try
        {
            await WindowsServiceLifecycle.DeleteServiceAndWaitAsync(
                TestServiceName, Log, CancellationToken.None, TimeSpan.FromSeconds(30));

            WindowsServiceLifecycle.ProbeRegistration(TestServiceName)
                .Should().Be(ServiceRegistrationState.Absent, "service absent = première installation");

            CreateDemandStartService(binPath);
            WindowsServiceLifecycle.ProbeRegistration(TestServiceName)
                .Should().Be(ServiceRegistrationState.Stopped, "service existant STOPPED");

            await WindowsServiceLifecycle.StopServiceAndWaitAsync(
                TestServiceName, Log, CancellationToken.None);
            await WindowsServiceLifecycle.DeleteServiceAndWaitAsync(
                TestServiceName, Log, CancellationToken.None);

            WindowsServiceLifecycle.ServiceExists(TestServiceName).Should().BeFalse(
                "après delete, le service doit avoir disparu du SCM (pas seulement marqué 1072)");

            CreateDemandStartService(binPath);
            WindowsServiceLifecycle.ServiceExists(TestServiceName).Should().BeTrue();

            logs.Should().Contain(l => l.Contains("[SERVICE] Service absent du registre/SCM.", StringComparison.Ordinal));
            logs.Should().Contain(l => l.Contains("via registre uniquement", StringComparison.Ordinal));
            logs.Should().Contain(l => l.Contains("PID Setup=", StringComparison.Ordinal));
            logs.Should().NotContain(l => l.Equals("[SERVICE] Service marqué pour suppression.", StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                await WindowsServiceLifecycle.DeleteServiceAndWaitAsync(
                    TestServiceName, Log, CancellationToken.None, TimeSpan.FromSeconds(30));
            }
            catch
            {
                // nettoyage best-effort
            }
        }
    }

    private static void CreateDemandStartService(string exe)
    {
        var args =
            $"create {TestServiceName} binPath= \"\\\"{exe}\\\"\" start= demand DisplayName= \"ERP Setup SCM 1072 Test\" obj= LocalSystem";
        var (code, output) = WindowsServiceLifecycle.RunSc(args);
        if (code == 0)
            return;

        if (WindowsServiceLifecycle.IsMarkedForDeleteError(code, output))
            throw new InvalidOperationException("Create immédiat 1072 — WaitUntilAbsent n'a pas suffi.\n" + output);

        throw new InvalidOperationException($"sc create {TestServiceName} → {code}\n{output}");
    }

    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
