using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Setup;

public enum InstallRole
{
    Client,
    Server
}

public sealed class InstallOptions
{
    public InstallRole Role { get; set; } = InstallRole.Client;
    public string InstallRoot { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ERP Scolaire");

    // SQL local
    public string SqlServer { get; set; } = @"localhost\HEROS_SQL19";
    public string Database { get; set; } = "SchoolManagementRDC_Production";
    public bool UseWindowsAuth { get; set; } = true;
    public string SqlUser { get; set; } = "sa";
    public string SqlPassword { get; set; } = "";
    public bool SqlConnectionVerified { get; set; }

    // Cloud SQL (sync)
    public bool ConfigureCloudSync { get; set; } = true;
    public string CloudSqlServer { get; set; } = "169.58.93.203";
    public int CloudSqlPort { get; set; } = 1433;
    public string CloudDatabase { get; set; } = "SchoolManagementRDC_Production";
    public string CloudSqlUser { get; set; } = "sa";
    public string CloudSqlPassword { get; set; } = "";
    public int CloudSyncIntervalMinutes { get; set; } = 5;
    public bool CloudConnectionVerified { get; set; }

    // Fichiers
    public string StorageRoot { get; set; } = @"D:\ERP_SCOLAIRE";
    public bool CreateNetworkShare { get; set; } = true;
    public string ShareName { get; set; } = "ERP_Dossiers";

    // Client
    public string ApiBaseUrl { get; set; } = "http://localhost:5096/";
    public string CloudApiUrl { get; set; } = "http://169.58.93.203:1804";

    public bool OpenFirewall { get; set; } = true;
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool StartAfterInstall { get; set; } = true;
    public bool ApplyVirginDatabase { get; set; } = true;
}

public sealed class InstallerEngine
{
    public const string ServiceName = "ErpScolaireApi";
    public const int ApiPort = 5096;
    public const int ApiPortAlt = 5041;

    private readonly Action<string> _log;

    public InstallerEngine(Action<string> log) => _log = log;

    public static string FindPayloadRoot()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var candidates = new[]
        {
            Path.Combine(baseDir, "payload"),
            Path.Combine(baseDir, "..", "payload"),
            Path.Combine(Directory.GetCurrentDirectory(), "payload"),
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (Directory.Exists(Path.Combine(full, "desktop")) &&
                Directory.Exists(Path.Combine(full, "api")))
            {
                return full;
            }
        }

        throw new DirectoryNotFoundException(
            "Dossier payload introuvable (desktop/ + api/). Lancez scripts/build-setup.ps1 avant d'utiliser le Setup.");
    }

    public static IReadOnlyList<string> DetectSqlInstances()
    {
        var list = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost", @".\SQLEXPRESS" };
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    list.Add(name.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase)
                        ? "localhost"
                        : $@"localhost\{name}");
                }
            }
        }
        catch
        {
            // ignore
        }

        return list.ToList();
    }

    public async Task<string> TestSqlAsync(InstallOptions opt, CancellationToken ct = default)
    {
        var cs = BuildSqlConnectionString(opt, "master");
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT @@SERVERNAME";
        var server = (string?)await cmd.ExecuteScalarAsync(ct) ?? "?";
        return server;
    }

    public async Task<string> TestCloudSqlAsync(InstallOptions opt, CancellationToken ct = default)
    {
        var cs = BuildCloudSqlConnectionString(opt);
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT DB_NAME()";
        var db = (string?)await cmd.ExecuteScalarAsync(ct) ?? opt.CloudDatabase;
        return $"{opt.CloudSqlServer} / {db}";
    }

    public async Task ProbeStorageAsync(string storageRoot, CancellationToken ct = default)
    {
        Directory.CreateDirectory(storageRoot);
        var probe = Path.Combine(storageRoot, $".erp_probe_{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(probe, "ok", ct);
        File.Delete(probe);
    }

    /// <returns><c>true</c> si la base vient d'être créée ; <c>false</c> si elle existait déjà.</returns>
    public async Task<bool> EnsureDatabaseExistsAsync(InstallOptions opt, CancellationToken ct = default)
    {
        _log("[DB] Création/vérification de la base...");
        var cs = BuildSqlConnectionString(opt, "master");
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);
        await using (var check = cn.CreateCommand())
        {
            check.CommandText = $"SELECT CASE WHEN DB_ID(N'{EscapeSql(opt.Database)}') IS NULL THEN 0 ELSE 1 END";
            var exists = Convert.ToInt32(await check.ExecuteScalarAsync(ct)) == 1;
            if (exists)
            {
                _log($"Base SQL déjà présente : {opt.Database}");
                return false;
            }
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $"CREATE DATABASE [{opt.Database.Replace("]", "]]")}];";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _log($"Base SQL créée : {opt.Database}");
        return true;
    }

    /// <summary>
    /// Le service Windows tourne en LocalSystem : il faut un login SQL pour Integrated Security.
    /// </summary>
    public async Task EnsureSystemSqlAccessAsync(InstallOptions opt, CancellationToken ct = default)
    {
        var db = opt.Database.Replace("]", "]]");
        var cs = BuildSqlConnectionString(opt, "master");
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'NT AUTHORITY\SYSTEM')
  CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $@"
USE [{db}];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'NT AUTHORITY\SYSTEM')
  CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
