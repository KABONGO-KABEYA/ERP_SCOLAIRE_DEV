using System.Net.Http;
using System.ServiceProcess;
using FluentAssertions;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

/// <summary>
/// Scénario réel de réinstallation, autonome vis-à-vis de Production.
///
/// Prérequis (documentés, non recréés par le test) :
/// - session administrateur (scripts/_test-reinstall-locks.ps1) ;
/// - installation existante sous C:\Program Files\ERP Scolaire\Api ;
/// - service Windows ErpScolaireApi déjà enregistré (ImagePath inchangé).
///
/// Le test ne recrée pas le service. Il retarget temporairement uniquement
/// ConnectionStrings__Default vers SchoolManagementRDC_SetupReinstallTest,
/// puis restaure l'Environment d'origine.
/// </summary>
public sealed class ReinstallIntegrationTests
{
    private const string InstallApiDir = @"C:\Program Files\ERP Scolaire\Api";
    private const string PreferredDll = "Asp.Versioning.Abstractions.dll";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReleaseServerPayloadLocks_replaces_locked_dll_and_restarts_service()
    {
        if (!IsRunningAsAdministrator())
        {
            throw new InvalidOperationException(
                "Ce test d'intégration exige une session administrateur. " +
                "Exécutez scripts/_test-reinstall-locks.ps1 (Run as administrator).");
        }

        if (!Directory.Exists(InstallApiDir))
            throw new InvalidOperationException($"Dossier API absent ({InstallApiDir}).");

        if (ServiceController.GetServices().All(s =>
                !s.ServiceName.Equals(InstallerEngine.ServiceName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Prérequis : service ErpScolaireApi déjà installé. Ce test ne le crée pas.");
        }

        var originalEnvironment = ReinstallTestSqlSupport.ReadServiceEnvironment();
        var originalConnection = ReinstallTestSqlSupport.ParseDefaultConnection(originalEnvironment);
        var masterCs = ReinstallTestSqlSupport.BuildMasterConnectionString(originalConnection);
        var testCs = ReinstallTestSqlSupport.BuildTestConnectionString(
            originalConnection, ReinstallTestSqlSupport.TestDatabaseName);
        var testEnvironment = ReinstallTestSqlSupport.WithTestCatalog(
            originalEnvironment, ReinstallTestSqlSupport.TestDatabaseName);

        originalConnection.InitialCatalog.Should().NotBeEquivalentTo(
            ReinstallTestSqlSupport.TestDatabaseName);
        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(testCs).InitialCatalog
            .Should().Be(ReinstallTestSqlSupport.TestDatabaseName);
        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(testCs).InitialCatalog
            .Should().NotBeEquivalentTo(ReinstallTestSqlSupport.ProductionDatabaseName);

        var originalStatus = ReadServiceStatus();
        var environmentRestored = true;
        var serviceRetargeted = false;
        var testDbCreated = false;
        var logs = new List<string>();
        void Log(string m) => logs.Add(m);

        var dllName = File.Exists(Path.Combine(InstallApiDir, PreferredDll))
            ? PreferredDll
            : Directory.GetFiles(InstallApiDir, "*.dll")
                .Select(Path.GetFileName)
                .FirstOrDefault(n => n is not null && !n.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Aucune DLL API à remplacer.");
        var installedDllPath = Path.Combine(InstallApiDir, dllName);
        var stagingDll = Path.Combine(Path.GetTempPath(), "erp-reinstall-staging-" + dllName);

        try
        {
            Log($"[Test] Base dédiée : {ReinstallTestSqlSupport.TestDatabaseName}");
            await ReinstallTestSqlSupport.RecreateTestDatabaseAsync(
                masterCs, ReinstallTestSqlSupport.TestDatabaseName, CancellationToken.None);
            testDbCreated = true;
            await ReinstallTestSqlSupport.ApplyBaselineAsync(
                testCs, ReinstallTestSqlSupport.TestDatabaseName, CancellationToken.None);
            await ReinstallTestSqlSupport.VerifyBaselineAsync(
                testCs, ReinstallTestSqlSupport.TestDatabaseName, CancellationToken.None);
            await ReinstallTestSqlSupport.GrantSystemAccessAsync(
                masterCs, ReinstallTestSqlSupport.TestDatabaseName, CancellationToken.None);

            await WindowsServiceLifecycle.StopServiceAndWaitAsync(
                InstallerEngine.ServiceName, Log, CancellationToken.None);
            ReinstallTestSqlSupport.WriteServiceEnvironment(testEnvironment);
            serviceRetargeted = true;
            environmentRestored = false;

            StartServiceAndWaitRunning();

            File.Copy(installedDllPath, stagingDll, overwrite: true);

            var lockedCopyFailed = false;
            try
            {
                File.Copy(stagingDll, installedDllPath, overwrite: true);
            }
            catch (IOException)
            {
                lockedCopyFailed = true;
            }

            lockedCopyFailed.Should().BeTrue(
                "avec ErpScolaireApi Running, la DLL API doit être verrouillée avant ReleaseServerPayloadLocksAsync.");

            await WindowsServiceLifecycle.ReleaseServerPayloadLocksAsync(Log, CancellationToken.None);

            File.Copy(stagingDll, installedDllPath, overwrite: true);

            StartServiceAndWaitRunning();
            await WaitForHealthAsync();

            logs.Should().Contain(l => l.Contains("[Service]", StringComparison.Ordinal));
        }
        finally
        {
            if (serviceRetargeted)
            {
                try
                {
                    await WindowsServiceLifecycle.StopServiceAndWaitAsync(
                        InstallerEngine.ServiceName, Log, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log("[Test] Arrêt final : " + ex.Message);
                }

                try
                {
                    ReinstallTestSqlSupport.WriteServiceEnvironment(originalEnvironment);
                    environmentRestored = true;
                }
                catch (Exception ex)
                {
                    Log("[Test] Restauration Environment : " + ex.Message);
                }
            }

            if (testDbCreated)
            {
                try
                {
                    await ReinstallTestSqlSupport.DropTestDatabaseAsync(
                        masterCs, ReinstallTestSqlSupport.TestDatabaseName, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log("[Test] DROP base de test : " + ex.Message);
                }
            }

            if (originalStatus == ServiceControllerStatus.Running && serviceRetargeted && environmentRestored)
            {
                try
                {
                    StartServiceAndWaitRunning();
                }
                catch (Exception ex)
                {
                    Log("[Test] Restauration Running d'origine non garantie (Production hors scope) : " + ex.Message);
                }
            }

            try
            {
                if (File.Exists(stagingDll))
                    File.Delete(stagingDll);
            }
            catch
            {
                // ignore
            }
        }

        environmentRestored.Should().BeTrue(
            "l'Environment d'origine du service doit être restauré (Production inchangée).");
    }

    private static ServiceControllerStatus ReadServiceStatus()
    {
        using var sc = new ServiceController(InstallerEngine.ServiceName);
        sc.Refresh();
        return sc.Status;
    }

    private static void StartServiceAndWaitRunning()
    {
        using var sc = new ServiceController(InstallerEngine.ServiceName);
        sc.Refresh();
        if (sc.Status == ServiceControllerStatus.Running)
            return;

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
        sc.Refresh();
        if (sc.Status != ServiceControllerStatus.Running)
        {
            throw new InvalidOperationException(
                $"ErpScolaireApi n'est pas Running (état : {sc.Status}).");
        }
    }

    private static async Task WaitForHealthAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        for (var i = 0; i < 30; i++)
        {
            try
            {
                using var resp = await http.GetAsync("http://127.0.0.1:5096/api/health");
                if (resp.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // retry
            }

            await Task.Delay(2000);
        }

        throw new InvalidOperationException(
            "L'API n'a pas renvoyé HTTP 200 sur /api/health après redémarrage.");
    }

    private static bool IsRunningAsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
