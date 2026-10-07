using SchoolManagement.Setup;

namespace SchoolManagement.Update;

public sealed class UpdateEngine
{
    private readonly List<string> _logLines = [];
    private readonly Action<string> _log;

    public UpdateEngine(Action<string>? log = null)
    {
        _log = line =>
        {
            _logLines.Add(line);
            log?.Invoke(line);
        };
    }

    public IReadOnlyList<string> LogLines => _logLines;

    public static string DefaultInstallRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ERP Scolaire");

    public static string FindPayloadRoot()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var candidates = new[]
        {
            Path.Combine(baseDir, "payload"),
            Path.Combine(baseDir, "..", "payload"),
            Path.Combine(Directory.GetCurrentDirectory(), "payload"),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(Path.Combine(full, "desktop"))
                && Directory.Exists(Path.Combine(full, "api")))
            {
                return full;
            }
        }

        throw new DirectoryNotFoundException(
            "Dossier payload introuvable (desktop/ + api/). Regénérez le package de mise à jour.");
    }

    public DetectedInstallation DetectInstallation(string? installRoot = null)
    {
        installRoot = string.IsNullOrWhiteSpace(installRoot)
            ? DefaultInstallRoot
            : installRoot.Trim();

        var apiDir = Path.Combine(installRoot, "Api");
        var desktopDir = Path.Combine(installRoot, "Desktop");
        var desktopExe = Path.Combine(desktopDir, "SchoolManagement.Desktop.exe");
        if (!File.Exists(desktopExe))
        {
            throw new InvalidOperationException(
                $"Installation ERP Scolaire introuvable : {desktopExe} absent.");
        }

        var apiExe = Path.Combine(apiDir, "SchoolManagement.API.exe");
        var hasApi = File.Exists(apiExe);
        var hasService = WindowsServiceLifecycle.ServiceExists(InstallerEngine.ServiceName);
        var role = hasApi || hasService ? UpdateInstallRole.Server : UpdateInstallRole.Client;
        var version = InstalledVersionReader.ReadFromDesktop(desktopDir);

        return new DetectedInstallation
        {
            InstallRoot = installRoot,
            Role = role,
            ApiDirectory = apiDir,
            DesktopDirectory = desktopDir,
            PreviousVersion = version,
            HasApiService = hasService,
        };
    }

    public async Task<UpdateReport> RunAsync(
        DetectedInstallation installation,
        CancellationToken cancellationToken = default)
    {
        string? rollbackRoot = null;
        var schoolCountBefore = -1;
        SubscriptionBootstrapResult? subscriptionResult = null;

        try
        {
            var payload = FindPayloadRoot();
            var newVersion = InstalledVersionReader.ReadFromPayloadDesktop(Path.Combine(payload, "desktop"));
            _log("==================================================");
            _log(" ERP SCOLAIRE — MISE À JOUR");
            _log("==================================================");
            _log($"Installation : {installation.InstallRoot}");
            _log($"Rôle détecté : {installation.Role}");
            _log($"Version précédente : {installation.PreviousVersion}");
            _log($"Nouvelle version : {newVersion}");

            rollbackRoot = BackupBinaries(installation);
            _log($"Sauvegarde binaires : {rollbackRoot}");

            if (installation.Role == UpdateInstallRole.Server)
            {
                schoolCountBefore = await ExistingInstallationSubscriptionBootstrap.CountActiveSchoolsAsync(
                    installation.ApiDirectory,
                    cancellationToken);
                _log($"[DB] Écoles actives avant mise à jour : {schoolCountBefore}");

                await UpdateApiAsync(installation, payload, cancellationToken);
                await ApiServiceStarter.StartAndWaitHealthyAsync(installation.ApiDirectory, _log, cancellationToken);

                var schemaOk = await ExistingInstallationSubscriptionBootstrap.VerifySchemaAsync(
                    installation.ApiDirectory,
                    cancellationToken);
                if (!schemaOk)
                {
                    throw new InvalidOperationException(
                        "Schéma incomplet après redémarrage API : vérifier SchoolSubscriptions, SyncEntityIdentity, SyncDestination et les colonnes d'audit de l'historique tarifaire.");
                }

                _log("[DB] Schéma vérifié — abonnement, correspondances et destination cloud, historique tarifaire corrigé.");
                subscriptionResult = await ExistingInstallationSubscriptionBootstrap.EnsureAsync(
                    installation.ApiDirectory,
                    _log,
                    cancellationToken);
            }

            await UpdateDesktopAsync(installation, payload, cancellationToken);

            var schoolCountAfter = installation.Role == UpdateInstallRole.Server
                ? await ExistingInstallationSubscriptionBootstrap.CountActiveSchoolsAsync(
                    installation.ApiDirectory,
                    cancellationToken)
                : schoolCountBefore;

            if (schoolCountBefore >= 0 && schoolCountAfter >= 0 && schoolCountBefore != schoolCountAfter)
            {
                throw new InvalidOperationException(
                    $"Nombre d'écoles modifié ({schoolCountBefore} → {schoolCountAfter}) — mise à jour annulée.");
            }

            _log("Toutes les données existantes ont été conservées.");
            return new UpdateReport
            {
                Success = true,
                PreviousVersion = installation.PreviousVersion,
                NewVersion = newVersion,
                ApiOk = installation.Role == UpdateInstallRole.Server,
                DatabaseOk = installation.Role != UpdateInstallRole.Server || schoolCountAfter >= 0,
                SchemaOk = installation.Role != UpdateInstallRole.Server || subscriptionResult?.TableExists == true,
                DesktopOk = true,
                SubscriptionOk = installation.Role != UpdateInstallRole.Server
                    || subscriptionResult is not null,
                SubscriptionCreated = subscriptionResult?.SubscriptionCreated == true,
                SubscriptionAlreadyExisted = subscriptionResult?.SubscriptionAlreadyExisted == true,
                SubscriptionDetail = subscriptionResult?.Detail,
                SchoolCountBefore = Math.Max(0, schoolCountBefore),
                SchoolCountAfter = Math.Max(0, schoolCountAfter),
                RollbackPath = rollbackRoot,
                LogLines = _logLines.ToArray(),
            };
        }
        catch (Exception ex)
        {
            _log($"ERREUR : {ex.Message}");
            if (rollbackRoot is not null)
            {
                try
                {
                    RestoreBinaries(installation, rollbackRoot);
                    _log("Rollback binaires effectué.");
                    if (installation.Role == UpdateInstallRole.Server && installation.HasApiService)
                    {
                        await ApiServiceStarter.StartAndWaitHealthyAsync(
                            installation.ApiDirectory,
                            _log,
                            cancellationToken);
                    }
                }
                catch (Exception rollbackEx)
                {
                    _log($"Rollback échoué : {rollbackEx.Message}");
                }
            }

            return new UpdateReport
            {
                Success = false,
                PreviousVersion = installation.PreviousVersion,
                NewVersion = InstalledVersionReader.UpdatePackageVersion,
                ApiOk = false,
                DatabaseOk = false,
                SchemaOk = false,
                DesktopOk = false,
                SubscriptionOk = false,
                SubscriptionCreated = subscriptionResult?.SubscriptionCreated == true,
                SubscriptionAlreadyExisted = subscriptionResult?.SubscriptionAlreadyExisted == true,
                SubscriptionDetail = subscriptionResult?.Detail,
                SchoolCountBefore = Math.Max(0, schoolCountBefore),
                RollbackPath = rollbackRoot,
                Error = ex.Message,
                LogLines = _logLines.ToArray(),
            };
        }
    }

    private async Task UpdateApiAsync(
        DetectedInstallation installation,
        string payloadRoot,
        CancellationToken cancellationToken)
    {
        _log("[API] Arrêt service et remplacement binaires…");
        await WindowsServiceLifecycle.ReleaseServerPayloadLocksAsync(_log, cancellationToken);

        var apiSnapshot = PayloadFilePreserver.SnapshotFiles(
            installation.ApiDirectory,
            PayloadFilePreserver.ApiPreservedFileNames);

        PayloadFilePreserver.CopyPayloadDirectory(
            Path.Combine(payloadRoot, "api"),
            installation.ApiDirectory,
            PayloadFilePreserver.ApiPreservedFileNames,
            apiSnapshot);

        PayloadFilePreserver.UnblockFiles(installation.ApiDirectory);
        _log("[API] Binaires API mis à jour (configuration locale conservée).");
        await Task.CompletedTask;
    }

    private async Task UpdateDesktopAsync(
        DetectedInstallation installation,
        string payloadRoot,
        CancellationToken cancellationToken)
    {
        _log("[DESKTOP] Remplacement binaires Desktop…");
        await WindowsServiceLifecycle.ReleaseClientPayloadLocksAsync(_log, cancellationToken);

        var desktopSnapshot = PayloadFilePreserver.SnapshotFiles(
            installation.DesktopDirectory,
            PayloadFilePreserver.DesktopPreservedFileNames);

        PayloadFilePreserver.CopyPayloadDirectory(
            Path.Combine(payloadRoot, "desktop"),
            installation.DesktopDirectory,
            PayloadFilePreserver.DesktopPreservedFileNames,
            desktopSnapshot);

        PayloadFilePreserver.UnblockFiles(installation.DesktopDirectory);
        _log("[DESKTOP] Binaires Desktop mis à jour (appsettings conservé).");
    }

    private static string BackupBinaries(DetectedInstallation installation)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ERP_SCOLAIRE",
            "UpdateRollback",
            stamp);

        CopyTree(installation.DesktopDirectory, Path.Combine(root, "Desktop"));
        if (Directory.Exists(installation.ApiDirectory))
            CopyTree(installation.ApiDirectory, Path.Combine(root, "Api"));

        return root;
    }

    private static void RestoreBinaries(DetectedInstallation installation, string rollbackRoot)
    {
        var desktopBackup = Path.Combine(rollbackRoot, "Desktop");
        var apiBackup = Path.Combine(rollbackRoot, "Api");
        if (Directory.Exists(desktopBackup))
            CopyTree(desktopBackup, installation.DesktopDirectory);
        if (Directory.Exists(apiBackup))
            CopyTree(apiBackup, installation.ApiDirectory);
    }

    private static void CopyTree(string source, string target)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var dest = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
