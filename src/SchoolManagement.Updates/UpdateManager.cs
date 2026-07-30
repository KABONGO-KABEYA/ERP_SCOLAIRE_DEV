using System.Diagnostics;

namespace SchoolManagement.Updates;

/// <summary>
/// Orchestrateur indépendant : check → download → hash → install.
/// Aucune dépendance UI : les callbacks permettent de brancher Desktop / console.
/// </summary>
public sealed class UpdateManager
{
    private readonly UpdateApiService _api;
    private readonly DownloadManager _download;
    private readonly UpdateSettingsStore _settingsStore;
    private readonly UpdateHistoryStore _historyStore;
    private readonly string _downloadDirectory;
    private readonly UpdateClientPlatform _platform;

    public UpdateManager(
        UpdateApiService api,
        DownloadManager download,
        UpdateSettingsStore settingsStore,
        UpdateHistoryStore historyStore,
        string downloadDirectory,
        UpdateClientPlatform platform)
    {
        _api = api;
        _download = download;
        _settingsStore = settingsStore;
        _historyStore = historyStore;
        _downloadDirectory = downloadDirectory;
        _platform = platform;
        Directory.CreateDirectory(downloadDirectory);
    }

    public UpdateSettings Settings => _settingsStore.Load();

    public UpdateCheckOutcome Evaluate(string currentVersion, UpdateManifest manifest)
    {
        var belowMinimum = VersionManager.IsOlderThan(currentVersion, manifest.MinimumVersion);
        var newer = VersionManager.IsNewer(manifest.LatestVersion, currentVersion);
        if (!newer && !belowMinimum)
        {
            return new UpdateCheckOutcome
            {
                Availability = UpdateAvailability.UpToDate,
                CurrentVersion = currentVersion,
                Manifest = manifest
            };
        }

        var mandatory = manifest.Mandatory || belowMinimum;
        return new UpdateCheckOutcome
        {
            Availability = mandatory ? UpdateAvailability.Mandatory : UpdateAvailability.Optional,
            CurrentVersion = currentVersion,
            Manifest = manifest
        };
    }

    public async Task<UpdateCheckOutcome?> CheckSilentlyAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsStore.Load();
        if (!settings.AutoCheckEnabled)
        {
            return null;
        }

        if (IsSnoozed(settings))
        {
            _historyStore.Append(new UpdateHistoryEntry
            {
                Result = UpdateHistoryResult.Snoozed,
                VersionFound = settings.LastFoundVersion,
                Detail = "Reporté jusqu'au prochain contrôle."
            });
            return null;
        }

