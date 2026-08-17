using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace SchoolManagement.Setup;

internal enum ServiceRegistrationState
{
    Absent,
    Stopped,
    Running,
    Busy
}

/// <summary>Arrêt du service ErpScolaireApi et libération des processus API/Desktop avant remplacement du payload.</summary>
internal static class WindowsServiceLifecycle
{
    internal const string ApiProcessName = "SchoolManagement.API";
    internal const string DesktopProcessName = "SchoolManagement.Desktop";
    internal const int ErrorServiceMarkedForDelete = 1072;
    internal const int ErrorServiceDoesNotExist = 1060;
    internal const int MaxCreateAttempts = 8;

    /// <summary>Si true, OpenService/GetServices est interdit (phase post-sc delete).</summary>
    internal static bool ForbidServiceControllerDuringDeleteWait { get; set; }

    internal static int ServiceControllerOpenCount { get; private set; }

    internal static readonly TimeSpan ServiceStopTimeout = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan ServiceDeleteTimeout = TimeSpan.FromSeconds(45);
    internal static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(45);
    internal static readonly TimeSpan ProcessExitPoll = TimeSpan.FromMilliseconds(500);

    internal static bool ShouldStopService(ServiceControllerStatus status) =>
        status is ServiceControllerStatus.Running
            or ServiceControllerStatus.StartPending
            or ServiceControllerStatus.StopPending
            or ServiceControllerStatus.PausePending
            or ServiceControllerStatus.ContinuePending
            or ServiceControllerStatus.Paused;

    internal static bool IsMarkedForDeleteError(int exitCode, string output)
    {
        if (exitCode == ErrorServiceMarkedForDelete)
            return true;

        return output.Contains("1072", StringComparison.Ordinal)
               || output.Contains("marqué pour suppression", StringComparison.OrdinalIgnoreCase)
               || output.Contains("marked for deletion", StringComparison.OrdinalIgnoreCase);
    }

    internal static string FormatDeleteServiceLog(int exitCode, string output)
    {
        if (exitCode == 0)
            return "[SERVICE] DeleteService OK : service marqué maintenant.";
        if (IsMarkedForDeleteError(exitCode, output))
            return "[SERVICE] DeleteService = 1072 : service déjà marqué pour suppression.";
        if (exitCode == ErrorServiceDoesNotExist)
            return "[SERVICE] DeleteService = 1060 : service déjà absent.";
        return $"[SERVICE] DeleteService code {exitCode}.";
    }

    internal static string FormatDeleteMarkedDetail(int exitCode, string output)
    {
        if (exitCode == 0)
            return "[SERVICE] Service marqué pour suppression par cet appel.";
        if (IsMarkedForDeleteError(exitCode, output))
            return "[SERVICE] Service déjà marqué pour suppression avant cet appel (1072).";
        return "";
    }