IF IS_ROLEMEMBER(N'db_owner', N'NT AUTHORITY\SYSTEM') = 0
  ALTER ROLE [db_owner] ADD MEMBER [NT AUTHORITY\SYSTEM];";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _log("Accès SQL accordé à NT AUTHORITY\\SYSTEM (service Windows).");
    }

    public async Task InstallAsync(InstallOptions opt, CancellationToken ct = default)
    {
        var payload = FindPayloadRoot();
        Directory.CreateDirectory(opt.InstallRoot);

        if (opt.Role == InstallRole.Server)
        {
            await InstallServerAsync(opt, payload, ct);
        }
        else
        {
            await InstallClientAsync(opt, payload, ct);
        }

        if (opt.CreateDesktopShortcut)
        {
            CreateShortcut(
                Path.Combine(opt.InstallRoot, "Desktop", "SchoolManagement.Desktop.exe"),
                "ERP Scolaire");
        }

        _log("Installation terminée.");
    }

    private async Task InstallServerAsync(InstallOptions opt, string payload, CancellationToken ct)
    {
        _log("Mode SERVEUR — déploiement API + Desktop + service Windows…");

        var apiDir = Path.Combine(opt.InstallRoot, "Api");
        var desktopDir = Path.Combine(opt.InstallRoot, "Desktop");

        await WindowsServiceLifecycle.ReleaseServerPayloadLocksAsync(_log, ct);

        CopyDirectory(Path.Combine(payload, "api"), apiDir, overwrite: true);
        CopyDirectory(Path.Combine(payload, "desktop"), desktopDir, overwrite: true);
        UnblockFiles(apiDir);
        UnblockFiles(desktopDir);
        _log("Fichiers copiés.");

        var databaseCreated = await EnsureDatabaseExistsAsync(opt, ct);
        if (opt.UseWindowsAuth)
            await EnsureSystemSqlAccessAsync(opt, ct);
        WriteServeurDonnees(apiDir, opt);
        WriteServeurDonnees(desktopDir, opt);
        WriteApiAppsettings(apiDir, opt);
        var storageRoot = EnsureFileStorage(opt, apiDir, desktopDir);
        if (opt.ConfigureCloudSync)
            WriteServeurDonneesCloud(apiDir, opt);
        WriteDesktopAppsettings(desktopDir, "http://127.0.0.1:5096/", opt.CloudApiUrl, clientMode: false);

        if (opt.OpenFirewall)
        {
            OpenFirewallPort(ApiPort, "ERP Scolaire API");
            OpenFirewallPort(ApiPortAlt, "ERP Scolaire API Alt");
        }

        await ApplyDatabaseBaselineAsync(opt, ct);

        // Pré-contrôle API obligatoire : exécute les SchemaInitializers du démarrage API
        // (mécanisme officiel d'évolution), même si StartAfterInstall = false.
        // Database.Migrate() n'est pas utilisé.
        await EnsureApiCanStartAsync(apiDir, opt, storageRoot, ct);
        await ApplyPostBaselineSchemaUpgradesAsync(opt, ct);

        // 010 : uniquement réinstall sur une base déjà existante, jamais sur CREATE DATABASE.
        if (opt.ApplyVirginDatabase && !databaseCreated)
            await ApplyVirginPurgeAsync(opt, ct);

        await InstallOrUpdateServiceAsync(apiDir, opt, storageRoot, ct);
        if (opt.StartAfterInstall)
        {
            StartService(apiDir);
            await WaitForHealthAsync(
                ["http://127.0.0.1:5096/api/health", "http://127.0.0.1:5096/api/v1/health"],
                ct);
            _log("[SERVICE] Health API 200.");
            _log("API répond sur http://127.0.0.1:5096");
            await RunFinalVerificationAsync(opt, storageRoot, ct);
            TryStart(Path.Combine(desktopDir, "SchoolManagement.Desktop.exe"));
        }

        _log("Installation serveur terminée avec succès.");
    }

    private async Task InstallClientAsync(InstallOptions opt, string payload, CancellationToken ct)
    {
        _log("Mode CLIENT — déploiement Desktop uniquement…");
        var desktopDir = Path.Combine(opt.InstallRoot, "Desktop");

        await WindowsServiceLifecycle.ReleaseClientPayloadLocksAsync(_log, ct);

        CopyDirectory(Path.Combine(payload, "desktop"), desktopDir, overwrite: true);
        UnblockFiles(desktopDir);

        var apiUrl = string.IsNullOrWhiteSpace(opt.ApiBaseUrl)
            ? "http://localhost:5096/"
            : NormalizeUrl(opt.ApiBaseUrl);

        if (apiUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
            apiUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            var discovered = await TryDiscoverLocalApiAsync(ct);
            if (!string.IsNullOrWhiteSpace(discovered))
            {
                apiUrl = discovered;
                _log($"Serveur école découvert : {apiUrl}");
            }
        }

        WriteDesktopAppsettings(desktopDir, apiUrl, opt.CloudApiUrl, clientMode: true);

        if (opt.StartAfterInstall)
        {
            TryStart(Path.Combine(desktopDir, "SchoolManagement.Desktop.exe"));
        }

        await Task.CompletedTask;
    }

    private static async Task<string?> TryDiscoverLocalApiAsync(CancellationToken ct)
    {
        // Scan léger des gateway /24 courantes sur le port 5096.
        var prefixes = new HashSet<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var ip = ua.Address.ToString();
                if (ip.StartsWith("127.")) continue;
                var parts = ip.Split('.');
                if (parts.Length == 4)
                    prefixes.Add($"{parts[0]}.{parts[1]}.{parts[2]}");
            }
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(400) };
        foreach (var prefix in prefixes.Take(2))
        {
            var tasks = new List<Task<(string url, bool ok)>>();
            for (var i = 1; i <= 254; i++)
            {
                var url = $"http://{prefix}.{i}:{ApiPort}";
                tasks.Add(ProbeAsync(http, url, ct));
            }

            while (tasks.Count > 0)
            {
                var done = await Task.WhenAny(tasks);
                tasks.Remove(done);
                var (url, ok) = await done;
                if (ok) return NormalizeUrl(url);
            }
        }

        return null;
    }

    private static async Task<(string url, bool ok)> ProbeAsync(HttpClient http, string baseUrl, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync($"{baseUrl}/api/health", ct);
            return (baseUrl, resp.IsSuccessStatusCode);
        }
        catch
        {
            return (baseUrl, false);
        }
    }

    /// <summary>
    /// Lance l'API en console jusqu'au health (SchemaInitializers + SecurityEngine Phase 0)
    /// pour détecter Smart App Control / erreurs fatales avant le service Windows.
    /// </summary>
    private async Task EnsureApiCanStartAsync(string apiDir, InstallOptions opt, string storageRoot, CancellationToken ct)
    {
        var exe = Path.Combine(apiDir, "SchoolManagement.API.exe");
        var outLog = Path.Combine(apiDir, "logs", "preflight-out.log");
        var errLog = Path.Combine(apiDir, "logs", "preflight-err.log");
        Directory.CreateDirectory(Path.GetDirectoryName(outLog)!);
        try { File.Delete(outLog); } catch { /* ignore */ }
        try { File.Delete(errLog); } catch { /* ignore */ }

        var timeout = ApiStartupWait.ResolveTimeout();
        _log($"Pré-contrôle : démarrage test de l'API (attente max {ApiStartupWait.Format(timeout)})…");
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = apiDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["DOTNET_ENVIRONMENT"] = "Production";
        psi.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:5096";
        psi.Environment["SEED_DATABASE"] = "true";
        psi.Environment["ALLOW_DEMO_SEED"] = "false";
        psi.Environment["ConnectionStrings__Default"] = BuildSqlConnectionString(opt, opt.Database);
        psi.Environment["FILE_STORAGE_ROOT"] = storageRoot;

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Impossible de lancer SchoolManagement.API.exe pour le pré-contrôle.");

        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var wait = await ApiStartupWait.WaitAsync(
            ct2 => ApiStartupWait.ProbeUrlsAsync(
                http,
                ["http://127.0.0.1:5096/api/health", "http://127.0.0.1:5096/api/v1/health"],
                ct2),
            () => !p.HasExited,
            _log,
            timeout,
            cancellationToken: ct);

        var stdout = "";
        var stderr = "";
        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000);
            }
        }
        catch { /* ignore */ }

        try { stdout = await stdoutTask; } catch { /* ignore */ }
        try { stderr = await stderrTask; } catch { /* ignore */ }
        try { await File.WriteAllTextAsync(outLog, stdout, ct); } catch { /* ignore */ }
        try { await File.WriteAllTextAsync(errLog, stderr, ct); } catch { /* ignore */ }

        var combined = (stdout + Environment.NewLine + stderr);
        if (combined.Contains("0x800711C7", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("stratégie de contrôle", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("application control", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("FileLoadException", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Windows Smart App Control bloque l'API (DLL non signées, erreur 0x800711C7).\n\n" +
                "À faire sur cette machine :\n" +
                "1. Paramètres Windows → Confidentialité et sécurité → Sécurité Windows\n" +
                "2. Contrôle des applications et du navigateur\n" +
                "3. Paramètres du Contrôle d'applications intelligent (Smart App Control)\n" +
                "4. Choisir « Désactivé »\n" +
                "5. Redémarrer le PC, puis relancer ce Setup.\n\n" +
                "Sans cela, le service ErpScolaireApi ne peut pas démarrer.");
        }

        if (!wait.Healthy)
        {
            var tail = combined.Length > 1200 ? combined[^1200..] : combined;
            throw new InvalidOperationException(
                "L'API n'est pas prête (pré-contrôle).\n" +
                wait.Reason + "\n\n" + tail);
        }

        await Task.Delay(1500, ct);
        _log("Pré-contrôle OK.");
    }

    private async Task InstallOrUpdateServiceAsync(string apiDir, InstallOptions opt, string storageRoot, CancellationToken ct)
    {
        var exe = Path.Combine(apiDir, "SchoolManagement.API.exe");
        if (!File.Exists(exe))
            throw new FileNotFoundException("SchoolManagement.API.exe introuvable dans le payload API.", exe);

        Directory.CreateDirectory(Path.Combine(apiDir, "logs"));

        try
        {
            var registration = WindowsServiceLifecycle.ProbeRegistration(ServiceName);
            _log($"[SERVICE] État SCM avant recréation : {registration}.");

            if (registration != ServiceRegistrationState.Absent)
            {
                await WindowsServiceLifecycle.StopServiceAndWaitAsync(ServiceName, _log, ct);
                await WindowsServiceLifecycle.DeleteServiceAndWaitAsync(ServiceName, _log, ct);
            }
            else
            {
                _log("[SERVICE] Aucun service existant — première installation.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not System.TimeoutException)
        {
            throw new InvalidOperationException($"Échec préparation service {ServiceName}.", ex);
        }

        await CreateServiceUntilRegisteredAsync(exe, ct);
        EnsureQuotedBinaryPathName(exe);

        var cs = BuildSqlConnectionString(opt, opt.Database);
        var regPath = $@"SYSTEM\CurrentControlSet\Services\{ServiceName}";
        using (var key = Registry.LocalMachine.OpenSubKey(regPath, writable: true))
        {
            if (key == null)
                throw new InvalidOperationException("Service créé mais clé registre introuvable.");

            key.SetValue("Description", "API locale ERP Administration Scolaire RDC");
            key.SetValue("Environment", new[]
            {
                "ASPNETCORE_ENVIRONMENT=Production",
                "DOTNET_ENVIRONMENT=Production",
                $"ASPNETCORE_URLS=http://0.0.0.0:{ApiPort};http://0.0.0.0:{ApiPortAlt}",
                $"ConnectionStrings__Default={cs}",
                $"FILE_STORAGE_ROOT={storageRoot}",
                "SEED_DATABASE=true",
                "ALLOW_DEMO_SEED=false",
            }, RegistryValueKind.MultiString);
        }

        RunProcess("sc.exe", $"failure {ServiceName} reset= 86400 actions= restart/5000/restart/5000/restart/5000", throwOnError: false);
        RunProcess("sc.exe", $"config {ServiceName} start= auto", throwOnError: false);
        _log("[SERVICE] Service créé.");
    }

    private async Task CreateServiceUntilRegisteredAsync(string exe, CancellationToken ct)
    {
        var createArgs = BuildScCreateArgs(exe);
        for (var attempt = 1; attempt <= WindowsServiceLifecycle.MaxCreateAttempts; attempt++)
        {
            if (WindowsServiceLifecycle.ServiceRegistryKeyExists(ServiceName))
            {
                _log("[SERVICE] Clé registre encore présente — attente de disparition avant create...");
                var previousForbid = WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait;
                WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = true;
                try
                {
                    await WindowsServiceLifecycle.WaitUntilServiceAbsentAsync(
                        ServiceName,
                        _log,
                        ct,
                        exists: WindowsServiceLifecycle.ServiceRegistryKeyExists);
                }
                finally
                {
                    WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = previousForbid;
                }
            }

            _log($"[SERVICE] Création {ServiceName} (tentative {attempt}/{WindowsServiceLifecycle.MaxCreateAttempts})...");
            _log($"sc.exe {createArgs}");
            var (exitCode, output) = WindowsServiceLifecycle.RunSc(createArgs);
            if (!string.IsNullOrWhiteSpace(output))
                _log(output);

            if (exitCode == 0)
                return;

            if (WindowsServiceLifecycle.IsMarkedForDeleteError(exitCode, output))
            {
                _log("[SERVICE] DeleteService = 1072 : service déjà marqué pour suppression.");
                var previousForbid = WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait;
                WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = true;
                try
                {
                    await WindowsServiceLifecycle.WaitUntilServiceAbsentAsync(
                        ServiceName,
                        _log,
                        ct,
                        exists: WindowsServiceLifecycle.ServiceRegistryKeyExists,
                        alreadyMarkedBeforeThisCall: true);
                }
                finally
                {
                    WindowsServiceLifecycle.ForbidServiceControllerDuringDeleteWait = previousForbid;
                }
                continue;
            }

            throw new InvalidOperationException(
                $"sc.exe {createArgs} → code {exitCode}{Environment.NewLine}{output}");
        }

        throw new InvalidOperationException(
            $"Impossible de créer {ServiceName} après {WindowsServiceLifecycle.MaxCreateAttempts} tentatives (ERROR_SERVICE_MARKED_FOR_DELETE / 1072).");
    }

    /// <summary>
    /// Construit les arguments <c>sc create</c> pour que BINARY_PATH_NAME conserve les guillemets
    /// lorsque le chemin contient des espaces (ex. Program Files\ERP Scolaire).
    /// </summary>
    internal static string BuildScCreateArgs(string exeFullPath)
    {
        // Résultat sc : binPath= "\"C:\Program Files\ERP Scolaire\Api\SchoolManagement.API.exe\""
        return $"create {ServiceName} binPath= \"\\\"{exeFullPath}\\\"\" start= auto DisplayName= \"ERP Scolaire API\" obj= LocalSystem";
    }

    private void EnsureQuotedBinaryPathName(string exeFullPath)
    {
        var qc = RunProcess("sc.exe", $"qc {ServiceName}", throwOnError: false);
        _log(qc.Trim());
        var binaryPath = ParseScQcBinaryPathName(qc);
        var expectedQuoted = $"\"{exeFullPath}\"";

        if (string.Equals(binaryPath, expectedQuoted, StringComparison.OrdinalIgnoreCase))
            return;

        // Filet de sécurité : forcer ImagePath quoté dans le registre puis re-vérifier.
        if (exeFullPath.Contains(' ', StringComparison.Ordinal) ||
            !string.Equals(binaryPath?.Trim('"'), exeFullPath, StringComparison.OrdinalIgnoreCase))
        {
            _log("BINARY_PATH_NAME incorrect après sc create — correction registre ImagePath…");
            var regPath = $@"SYSTEM\CurrentControlSet\Services\{ServiceName}";
            using (var key = Registry.LocalMachine.OpenSubKey(regPath, writable: true))
            {
                if (key == null)
                    throw new InvalidOperationException("Clé registre service introuvable pour corriger ImagePath.");
                key.SetValue("ImagePath", expectedQuoted, RegistryValueKind.ExpandString);
            }

            qc = RunProcess("sc.exe", $"qc {ServiceName}", throwOnError: false);
            _log(qc.Trim());
            binaryPath = ParseScQcBinaryPathName(qc);
        }

        if (!string.Equals(binaryPath, expectedQuoted, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Le BINARY_PATH_NAME du service n'est pas correctement quoté (chemins avec espaces).\n" +
                $"Attendu : {expectedQuoted}\n" +
                $"Obtenu  : {binaryPath ?? "(vide)"}\n\n" +
                qc);
        }

        _log($"BINARY_PATH_NAME OK : {binaryPath}");
    }

    internal static string? ParseScQcBinaryPathName(string scQcOutput)
    {
        foreach (var raw in scQcOutput.Split('\n'))
        {
            var line = raw.Trim();
            // BINARY_PATH_NAME   : "C:\Program Files\..."
            const string marker = "BINARY_PATH_NAME";
            var idx = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var colon = line.IndexOf(':', idx + marker.Length);
            if (colon < 0) continue;
            return line[(colon + 1)..].Trim();
        }

        return null;
    }

    private void StartService(string apiDir)
    {
        using var sc = new ServiceController(ServiceName);
        sc.Refresh();
        _log($"[SERVICE] Démarrage {ServiceName} (état initial : {sc.Status})...");

        if (sc.Status == ServiceControllerStatus.Running)
        {
            _log("[SERVICE] Service RUNNING.");
            return;
        }

        try
        {
            sc.Start();
            var timeout = ApiStartupWait.ResolveTimeout();
            var deadline = DateTime.UtcNow + timeout;
            ServiceControllerStatus? lastLogged = null;
            var lastProgress = DateTime.UtcNow;
            while (DateTime.UtcNow < deadline)
            {
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    _log("[SERVICE] Service RUNNING.");
                    return;
                }

                if (lastLogged != sc.Status)
                {
                    _log($"[SERVICE] En attente de Running... (actuel : {sc.Status})");
                    lastLogged = sc.Status;
                }
                else if (DateTime.UtcNow - lastProgress >= TimeSpan.FromSeconds(ApiStartupWait.DefaultProgressLogSeconds))
                {
                    lastProgress = DateTime.UtcNow;
                    _log($"[SERVICE] Toujours en démarrage ({sc.Status}) — initialisation API possible lente.");
                }

                Thread.Sleep(2000);
            }

            sc.Refresh();
            if (sc.Status != ServiceControllerStatus.Running)
                throw new System.TimeoutException(
                    $"Le service {ServiceName} n'est pas Running après {ApiStartupWait.Format(timeout)} (état : {sc.Status}).");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(BuildServiceStartFailureMessage(ex, apiDir), ex);
        }
    }

    private string BuildServiceStartFailureMessage(Exception ex, string apiDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Impossible de démarrer le service ErpScolaireApi.");
        sb.AppendLine(ex.Message);

        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            sb.AppendLine($"InnerException: {inner.GetType().Name}: {inner.Message}");
            if (inner is System.ComponentModel.Win32Exception win32)
                sb.AppendLine($"Win32 NativeErrorCode: {win32.NativeErrorCode}");
        }

        if (ex is System.ComponentModel.Win32Exception directWin32)
            sb.AppendLine($"Win32 NativeErrorCode: {directWin32.NativeErrorCode}");

        try
        {
            using var statusSc = new ServiceController(ServiceName);
            sb.AppendLine($"État service après échec: {statusSc.Status}");
        }
        catch (Exception statusEx)
        {
            sb.AppendLine($"État service: indisponible ({statusEx.Message})");
        }

        try
        {
            var qc = RunProcess("sc.exe", $"qc {ServiceName}", throwOnError: false);
            var binaryPath = ParseScQcBinaryPathName(qc);
            sb.AppendLine($"BINARY_PATH_NAME: {binaryPath ?? "(introuvable)"}");
            if (!string.IsNullOrWhiteSpace(qc))
            {
                sb.AppendLine("--- sc.exe qc ---");
                sb.AppendLine(qc.Trim());
            }
        }
        catch (Exception qcEx)
        {
            sb.AppendLine($"sc.exe qc: {qcEx.Message}");
        }

        var scmHint = ReadServiceControlManagerEvents();
        if (!string.IsNullOrWhiteSpace(scmHint))
        {
            sb.AppendLine("--- Service Control Manager (récent) ---");
            sb.AppendLine(scmHint);
        }

        var appHint = ReadApplicationFailureHint();
        if (!string.IsNullOrWhiteSpace(appHint))
        {
            sb.AppendLine("--- Journal Application (récent) ---");
            sb.AppendLine(appHint);
        }

        var logTail = ReadApiLogTails(apiDir);
        if (!string.IsNullOrWhiteSpace(logTail))
        {
            sb.AppendLine("--- Api\\logs ---");
            sb.AppendLine(logTail);
        }

        return sb.ToString().TrimEnd();
    }

    private static string ReadApiLogTails(string apiDir)
    {
        try
        {
            var logsDir = Path.Combine(apiDir, "logs");
            if (!Directory.Exists(logsDir))
                return "(dossier Api\\logs absent)";

            var files = new[]
                {
                    "preflight-err.log",
                    "preflight-out.log",
                }
                .Select(n => Path.Combine(logsDir, n))
                .Concat(Directory.GetFiles(logsDir, "api-*.log")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Take(2))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
                return "(aucun preflight-*.log / api-*.log)";

            var sb = new StringBuilder();
            foreach (var file in files)
            {
                sb.AppendLine($"[{Path.GetFileName(file)}]");
                try
                {
                    var text = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine("(vide)");
                        continue;
                    }

                    sb.AppendLine(text.Length > 1500 ? text[^1500..] : text);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"(lecture impossible: {ex.Message})");
                }
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"(logs: {ex.Message})";
        }
    }

    private static string ReadServiceControlManagerEvents()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wevtutil.exe",
                Arguments =
                    "qe System /c:12 /rd:true /f:text /q:\"*[System[Provider[@Name='Service Control Manager'] and TimeCreated[timediff(@SystemTime) <= 600000]]]\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return "";
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15_000);
            if (string.IsNullOrWhiteSpace(output)) return "";

            var lines = output.Split('\n')
                .Select(l => l.TrimEnd())
                .Where(l => l.Contains("ErpScolaire", StringComparison.OrdinalIgnoreCase)
                            || l.Contains("ERP Scolaire", StringComparison.OrdinalIgnoreCase)
                            || l.Contains("SchoolManagement", StringComparison.OrdinalIgnoreCase)
                            || l.Contains("7000", StringComparison.Ordinal)
                            || l.Contains("7009", StringComparison.Ordinal)
                            || l.Contains("7023", StringComparison.Ordinal)
                            || l.Contains("7024", StringComparison.Ordinal))
                .Take(40);
            return string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return "";
        }
    }

    private static string ReadApplicationFailureHint()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wevtutil.exe",
                Arguments =
                    "qe Application /c:12 /rd:true /f:text /q:\"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= 600000]]]\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return "";
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15_000);
            if (string.IsNullOrWhiteSpace(output)) return "";

            var lines = output.Split('\n')
                .Select(l => l.TrimEnd())
                .Where(l => l.Contains("ErpScolaire", StringComparison.OrdinalIgnoreCase)
                            || l.Contains("SchoolManagement", StringComparison.OrdinalIgnoreCase)
                            || l.Contains(".NET Runtime", StringComparison.OrdinalIgnoreCase)
                            || l.Contains("Application Error", StringComparison.OrdinalIgnoreCase)
                            || l.Contains("cannot start", StringComparison.OrdinalIgnoreCase))
                .Take(30);
            return string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return "";
        }
    }

    private string EnsureFileStorage(InstallOptions opt, string apiDir, string desktopDir)
    {
        var root = string.IsNullOrWhiteSpace(opt.StorageRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ERP Scolaire",
                "Dossier_Eleve")
            : opt.StorageRoot.Trim();

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Eleves"));
        Directory.CreateDirectory(Path.Combine(root, "Documents"));
        Directory.CreateDirectory(Path.Combine(root, "Branding"));

        // Permissions lecture/écriture Users + SYSTEM
        RunProcess("icacls.exe", $"\"{root}\" /grant *S-1-5-32-545:(OI)(CI)M /T /C /Q", throwOnError: false);
        RunProcess("icacls.exe", $"\"{root}\" /grant *S-1-5-18:(OI)(CI)F /T /C /Q", throwOnError: false);

        WriteServeurFichiers(apiDir, root);
        WriteServeurFichiers(desktopDir, root);
        _log($"Dossier fichiers : {root}");

        if (opt.CreateNetworkShare)
        {
            if (!NetworkShareCommands.TryResolveLocalPath(root, out var localSharePath, out var resolveError))
            {
                throw new InvalidOperationException(
                    "Impossible de créer le partage réseau : chemin local introuvable." +
                    Environment.NewLine + resolveError +
                    Environment.NewLine + $"Dossier configuré : {root}");
            }

            var deleteArgs = NetworkShareCommands.BuildDeleteArguments(opt.ShareName);
            var (deleteCode, deleteOut) = RunProcessEx("net.exe", deleteArgs);
            if (deleteCode != 0 && !string.IsNullOrWhiteSpace(deleteOut))
                _log($"net.exe {deleteArgs} → code {deleteCode} (info) : {deleteOut.Trim()}");

            var createArgs = NetworkShareCommands.BuildCreateArguments(opt.ShareName, localSharePath);
            _log($"net.exe {createArgs}");
            var (createCode, createOut) = RunProcessEx("net.exe", createArgs);
            if (createCode != 0)
            {
                throw new InvalidOperationException(
                    "Échec net share (création du partage)." + Environment.NewLine +
                    $"Commande : net.exe {createArgs}" + Environment.NewLine +
                    $"Chemin local : {localSharePath}" + Environment.NewLine +
                    $"Code retour : {createCode}" + Environment.NewLine +
                    createOut);
            }

            if (!string.IsNullOrWhiteSpace(createOut))
                _log(createOut.Trim());
            _log($"Partage réseau : {NetworkShareCommands.BuildUncAccessPath(opt.ShareName)} (local : {localSharePath})");
        }

        return root;
    }

    private void WriteServeurDonneesCloud(string apiDir, InstallOptions opt)
    {
        var encrypted = SetupDpapi.Encrypt(opt.CloudSqlPassword);
        var sb = new StringBuilder();
        sb.AppendLine("#######################################################");
        sb.AppendLine("# ERP SCOLAIRE RDC - genere par Setup");
        sb.AppendLine("# Configuration SQL Server DISTANT (cloud)");
        sb.AppendLine("#######################################################");
        sb.AppendLine("ACTIF=1");
        sb.AppendLine($"INTERVALLE_MINUTES={Math.Clamp(opt.CloudSyncIntervalMinutes, 1, 1440)}");
        sb.AppendLine($"SERVEUR={opt.CloudSqlServer.Trim()}");
        sb.AppendLine($"PORT={opt.CloudSqlPort}");
        sb.AppendLine($"BASE={opt.CloudDatabase.Trim()}");
        sb.AppendLine("AUTHENTIFICATION=SQL");
        sb.AppendLine($"UTILISATEUR={opt.CloudSqlUser.Trim()}");
        sb.AppendLine($"MOTDEPASSE={encrypted}");
        File.WriteAllText(Path.Combine(apiDir, "ServeurDonneesCloud.txt"), sb.ToString(), Encoding.UTF8);
        _log("ServeurDonneesCloud.txt écrit (ACTIF=1, mot de passe DPAPI).");
    }

    private const string BaselineSqlFileName = "001_InitialCreate_EF.sql";
    private const string InitialCreateMigrationId = "20260706114538_InitialCreate";

    /// <summary>
    /// Pose le schéma cœur (dont dbo.Schools) via 001_InitialCreate_EF.sql.
    /// Cette baseline est une snapshot historique immuable de InitialCreate.
    /// Les évolutions suivantes passent par les SchemaInitializers (API + ApplyCriticalSchemaUpgradesAsync),
    /// jamais par Database.Migrate().
    /// </summary>
    private async Task ApplyDatabaseBaselineAsync(InstallOptions opt, CancellationToken ct)
    {
        _log("[DB] Application de la baseline SQL...");
        _log($"[DB] Baseline : {BaselineSqlFileName}");

        var scriptPath = FindBaselineSqlScript();
        string sql;
        try
        {
            sql = await File.ReadAllTextAsync(scriptPath, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[DB] Impossible de lire le script baseline '{BaselineSqlFileName}'.{Environment.NewLine}{ex.Message}",
                ex);
        }

        var cs = BuildSqlConnectionString(opt, opt.Database);
        await using var cn = new SqlConnection(cs);
        try
        {
            await cn.OpenAsync(ct);
        }
        catch (Exception ex)
        {
            throw WrapBaselineSqlError(ex, "ouverture de la connexion (base cible)", 0);
        }

        var batches = SplitSqlBatches(sql);
        var batchIndex = 0;
        foreach (var batch in batches)
        {
            batchIndex++;
            try
            {
                await using var cmd = cn.CreateCommand();
                cmd.CommandTimeout = 180;
                // Index filtrés / vues de 001 exigent QUOTED_IDENTIFIER ON (sqlcmd le coupe par défaut).
                cmd.CommandText = "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;" + Environment.NewLine + batch;
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                try
                {
                    await using var rollback = cn.CreateCommand();
                    rollback.CommandText = "IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;";
                    await rollback.ExecuteNonQueryAsync(ct);
                }
                catch
                {
                    // ignore rollback secondary failure
                }

                throw WrapBaselineSqlError(ex, $"lot SQL {batchIndex}/{batches.Count}", batchIndex);
            }
        }

        _log("[DB] Baseline SQL appliquée avec succès.");
        _log("[DB] Vérification de la table Schools...");
        await VerifyDatabaseBaselineAsync(cn, ct);
        _log("[DB] Vérification de la baseline terminée.");
    }

    internal static SchoolDbContext CreateMigrationDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString, sql =>
            {
                sql.CommandTimeout(180);
                sql.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .Options;

        return new SchoolDbContext(options)
        {
            IgnoreSchoolScope = true,
            SuppressCloudSyncEnqueue = true
        };
    }

    /// <summary>
    /// Interdit. Le déploiement officiel est baseline + SchemaInitializers.
    /// Les migrations EF ne doivent pas être exécutées par le Setup.
    /// </summary>
    [Obsolete("Database.Migrate() est interdit dans le chemin Setup/API. Utiliser les SchemaInitializers.")]
    internal static Task<IReadOnlyList<string>> ApplyPendingEfMigrationsAsync(
        string connectionString,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        _ = connectionString;
        _ = log;
        _ = cancellationToken;
        throw new InvalidOperationException(
            "Database.Migrate() est interdit dans le chemin Setup/API actuel. " +
            "Le schéma évolue uniquement via 001_InitialCreate_EF.sql (baseline) " +
            "et les SchemaInitializers (voir SchemaDeploymentCoverage).");
    }

    internal static async Task ApplyCriticalSchemaUpgradesAsync(
        string connectionString,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var registrationSchema = new RegistrationNumberCounterSchemaInitializer(
            connectionString,
            NullLogger<RegistrationNumberCounterSchemaInitializer>.Instance);
        await registrationSchema.EnsureCreatedAsync(cancellationToken);

        var userRoleAssignmentSchema = new UserRoleAssignmentSchemaInitializer(
            connectionString,
            NullLogger<UserRoleAssignmentSchemaInitializer>.Instance);
        await userRoleAssignmentSchema.EnsureUpdatedAsync(cancellationToken);

        log?.Invoke("[DB] Initializers critiques post-baseline appliqués.");
    }

    private async Task ApplyPostBaselineSchemaUpgradesAsync(InstallOptions opt, CancellationToken ct)
    {
        _log("[DB] Application des compléments de schéma post-baseline...");
        var connectionString = BuildSqlConnectionString(opt, opt.Database);
        await ApplyCriticalSchemaUpgradesAsync(connectionString, _log, ct);

        _log("[DB] Compléments de schéma post-baseline appliqués.");
    }

    private async Task VerifyDatabaseBaselineAsync(SqlConnection cn, CancellationToken ct)
    {
        async Task<int> ScalarAsync(string commandText)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = commandText;
            var value = await cmd.ExecuteScalarAsync(ct);
            return value is null or DBNull ? 0 : Convert.ToInt32(value);
        }

        if (await ScalarAsync("SELECT CASE WHEN OBJECT_ID(N'dbo.Schools', N'U') IS NULL THEN 0 ELSE 1 END") != 1)
        {
            throw new InvalidOperationException(
                "[DB] Vérification baseline échouée : la table dbo.Schools est absente après 001_InitialCreate_EF.sql. Installation arrêtée.");
        }

        if (await ScalarAsync("SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END") != 1)
        {
            throw new InvalidOperationException(
                "[DB] Vérification baseline échouée : dbo.__EFMigrationsHistory est absente. Installation arrêtée.");
        }

        if (await ScalarAsync(
                $"SELECT COUNT(1) FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'{EscapeSql(InitialCreateMigrationId)}'") < 1)
        {
            throw new InvalidOperationException(
                $"[DB] Vérification baseline échouée : la migration {InitialCreateMigrationId} est absente de __EFMigrationsHistory. Installation arrêtée.");
        }
    }

    private static List<string> SplitSqlBatches(string sql)
    {
        var raw = System.Text.RegularExpressions.Regex.Split(
            sql,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var batches = new List<string>();
        foreach (var batch in raw)
        {
            var text = batch.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"^\s*USE\s+\[[^\]]+\]\s*;?\s*",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
            text = text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            batches.Add(text);
        }

        return batches;
    }

    private static Exception WrapBaselineSqlError(Exception ex, string step, int batchIndex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[DB] Échec de la baseline SQL.");
        sb.AppendLine($"Script : {BaselineSqlFileName}");
        sb.AppendLine($"Étape : {step}");
        if (ex is SqlException sql)
        {
            sb.AppendLine($"Erreur SQL n° {sql.Number} (état {sql.State}, classe {sql.Class})");
            sb.AppendLine(sql.Message);
            if (sql.Errors is { Count: > 0 })
            {
                foreach (SqlError err in sql.Errors)
                {
                    if (!string.Equals(err.Message, sql.Message, StringComparison.Ordinal))
                        sb.AppendLine($"  [{err.Number}] {err.Message}");
                }
            }
        }
        else
        {
            sb.AppendLine(ex.Message);
        }

        if (batchIndex > 0)
            sb.AppendLine($"Lot : {batchIndex}");

        return new InvalidOperationException(sb.ToString().TrimEnd(), ex);
    }

    private static string FindBaselineSqlScript()
    {
        var payload = FindPayloadRoot();
        var candidates = new[]
        {
            Path.Combine(payload, "sql", BaselineSqlFileName),
            Path.Combine(AppContext.BaseDirectory, "sql", BaselineSqlFileName),
            Path.Combine(AppContext.BaseDirectory, "payload", "sql", BaselineSqlFileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "scripts", BaselineSqlFileName)),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        throw new FileNotFoundException(
            $"Script {BaselineSqlFileName} introuvable dans payload/sql/. Relancez scripts/build-setup.ps1.");
    }

    private async Task ApplyVirginPurgeAsync(InstallOptions opt, CancellationToken ct)
    {
        _log("Application de la base Production vierge (purge métier)…");
        var script = FindVirginSqlScript();
        var sql = await File.ReadAllTextAsync(script, ct);
        // Exécuter batch par batch (séparateur GO)
        var batches = System.Text.RegularExpressions.Regex.Split(
            sql, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var cs = BuildSqlConnectionString(opt, opt.Database);
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);
        foreach (var batch in batches)
        {
            var text = batch.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            // Retirer USE [...] si présent — déjà connectés sur la bonne base
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"^\s*USE\s+\[[^\]]+\]\s*;?\s*", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
            if (string.IsNullOrWhiteSpace(text)) continue;

            await using var cmd = cn.CreateCommand();
            cmd.CommandTimeout = 180;
            cmd.CommandText = text;
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                // Tables absentes possibles sur DB fraîche
                _log($"Purge (info) : {ex.Message}");
            }
        }

        _log("Base vierge appliquée (école / élèves / frais absents).");
    }

    private static string FindVirginSqlScript()
    {
        var payload = FindPayloadRoot();
        var candidates = new[]
        {
            Path.Combine(payload, "sql", "010_Purge_Production_Virgin.sql"),
            Path.Combine(AppContext.BaseDirectory, "sql", "010_Purge_Production_Virgin.sql"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "scripts", "010_Purge_Production_Virgin.sql")),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        throw new FileNotFoundException(
            "Script 010_Purge_Production_Virgin.sql introuvable. Relancez scripts/build-setup.ps1.");
    }

    private async Task RunFinalVerificationAsync(InstallOptions opt, string storageRoot, CancellationToken ct)
    {
        _log("Vérifications finales…");
        var errors = new List<string>();

        try
        {
            await TestSqlAsync(opt, ct);
            // lecture/écriture locale
            var cs = BuildSqlConnectionString(opt, opt.Database);
            await using var cn = new SqlConnection(cs);
            await cn.OpenAsync(ct);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Permissions";
            var perms = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            if (perms <= 0) errors.Add("Permissions système absentes (seed incomplet).");
            _log($"SQL local OK — Permissions={perms}");
        }
        catch (Exception ex)
        {
            errors.Add("SQL local : " + ex.Message);
        }

        if (opt.ConfigureCloudSync)
        {
            try
            {
                await TestCloudSqlAsync(opt, ct);
                var cs = BuildCloudSqlConnectionString(opt);
                await using var cn = new SqlConnection(cs);
                await cn.OpenAsync(ct);
                await using var cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync(ct);
                _log("SQL cloud OK");
            }
            catch (Exception ex)
            {
                errors.Add("SQL cloud : " + ex.Message);
            }
        }

        try
        {
            await ProbeStorageAsync(storageRoot, ct);
            _log("Dossier fichiers OK");
        }
        catch (Exception ex)
        {
            errors.Add("Dossier fichiers : " + ex.Message);
        }

        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status != ServiceControllerStatus.Running)
                errors.Add("Service Windows ErpScolaireApi non démarré.");
            else
                _log("Service Windows OK");
        }
        catch (Exception ex)
        {
            errors.Add("Service Windows : " + ex.Message);
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var resp = await http.GetAsync("http://127.0.0.1:5096/api/health", ct);
            if (!resp.IsSuccessStatusCode)
                errors.Add($"API health HTTP {(int)resp.StatusCode}");
            else
                _log("API health OK");
        }
        catch (Exception ex)
        {
            errors.Add("API health : " + ex.Message);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Vérifications finales échouées :\n- " + string.Join("\n- ", errors));
        }

        _log("Toutes les vérifications sont OK.");
    }

    private static string BuildCloudSqlConnectionString(InstallOptions opt)
    {
        var dataSource = opt.CloudSqlPort is > 0 and not 1433
            ? $"{opt.CloudSqlServer.Trim()},{opt.CloudSqlPort}"
            : opt.CloudSqlServer.Trim();
        return new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = opt.CloudDatabase.Trim(),
            UserID = opt.CloudSqlUser.Trim(),
            Password = opt.CloudSqlPassword,
            TrustServerCertificate = true,
            Encrypt = true,
            ConnectTimeout = 15,
        }.ConnectionString;
    }

    private static void WriteServeurFichiers(string appDir, string storageRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#######################################################");
        sb.AppendLine("# ERP SCOLAIRE RDC - genere par Setup");
        sb.AppendLine("# Configuration des fichiers (dossiers eleves)");
        sb.AppendLine("#######################################################");
        sb.AppendLine($"RACINE={storageRoot}");
        File.WriteAllText(Path.Combine(appDir, "ServeurFichiers.txt"), sb.ToString(), Encoding.UTF8);
    }

    private static void WriteServeurDonnees(string appDir, InstallOptions opt) =>
        ServeurDonneesFileWriter.Write(appDir, opt);

    private static void WriteApiAppsettings(string apiDir, InstallOptions opt)
    {
        var cs = BuildSqlConnectionString(opt, opt.Database);
        var json = new
        {
            ConnectionStrings = new { Default = cs },
            Deployment = new { Role = "Local", ReadOnly = false },
            Seed = new { IncludeDemoData = false },
            Jwt = new
            {
                Issuer = "SchoolManagementRDC",
                Audience = "SchoolManagementClients",
                SecretKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                            Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                AccessTokenExpirationMinutes = 30,
                RefreshTokenExpirationDays = 7
            }
        };
        File.WriteAllText(
            Path.Combine(apiDir, "appsettings.Production.json"),
            JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteDesktopAppsettings(string desktopDir, string apiBaseUrl, string cloudUrl, bool clientMode)
    {
        var json = new Dictionary<string, object?>
        {
            ["Api"] = new Dictionary<string, object>
            {
                ["BaseUrl"] = NormalizeUrl(apiBaseUrl),
                ["RemoteBaseUrl"] = NormalizeUrl(cloudUrl),
                ["ClientMode"] = clientMode,
            },
            ["LocalServerDiscovery"] = new Dictionary<string, object>
            {
                ["RemoteBaseUrl"] = NormalizeUrl(cloudUrl),
                ["EnableSubnetScan"] = true,
                ["EnableBackgroundRecheck"] = true,
            },
            ["Updates"] = new Dictionary<string, object>
            {
                ["CurrentVersion"] = "1.0.0",
                ["CheckEndpoint"] = "/api/v1/update/check",
                ["CheckIntervalHours"] = 6,
                ["AllowedHosts"] = new[] { "localhost", "127.0.0.1", "169.58.93.203" },
            },
            ["Dev"] = new Dictionary<string, object>
            {
                ["AutoLogin"] = false,
                ["UserName"] = "",
                ["Password"] = "",
            },
        };
        File.WriteAllText(
            Path.Combine(desktopDir, "appsettings.json"),
            JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string BuildSqlConnectionString(InstallOptions opt, string database)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = opt.SqlServer,
            InitialCatalog = database,
            TrustServerCertificate = true,
            Encrypt = true,
            ConnectTimeout = 15,
        };
        if (opt.UseWindowsAuth)
        {
            b.IntegratedSecurity = true;
        }
        else
        {
            b.UserID = opt.SqlUser;
            b.Password = opt.SqlPassword;
        }

        return b.ConnectionString;
    }

    private void OpenFirewallPort(int port, string name)
    {
        _log($"Firewall TCP {port}…");
        RunProcess("netsh",
            $"advfirewall firewall delete rule name=\"{name}\" protocol=TCP localport={port}");
        RunProcess("netsh",
            $"advfirewall firewall add rule name=\"{name}\" dir=in action=allow protocol=TCP localport={port}");
    }

    private static void CreateShortcut(string targetExe, string shortcutName)
    {
        if (!File.Exists(targetExe)) return;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        var lnk = Path.Combine(desktop, $"{shortcutName}.lnk");
        var workDir = Path.GetDirectoryName(targetExe)!;
        static string Q(string s) => s.Replace("\"", "\"\"");
        var vbs = Path.Combine(Path.GetTempPath(), "erp_shortcut.vbs");
        File.WriteAllText(vbs, $@"
Set o = CreateObject(""WScript.Shell"")
Set s = o.CreateShortcut(""{Q(lnk)}"")
s.TargetPath = ""{Q(targetExe)}""
s.WorkingDirectory = ""{Q(workDir)}""
s.Description = ""ERP Administration Scolaire""
s.Save
");
        RunProcess("wscript.exe", $"\"{vbs}\"");
        try { File.Delete(vbs); } catch { /* ignore */ }
    }

    private async Task WaitForHealthAsync(IReadOnlyList<string> urls, CancellationToken ct)
    {
        var timeout = ApiStartupWait.ResolveTimeout();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var wait = await ApiStartupWait.WaitAsync(
            ct2 => ApiStartupWait.ProbeUrlsAsync(http, urls, ct2),
            () =>
            {
                try
                {
                    using var sc = new ServiceController(ServiceName);
                    sc.Refresh();
                    return sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending;
                }
                catch
                {
                    return true;
                }
            },
            _log,
            timeout,
            cancellationToken: ct);

        if (!wait.Healthy)
            throw new System.TimeoutException(wait.Reason);
    }

    private static void TryStart(string exe)
    {
        if (!File.Exists(exe)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe),
            UseShellExecute = true,
        });
    }

    private static void CopyDirectory(string source, string target, bool overwrite)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, target));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var dest = file.Replace(source, target);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite);
        }
    }

    /// <summary>
    /// Retire le Mark-of-the-Web / Zone.Identifier (sinon WDAC bloque les DLL : 0x800711C7).
    /// </summary>
    private static void UnblockFiles(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                var zone = file + ":Zone.Identifier";
                if (File.Exists(zone))
                    File.Delete(zone);
            }
            catch
            {
                // ignore per-file
            }
        }

        // PowerShell Unblock-File (plus fiable selon politiques)
        try
        {
            RunProcess(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -LiteralPath '{root.Replace("'", "''")}' -Recurse -File | Unblock-File\"",
                throwOnError: false);
        }
        catch
        {
            // ignore
        }
    }

    private static string RunProcess(string fileName, string args, bool throwOnError = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Impossible de lancer {fileName}");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
        var combined = (stdout + Environment.NewLine + stderr).Trim();
        if (throwOnError && p.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} {args} → code {p.ExitCode}\n{combined}");
        return combined;
    }

    private static (int ExitCode, string Output) RunProcessEx(string fileName, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Impossible de lancer {fileName}");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
        return (p.ExitCode, (stdout + Environment.NewLine + stderr).Trim());
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (!url.EndsWith('/')) url += "/";
        return url;
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");
}