        try
        {
            var manifest = await _api.CheckAsync(
                settings.CheckEndpoint,
                _platform,
                settings.CurrentVersion,
                cancellationToken);

            settings.LastCheckUtc = DateTime.UtcNow.ToString("O");
            if (manifest is null)
            {
                _settingsStore.Save(settings);
                _historyStore.Append(new UpdateHistoryEntry
                {
                    Result = UpdateHistoryResult.CheckFailed,
                    Detail = "Réponse vide ou serveur indisponible."
                });
                return null;
            }

            settings.LastFoundVersion = manifest.LatestVersion;
            _settingsStore.Save(settings);

            var outcome = Evaluate(settings.CurrentVersion, manifest);
            _historyStore.Append(new UpdateHistoryEntry
            {
                Result = outcome.Availability switch
                {
                    UpdateAvailability.UpToDate => UpdateHistoryResult.UpToDate,
                    UpdateAvailability.Optional => UpdateHistoryResult.OptionalAvailable,
                    _ => UpdateHistoryResult.MandatoryAvailable
                },
                VersionFound = manifest.LatestVersion
            });

            return outcome.Availability == UpdateAvailability.UpToDate ? null : outcome;
        }
        catch (Exception ex)
        {
            // Silencieux si offline / erreur réseau.
            _historyStore.Append(new UpdateHistoryEntry
            {
                Result = UpdateHistoryResult.CheckFailed,
                Detail = ex.Message
            });
            return null;
        }
    }

    public void SnoozeOptional(TimeSpan duration)
    {
        var settings = _settingsStore.Load();
        settings.SnoozeUntilUtc = DateTime.UtcNow.Add(duration).ToString("O");
        _settingsStore.Save(settings);
        _historyStore.Append(new UpdateHistoryEntry
        {
            Result = UpdateHistoryResult.Snoozed,
            Detail = $"Plus tard jusqu'à {settings.SnoozeUntilUtc}"
        });
    }

    public async Task<string> DownloadAndVerifyAsync(
        UpdateManifest manifest,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            throw new InvalidOperationException("Aucune URL de téléchargement.");
        }

        var fileName = Path.GetFileName(new Uri(manifest.DownloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = _platform == UpdateClientPlatform.Mobile
                ? $"update-{manifest.LatestVersion}.apk"
                : $"update-{manifest.LatestVersion}.exe";
        }

        var destination = Path.Combine(_downloadDirectory, fileName);
        _historyStore.Append(new UpdateHistoryEntry
        {
            Result = UpdateHistoryResult.DownloadStarted,
            VersionFound = manifest.LatestVersion,
            Detail = destination
        });

        try
        {
            await _download.DownloadAsync(
                manifest.DownloadUrl,
                destination,
                manifest.Size,
                progress,
                cancellationToken);

            var hash = await DownloadManager.ComputeSha256Async(destination, cancellationToken);
            if (!DownloadManager.HashesMatch(manifest.Sha256, hash))
            {
                File.Delete(destination);
                _historyStore.Append(new UpdateHistoryEntry
                {
                    Result = UpdateHistoryResult.HashInvalid,
                    VersionFound = manifest.LatestVersion,
                    Detail = $"Attendu={manifest.Sha256}, obtenu={hash}"
                });
                throw new InvalidOperationException("Le fichier téléchargé est invalide.");
            }

            _historyStore.Append(new UpdateHistoryEntry
            {
                Result = UpdateHistoryResult.DownloadSucceeded,
                VersionFound = manifest.LatestVersion,
                Detail = destination
            });
            return destination;
        }
        catch (OperationCanceledException)
        {
            _historyStore.Append(new UpdateHistoryEntry
            {
                Result = UpdateHistoryResult.DownloadCancelled,
                VersionFound = manifest.LatestVersion
            });
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _historyStore.Append(new UpdateHistoryEntry
            {
                Result = UpdateHistoryResult.DownloadFailed,
                VersionFound = manifest.LatestVersion,
                Detail = ex.Message
            });
            throw;
        }
    }

    public void LaunchDesktopInstaller(string installerPath, string? appRestartPath = null)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installateur introuvable.", installerPath);
        }

        _historyStore.Append(new UpdateHistoryEntry
        {
            Result = UpdateHistoryResult.InstallStarted,
            Detail = installerPath
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(appRestartPath))
        {
            // Convention : installateur / redémarrage laissé à l'installeur ; on passe le chemin en arg si supporté.
            startInfo.Arguments = $"\"{appRestartPath}\"";
        }

        Process.Start(startInfo);

        var settings = _settingsStore.Load();
        settings.LastUpdateUtc = DateTime.UtcNow.ToString("O");
        _settingsStore.Save(settings);

        _historyStore.Append(new UpdateHistoryEntry
        {
            Result = UpdateHistoryResult.InstallSucceeded,
            Detail = installerPath
        });
    }

    private static bool IsSnoozed(UpdateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SnoozeUntilUtc))
        {
            return false;
        }

        return DateTime.TryParse(settings.SnoozeUntilUtc, null,
                   System.Globalization.DateTimeStyles.RoundtripKind, out var until)
               && until > DateTime.UtcNow;
    }
}