    internal static string FormatDeleteTimeoutMessage(
        string serviceName,
        TimeSpan timeout,
        bool alreadyMarkedBeforeThisCall)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(
            $"L'enregistrement du service {serviceName} est encore présent dans le registre après {timeout.TotalSeconds:0}s.");
        sb.AppendLine(
            "Le Setup n'ouvre aucun handle SCM (ServiceController / GetServices / OpenService) pendant cette attente.");
        sb.AppendLine(
            $"Le handle restant peut appartenir à ce processus Setup (PID={Environment.ProcessId}) ou à un autre processus (MMC, services.msc, autre instance, outil d'administration).");
        if (alreadyMarkedBeforeThisCall)
        {
            sb.AppendLine(
                "Le service était déjà marqué pour suppression (1072) avant cet appel : le blocage provient probablement d'un handle externe.");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Ouvre un unique ServiceController pour le service cible et dispose tous les autres
    /// handles renvoyés par GetServices() — indispensable avant sc delete (erreur 1072).
    /// </summary>
    internal static ServiceController? OpenService(string serviceName)
    {
        if (ForbidServiceControllerDuringDeleteWait)
        {
            throw new InvalidOperationException(
                $"OpenService/GetServices interdit pendant l'attente post-delete (service '{serviceName}'). " +
                "Un handle SCM rouvert empêche la disparition réelle (1072).");
        }

        ServiceControllerOpenCount++;
        var all = ServiceController.GetServices();
        ServiceController? found = null;
        foreach (var s in all)
        {
            if (found is null && s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                found = s;
            else
                s.Dispose();
        }

        return found;
    }

    /// <summary>
    /// Présence de l'enregistrement : clé registre uniquement, aucun handle SCM.
    /// GetServices() après sc delete rouvre un handle et bloque la suppression.
    /// </summary>
    internal static bool ServiceExists(string serviceName) => ServiceRegistryKeyExists(serviceName);

    internal static bool ServiceRegistryKeyExists(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: false);
        return key is not null;
    }

    internal static ServiceRegistrationState ProbeRegistration(string serviceName)
    {
        using var sc = OpenService(serviceName);
        if (sc is null)
            return ServiceRegistrationState.Absent;

        sc.Refresh();
        return sc.Status switch
        {
            ServiceControllerStatus.Stopped => ServiceRegistrationState.Stopped,
            ServiceControllerStatus.Running => ServiceRegistrationState.Running,
            _ => ServiceRegistrationState.Busy
        };
    }

    /// <summary>Arrête le service Windows s'il existe et attend l'état STOPPED.</summary>
    internal static async Task StopServiceAndWaitAsync(
        string serviceName,
        Action<string> log,
        CancellationToken ct,
        TimeSpan? stopTimeout = null)
    {
        var timeout = stopTimeout ?? ServiceStopTimeout;
        ServiceController? controller;
        try
        {
            controller = OpenService(serviceName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Impossible d'énumérer les services Windows pour '{serviceName}'.", ex);
        }

        if (controller is null)
        {
            log($"[SERVICE] Aucun service '{serviceName}' — première installation ou service déjà retiré.");
            return;
        }

        using (controller)
        {
            controller.Refresh();
            log($"[SERVICE] État initial de {serviceName} : {controller.Status}");

            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                log($"[SERVICE] {serviceName} déjà arrêté.");
                log("[SERVICE] Service STOPPED.");
            }
            else if (ShouldStopService(controller.Status))
            {
                if (controller.Status != ServiceControllerStatus.StopPending)
                {
                    log($"[SERVICE] Arrêt {serviceName}...");
                    try
                    {
                        controller.Stop();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Impossible d'arrêter le service {serviceName} (état {controller.Status}).", ex);
                    }
                }
                else
                {
                    log($"[SERVICE] {serviceName} déjà en cours d'arrêt (StopPending)...");
                }

                await WaitForServiceStatusAsync(
                    controller,
                    ServiceControllerStatus.Stopped,
                    timeout,
                    serviceName,
                    log,
                    ct);
            }

            controller.Refresh();
            log($"[SERVICE] Service STOPPED.");
            log($"[SERVICE] État final de {serviceName} : {controller.Status}");

            if (controller.Status != ServiceControllerStatus.Stopped)
            {
                throw new InvalidOperationException(
                    $"Le service {serviceName} n'est pas STOPPED après {timeout.TotalSeconds:0}s (état : {controller.Status}). " +
                    "Impossible de remplacer les fichiers API verrouillés.");
            }
        }

        log("[SERVICE] Tous les handles ServiceController libérés.");
    }

    internal static async Task WaitUntilServiceAbsentAsync(
        string serviceName,
        Action<string> log,
        CancellationToken ct,
        TimeSpan? timeout = null,
        Func<string, bool>? exists = null,
        bool alreadyMarkedBeforeThisCall = false)
    {
        var limit = timeout ?? ServiceDeleteTimeout;
        var probe = exists ?? ServiceRegistryKeyExists;
        var deadline = DateTime.UtcNow + limit;
        var loggedWait = false;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (!probe(serviceName))
            {
                log("[SERVICE] Service absent du registre/SCM.");
                return;
            }

            if (!loggedWait)
            {
                log("[SERVICE] Attente de disparition via registre uniquement.");
                loggedWait = true;
            }

            await Task.Delay(ProcessExitPoll, ct);
        }

        if (!probe(serviceName))
        {
            log("[SERVICE] Service absent du registre/SCM.");
            return;
        }

        throw new System.TimeoutException(
            FormatDeleteTimeoutMessage(serviceName, limit, alreadyMarkedBeforeThisCall));
    }

