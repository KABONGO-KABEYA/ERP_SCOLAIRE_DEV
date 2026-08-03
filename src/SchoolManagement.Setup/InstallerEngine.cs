using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

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

    public async Task EnsureDatabaseExistsAsync(InstallOptions opt, CancellationToken ct = default)
    {
        var cs = BuildSqlConnectionString(opt, "master");
        await using var cn = new SqlConnection(cs);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"
IF DB_ID(N'{EscapeSql(opt.Database)}') IS NULL
BEGIN
  CREATE DATABASE [{opt.Database.Replace("]", "]]")}];
END";
        await cmd.ExecuteNonQueryAsync(ct);
        _log($"Base SQL prête : {opt.Database}");
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
        CopyDirectory(Path.Combine(payload, "api"), apiDir, overwrite: true);
        CopyDirectory(Path.Combine(payload, "desktop"), desktopDir, overwrite: true);
        UnblockFiles(apiDir);
        UnblockFiles(desktopDir);
        _log("Fichiers copiés.");

        await EnsureDatabaseExistsAsync(opt, ct);
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

        // Pré-contrôle API (migrations + seed système)
        if (opt.StartAfterInstall)
            await EnsureApiCanStartAsync(apiDir, opt, storageRoot, ct);

        if (opt.ApplyVirginDatabase)
            await ApplyVirginPurgeAsync(opt, ct);

        InstallOrUpdateService(apiDir, opt, storageRoot);
        if (opt.StartAfterInstall)
        {
            StartService();
            await WaitForHealthAsync("http://127.0.0.1:5096/api/health", TimeSpan.FromSeconds(90), ct);
            _log("API répond sur http://127.0.0.1:5096");
            await RunFinalVerificationAsync(opt, storageRoot, ct);
            TryStart(Path.Combine(desktopDir, "SchoolManagement.Desktop.exe"));
        }
    }

    private async Task InstallClientAsync(InstallOptions opt, string payload, CancellationToken ct)
    {
        _log("Mode CLIENT — déploiement Desktop uniquement…");
        var desktopDir = Path.Combine(opt.InstallRoot, "Desktop");
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
    /// Lance l'API en console quelques secondes pour détecter Smart App Control / erreurs fatales
    /// avant d'enregistrer le démarrage du service Windows.
    /// </summary>
    private async Task EnsureApiCanStartAsync(string apiDir, InstallOptions opt, string storageRoot, CancellationToken ct)
    {
        var exe = Path.Combine(apiDir, "SchoolManagement.API.exe");
        var outLog = Path.Combine(apiDir, "logs", "preflight-out.log");
        var errLog = Path.Combine(apiDir, "logs", "preflight-err.log");
        Directory.CreateDirectory(Path.GetDirectoryName(outLog)!);
        try { File.Delete(outLog); } catch { /* ignore */ }
        try { File.Delete(errLog); } catch { /* ignore */ }

        _log("Pré-contrôle : démarrage test de l'API…");
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

        // Attendre health ou sortie anticipée
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(25);
        var healthy = false;
        while (DateTime.UtcNow < until && !p.HasExited)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var resp = await http.GetAsync("http://127.0.0.1:5096/api/health", ct);
                if (resp.IsSuccessStatusCode)
                {
                    healthy = true;
                    break;
                }
            }
            catch
            {
                // retry
            }

            await Task.Delay(800, ct);
        }

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

        if (!healthy && p.ExitCode != 0)
        {
            var tail = combined.Length > 1200 ? combined[^1200..] : combined;
            throw new InvalidOperationException(
                "L'API refuse de démarrer (pré-contrôle).\n\n" + tail);
        }

        // Laisser le port se libérer
        await Task.Delay(1500, ct);
        _log("Pré-contrôle OK.");
    }

    private void InstallOrUpdateService(string apiDir, InstallOptions opt, string storageRoot)
    {
        var exe = Path.Combine(apiDir, "SchoolManagement.API.exe");
        if (!File.Exists(exe))
            throw new FileNotFoundException("SchoolManagement.API.exe introuvable dans le payload API.", exe);

        Directory.CreateDirectory(Path.Combine(apiDir, "logs"));

        try
        {
            var existing = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _log("Service existant — arrêt / suppression…");
                using (existing)
                {
                    if (existing.Status != ServiceControllerStatus.Stopped)
                    {
                        try
                        {
                            existing.Stop();
                            existing.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                        catch (Exception ex)
                        {
                            _log($"Arrêt service: {ex.Message}");
                        }
                    }
                }

                RunProcess("sc.exe", $"delete {ServiceName}", throwOnError: false);
                Thread.Sleep(2000);
            }
        }
        catch (Exception ex)
        {
            _log($"Info service: {ex.Message}");
        }

        // sc.exe exige un espace après '='. Chemins avec espaces : tout le binPath entre guillemets.
        _log("Création du service Windows ErpScolaireApi…");
        var createArgs =
            $"create {ServiceName} binPath= \"{exe}\" start= auto DisplayName= \"ERP Scolaire API\" obj= LocalSystem";
        var createOut = RunProcess("sc.exe", createArgs, throwOnError: true);
        _log(createOut.Trim());

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
        _log("Service Windows configuré.");
    }

    private void StartService()
    {
        using var sc = new ServiceController(ServiceName);
        if (sc.Status == ServiceControllerStatus.Running)
        {
            _log("Service déjà démarré.");
            return;
        }

        try
        {
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
            _log("Service démarré.");
        }
        catch (Exception ex)
        {
            var hint = ReadServiceFailureHint();
            throw new InvalidOperationException(
                "Impossible de démarrer le service ErpScolaireApi.\n" +
                ex.Message +
                (string.IsNullOrWhiteSpace(hint) ? "" : "\n\nDétail:\n" + hint) +
                "\n\nVérifiez les logs dans le dossier Api\\logs\\ après un nouvel essai.",
                ex);
        }
    }

    private static string ReadServiceFailureHint()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wevtutil.exe",
                Arguments =
                    "qe Application /c:8 /rd:true /f:text /q:\"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= 600000]]]\"",
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
                            || l.Contains("Service Control Manager", StringComparison.OrdinalIgnoreCase)
                            || l.Contains("cannot start", StringComparison.OrdinalIgnoreCase))
                .Take(25);
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
            try
            {
                RunProcess("net.exe", $"share {opt.ShareName} /delete /y", throwOnError: false);
                var shareOut = RunProcess(
                    "net.exe",
                    $"share {opt.ShareName}=\"{root}\" /GRANT:Everyone,FULL",
                    throwOnError: false);
                if (!string.IsNullOrWhiteSpace(shareOut))
                    _log(shareOut.Trim());
                _log($"Partage réseau : \\\\{Environment.MachineName}\\{opt.ShareName}");
            }
            catch (Exception ex)
            {
                _log($"Partage réseau non créé : {ex.Message}");
            }
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

    private static void WriteServeurDonnees(string apiDir, InstallOptions opt)
    {
        var auth = opt.UseWindowsAuth ? "WINDOWS" : "SQL";
        var sb = new StringBuilder();
        sb.AppendLine("#######################################################");
        sb.AppendLine("# ERP SCOLAIRE RDC - genere par Setup");
        sb.AppendLine("#######################################################");
        sb.AppendLine($"SERVEUR={opt.SqlServer}");
        sb.AppendLine("PORT=1433");
        sb.AppendLine($"BASE={opt.Database}");
        sb.AppendLine($"AUTHENTIFICATION={auth}");
        sb.AppendLine($"UTILISATEUR={opt.SqlUser}");
        // Mot de passe : laisser vide en Windows auth ; SQL auth en clair uniquement
        // pour bootstrap (DPAPI sera appliqué au 1er enregistrement Desktop/config).
        sb.AppendLine(opt.UseWindowsAuth ? "MOTDEPASSE=" : $"MOTDEPASSE={opt.SqlPassword}");
        File.WriteAllText(Path.Combine(apiDir, "ServeurDonnees.txt"), sb.ToString(), Encoding.UTF8);
    }

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

    private static async Task WaitForHealthAsync(string url, TimeSpan timeout, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var resp = await http.GetAsync(url, ct);
                if (resp.IsSuccessStatusCode) return;
            }
            catch
            {
                // retry
            }

            await Task.Delay(2000, ct);
        }

        throw new System.TimeoutException("L'API n'a pas répondu à temps après le démarrage du service.");
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

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (!url.EndsWith('/')) url += "/";
        return url;
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");
}
