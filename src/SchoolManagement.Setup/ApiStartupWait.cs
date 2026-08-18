using System.Diagnostics;
using System.Net.Http;

namespace SchoolManagement.Setup;

/// <summary>
/// Attente du health API pour le Setup : distingue un process mort d'une
/// initialisation lente (SchemaInitializers + SecurityEngine Phase 0).
/// </summary>
internal static class ApiStartupWait
{
    internal const string TimeoutEnvVar = "ERP_SETUP_API_START_TIMEOUT_SECONDS";
    internal const int DefaultTimeoutSeconds = 1200;
    internal const int MinTimeoutSeconds = 60;
    internal const int MaxTimeoutSeconds = 1800;
    internal const int DefaultPollMilliseconds = 2000;
    internal const int DefaultProgressLogSeconds = 15;
    internal const int DefaultConfirmCount = 2;

    internal static TimeSpan ResolveTimeout(string? envValue = null)
    {
        var raw = envValue ?? Environment.GetEnvironmentVariable(TimeoutEnvVar);
        if (int.TryParse(raw, out var seconds) && seconds > 0)
            return Clamp(TimeSpan.FromSeconds(seconds));

        return TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    }

    internal static TimeSpan Clamp(TimeSpan timeout)
    {
        var seconds = (int)Math.Round(timeout.TotalSeconds);
        if (seconds < MinTimeoutSeconds) seconds = MinTimeoutSeconds;
        if (seconds > MaxTimeoutSeconds) seconds = MaxTimeoutSeconds;
        return TimeSpan.FromSeconds(seconds);
    }

    internal static async Task<ApiStartupWaitResult> WaitAsync(
        Func<CancellationToken, Task<bool>> probeHealthy,
        Func<bool> isProcessAlive,
        Action<string> log,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        TimeSpan? progressInterval = null,
        int confirmCount = DefaultConfirmCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probeHealthy);
        ArgumentNullException.ThrowIfNull(isProcessAlive);
        ArgumentNullException.ThrowIfNull(log);

        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var poll = pollInterval ?? TimeSpan.FromMilliseconds(DefaultPollMilliseconds);
        var progress = progressInterval ?? TimeSpan.FromSeconds(DefaultProgressLogSeconds);
        if (confirmCount < 1) confirmCount = 1;

        var sw = Stopwatch.StartNew();
        var lastProgress = TimeSpan.Zero;
        var consecutive = 0;
        log($"Attente health API (timeout {Format(timeout)}, premier démarrage possible lent)…");

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!isProcessAlive())
            {
                return new ApiStartupWaitResult(
                    false,
                    "L'API s'est arrêtée pendant l'initialisation (processus terminé avant le health).",
                    sw.Elapsed);
            }

            var ok = false;
            try
            {
                ok = await probeHealthy(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                ok = false;
            }

            if (ok)
            {
                consecutive++;
                if (consecutive >= confirmCount)
                {
                    log($"API prête (health confirmé {confirmCount}x, {Format(sw.Elapsed)}).");
                    return new ApiStartupWaitResult(true, "OK", sw.Elapsed);
                }

                log($"Health HTTP OK ({consecutive}/{confirmCount}), confirmation…");
            }
            else
            {
                consecutive = 0;
                if (sw.Elapsed - lastProgress >= progress)
                {
                    lastProgress = sw.Elapsed;
                    log(
                        $"L'API initialise encore le schéma / SecurityEngine Phase 0 " +
                        $"(écoulé {Format(sw.Elapsed)} / max {Format(timeout)})…");
                }
            }

            var remaining = timeout - sw.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            var delay = remaining < poll ? remaining : poll;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        if (!isProcessAlive())
        {
            return new ApiStartupWaitResult(
                false,
                "L'API s'est arrêtée pendant l'initialisation (processus terminé avant le health).",
                sw.Elapsed);
        }

        return new ApiStartupWaitResult(
            false,
            $"Timeout : l'API n'a pas répondu au health après {Format(timeout)} " +
            "(processus encore en vie — initialisation trop longue ou bloquée).",
            sw.Elapsed);
    }

    internal static async Task<bool> ProbeUrlsAsync(
        HttpClient http,
        IReadOnlyList<string> urls,
        CancellationToken cancellationToken)
    {
        foreach (var url in urls)
        {
            using var resp = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return false;
        }

        return urls.Count > 0;
    }

    internal static string Format(TimeSpan value)
    {
        if (value.TotalHours >= 1)
            return $"{(int)value.TotalHours}h {value.Minutes:00}m";
        if (value.TotalMinutes >= 1)
            return $"{(int)value.TotalMinutes} min {value.Seconds:00}s";
        return $"{Math.Max(0, (int)value.TotalSeconds)}s";
    }
}

internal sealed record ApiStartupWaitResult(bool Healthy, string Reason, TimeSpan Elapsed);