    /// <summary>
    /// sc delete puis attente de disparition via la clé registre.
    /// N'ouvre aucun ServiceController après delete (sinon le SCM ne peut pas terminer la suppression).
    /// </summary>
    internal static async Task DeleteServiceAndWaitAsync(
        string serviceName,
        Action<string> log,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        log($"[SERVICE] PID Setup={Environment.ProcessId}");
        if (!ServiceRegistryKeyExists(serviceName))
        {
            log($"[SERVICE] Aucun service '{serviceName}' à supprimer — première installation.");
            return;
        }

        log($"[SERVICE] Suppression {serviceName}...");
        log($"[SERVICE] sc delete lancé à {DateTime.Now:HH:mm:ss.fff}");
        var (exitCode, output) = RunSc($"delete {serviceName}");
        if (!string.IsNullOrWhiteSpace(output))
            log(output.Trim());

        var alreadyMarked = IsMarkedForDeleteError(exitCode, output);
        if (exitCode == 0 || alreadyMarked || exitCode == ErrorServiceDoesNotExist)
        {
            log(FormatDeleteServiceLog(exitCode, output));
            var detail = FormatDeleteMarkedDetail(exitCode, output);
            if (!string.IsNullOrWhiteSpace(detail))
                log(detail);
        }
        else
        {
            throw new InvalidOperationException(
                $"sc.exe delete {serviceName} a échoué (code {exitCode}).{Environment.NewLine}{output}");
        }

        if (exitCode == ErrorServiceDoesNotExist && !ServiceRegistryKeyExists(serviceName))
        {
            log("[SERVICE] Service absent du registre/SCM.");
            return;
        }

        var previousForbid = ForbidServiceControllerDuringDeleteWait;
        ForbidServiceControllerDuringDeleteWait = true;
        try
        {
            log("[SERVICE] Attente de disparition via registre uniquement.");
            log($"[SERVICE] ServiceControllerOpenCount={ServiceControllerOpenCount}");
            await WaitUntilServiceAbsentAsync(
                serviceName,
                log,
                ct,
                timeout,
                exists: ServiceRegistryKeyExists,
                alreadyMarkedBeforeThisCall: alreadyMarked);
        }
        finally
        {
            ForbidServiceControllerDuringDeleteWait = previousForbid;
        }
    }

    internal static (int ExitCode, string Output) RunSc(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de lancer sc.exe");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
        return (p.ExitCode, (stdout + Environment.NewLine + stderr).Trim());
    }

    internal static async Task WaitForProcessesExitAsync(
        string processName,
        Action<string> log,
        CancellationToken ct,
        TimeSpan? exitTimeout = null,
        bool allowForceKillAfterTimeout = true)
    {
        var timeout = exitTimeout ?? ProcessExitTimeout;
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var running = Process.GetProcessesByName(processName);
            try
            {
                if (running.Length == 0)
                {
                    log($"[Process] Aucun processus {processName} actif.");
                    return;
                }

                log($"[Process] {running.Length} processus {processName} encore actif(s) — attente…");
            }
            finally
            {
                foreach (var p in running)
                    p.Dispose();
            }

            await Task.Delay(ProcessExitPoll, ct);
        }

