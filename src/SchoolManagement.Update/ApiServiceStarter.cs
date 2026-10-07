using System.ComponentModel;
using System.Net.Http;
using System.ServiceProcess;
using Microsoft.Win32;
using SchoolManagement.Setup;

namespace SchoolManagement.Update;

internal static class ApiServiceStarter
{
    internal static async Task StartAndWaitHealthyAsync(
        string apiDirectory,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        using var sc = new ServiceController(InstallerEngine.ServiceName);
        sc.Refresh();
        log($"[SERVICE] Démarrage {InstallerEngine.ServiceName} (état initial : {sc.Status})...");

        if (sc.Status == ServiceControllerStatus.Running)
        {
            log("[SERVICE] Service RUNNING.");
        }
        else
        {
            TryIncreaseServicesPipeTimeout(log);

            try
            {
                sc.Start();
            }
            catch (Exception startEx) when (IsScmStartTimeout(startEx))
            {
                log("[SERVICE] Timeout SCM 1053 — poursuite de l'attente Running/health…");
                log($"[SERVICE] Détail : {startEx.Message}");
            }

            var timeout = ApiStartupWait.ResolveTimeout();
            var deadline = DateTime.UtcNow + timeout;
            ServiceControllerStatus? lastLogged = null;
            var lastProgress = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    log("[SERVICE] Service RUNNING.");
                    break;
                }

                if (lastLogged != sc.Status)
                {
                    log($"[SERVICE] En attente de Running... (actuel : {sc.Status})");
                    lastLogged = sc.Status;
                }
                else if (DateTime.UtcNow - lastProgress >= TimeSpan.FromSeconds(ApiStartupWait.DefaultProgressLogSeconds))
                {
                    lastProgress = DateTime.UtcNow;
                    log($"[SERVICE] Toujours en démarrage ({sc.Status}) — SchemaInitializers possibles.");
                }

                if (sc.Status == ServiceControllerStatus.Stopped && await IsLocalApiHealthyAsync(cancellationToken))
                {
                    log("[SERVICE] Health API déjà OK alors que SCM = Stopped — nouvelle tentative Start…");
                    try
                    {
                        sc.Start();
                    }
                    catch (Exception retryEx) when (IsScmStartTimeout(retryEx) || IsAlreadyStarted(retryEx))
                    {
                        log($"[SERVICE] Nouvelle tentative Start : {retryEx.Message}");
                    }
                }

                await Task.Delay(2000, cancellationToken);
            }

            sc.Refresh();
            if (sc.Status != ServiceControllerStatus.Running && !await IsLocalApiHealthyAsync(cancellationToken))
            {
                throw new System.TimeoutException(
                    $"Le service {InstallerEngine.ServiceName} n'est pas Running après {ApiStartupWait.Format(timeout)} (état : {sc.Status}).");
            }
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var healthUrls = new[]
        {
            "http://127.0.0.1:5096/api/v1/health",
            "http://127.0.0.1:5096/api/health",
        };

        var wait = await ApiStartupWait.WaitAsync(
            ct => ProbeHealthyAsync(http, healthUrls, ct),
            () => IsServiceAlive(),
            log,
            ApiStartupWait.ResolveTimeout(),
            cancellationToken: cancellationToken);

        if (!wait.Healthy)
        {
            throw new InvalidOperationException(
                "L'API n'est pas prête après redémarrage : " + wait.Reason);
        }

        log("[SERVICE] Health API OK (schéma / seed terminés).");
    }

    private static bool IsServiceAlive()
    {
        try
        {
            using var sc = new ServiceController(InstallerEngine.ServiceName);
            sc.Refresh();
            return sc.Status is ServiceControllerStatus.Running
                or ServiceControllerStatus.StartPending
                or ServiceControllerStatus.StopPending;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ProbeHealthyAsync(
        HttpClient http,
        IReadOnlyList<string> urls,
        CancellationToken cancellationToken)
    {
        foreach (var url in urls)
        {
            try
            {
                using var response = await http.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
                // ignore per-url
            }
        }

        return false;
    }

    private static async Task<bool> IsLocalApiHealthyAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        return await ProbeHealthyAsync(
            http,
            ["http://127.0.0.1:5096/api/v1/health", "http://127.0.0.1:5096/api/health"],
            cancellationToken);
    }

    private static bool IsScmStartTimeout(Exception ex)
    {
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            if (cur is Win32Exception win32 && win32.NativeErrorCode == 1053)
                return true;
            if (cur.Message.Contains("1053", StringComparison.Ordinal)
                || cur.Message.Contains("did not respond", StringComparison.OrdinalIgnoreCase)
                || cur.Message.Contains("n'a pas répondu", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAlreadyStarted(Exception ex)
    {
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            if (cur.Message.Contains("already been started", StringComparison.OrdinalIgnoreCase)
                || cur.Message.Contains("déjà démarré", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryIncreaseServicesPipeTimeout(Action<string> log)
    {
        try
        {
            const int desiredMs = 180_000;
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control",
                writable: true);
            if (key is null)
                return;

            var current = key.GetValue("ServicesPipeTimeout");
            if (current is int i && i >= desiredMs)
                return;

            key.SetValue("ServicesPipeTimeout", desiredMs, RegistryValueKind.DWord);
            log("[SERVICE] ServicesPipeTimeout porté à 180 s (SCM / schéma long).");
        }
        catch (Exception ex)
        {
            log($"[SERVICE] ServicesPipeTimeout non modifié : {ex.Message}");
        }
    }
}