        var remaining = Process.GetProcessesByName(processName);
        try
        {
            if (remaining.Length == 0)
                return;

            if (!allowForceKillAfterTimeout)
            {
                throw new System.TimeoutException(
                    $"Le processus {processName} est encore actif après {timeout.TotalSeconds:0}s.");
            }

            log($"[Process] Timeout — terminaison forcée de {remaining.Length} processus {processName}.");
            foreach (var proc in remaining)
            {
                try
                {
                    if (proc.HasExited)
                        continue;

                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(5000);
                    log($"[Process] Processus {processName} PID {proc.Id} terminé.");
                }
                catch (Exception ex)
                {
                    log($"[Process] Échec terminaison PID {proc.Id} : {ex.Message}");
                }
            }
        }
        finally
        {
            foreach (var p in remaining)
                p.Dispose();
        }

        var still = Process.GetProcessesByName(processName);
        try
        {
            if (still.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Impossible de libérer {still.Length} processus {processName} — fichiers encore verrouillés.");
            }
        }
        finally
        {
            foreach (var p in still)
                p.Dispose();
        }
    }

    internal static async Task StopDesktopProcessesAsync(Action<string> log, CancellationToken ct)
    {
        var running = Process.GetProcessesByName(DesktopProcessName);
        try
        {
            if (running.Length == 0)
            {
                log("[Process] Aucun Desktop ERP Scolaire en cours d'exécution.");
                return;
            }

            log($"[Process] Fermeture de {running.Length} instance(s) {DesktopProcessName} avant remplacement du payload…");
            foreach (var proc in running)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.CloseMainWindow();
                    }
                }
                catch (Exception ex)
                {
                    log($"[Process] CloseMainWindow PID {proc.Id} : {ex.Message}");
                }
            }
        }
        finally
        {
            foreach (var p in running)
                p.Dispose();
        }

        await WaitForProcessesExitAsync(
            DesktopProcessName,
            log,
            ct,
            TimeSpan.FromSeconds(20),
            allowForceKillAfterTimeout: true);
    }

    /// <summary>Libère les verrous API + Desktop avant copie du payload serveur.</summary>
    internal static async Task ReleaseServerPayloadLocksAsync(Action<string> log, CancellationToken ct)
    {
        log("[Setup] Préparation réinstallation — libération des fichiers API/Desktop…");
        await StopDesktopProcessesAsync(log, ct);
        await StopServiceAndWaitAsync(InstallerEngine.ServiceName, log, ct);
        await WaitForProcessesExitAsync(ApiProcessName, log, ct);
        log("[Setup] Fichiers API/Desktop prêts pour remplacement.");
    }

    /// <summary>Libère les verrous Desktop avant copie client.</summary>
    internal static async Task ReleaseClientPayloadLocksAsync(Action<string> log, CancellationToken ct)
    {
        log("[Setup] Préparation — fermeture du Desktop avant remplacement…");
        await StopDesktopProcessesAsync(log, ct);
        log("[Setup] Payload Desktop prêt pour remplacement.");
    }

    private static async Task WaitForServiceStatusAsync(
        ServiceController controller,
        ServiceControllerStatus desired,
        TimeSpan timeout,
        string serviceName,
        Action<string> log,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        ServiceControllerStatus? lastLogged = null;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            controller.Refresh();
            if (controller.Status == desired)
            {
                log($"[SERVICE] {serviceName} → {desired}.");
                return;
            }

            if (lastLogged != controller.Status)
            {
                log($"[SERVICE] En attente de {desired}... (actuel : {controller.Status})");
                lastLogged = controller.Status;
            }

            try
            {
                controller.WaitForStatus(desired, ProcessExitPoll);
                controller.Refresh();
                if (controller.Status == desired)
                {
                    log($"[SERVICE] {serviceName} → {desired}.");
                    return;
                }
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                // poll loop
            }

            await Task.Delay(ProcessExitPoll, ct);
        }

        controller.Refresh();
        throw new System.TimeoutException(
            $"Le service {serviceName} n'a pas atteint {desired} dans {timeout.TotalSeconds:0}s (état : {controller.Status}).");
    }
}
