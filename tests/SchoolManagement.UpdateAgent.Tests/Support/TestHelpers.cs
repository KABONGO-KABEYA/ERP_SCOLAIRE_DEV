using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using SchoolManagement.UpdateAgent;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent.Tests.Support;

internal sealed class TempWorkspace : IDisposable
{
    public string Root { get; }
    public AgentPaths Paths { get; }
    public string ApiInstallRoot { get; }
    public string InstallParent { get; }

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "ua-2b3-" + Guid.NewGuid().ToString("N"));
        InstallParent = Path.Combine(Root, "Install");
        ApiInstallRoot = Path.Combine(InstallParent, "Api");
        Directory.CreateDirectory(ApiInstallRoot);
        File.WriteAllText(Path.Combine(ApiInstallRoot, "SchoolManagement.API.dll"), "api-marker-v1");
        File.WriteAllText(Path.Combine(ApiInstallRoot, "ServeurDonnees.txt"), "SERVEUR=local");
        File.WriteAllText(Path.Combine(ApiInstallRoot, "ServeurDonneesCloud.txt"), "CLOUD=keep");
        File.WriteAllText(Path.Combine(ApiInstallRoot, "ServeurFichiers.txt"), "FILES=keep");
        File.WriteAllText(Path.Combine(ApiInstallRoot, "ServerIdentity.json"), """{"serverInstanceId":"keep"}""");
        Paths = new AgentPaths(Path.Combine(Root, "UpdateAgent"), ApiInstallRoot);
        Paths.EnsureDirectories();
    }

    public string SnapshotApiInstall() =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('|',
                Directory.GetFiles(ApiInstallRoot, "*", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(p => Path.GetRelativePath(ApiInstallRoot, p) + "=" + File.ReadAllText(p))))));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // temp cleanup
        }
    }
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var text = formatter(state, exception);
        if (exception is not null)
        {
            text += " " + exception.Message;
        }

        Messages.Add(text);
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class FakeBootstrap : IBootstrapAgentClient
{
    public AgentTokenResponse? Token { get; set; }
    public Exception? TokenError { get; set; }
    public AgentCheckResult? Check { get; set; }
    public Exception? CheckError { get; set; }
    public string? LastRequestBodyHint { get; set; }
    public int TokenCalls { get; private set; }
    public int CheckCalls { get; private set; }

    public Task<AgentTokenResponse> GetTokenAsync(Guid clientId, string clientSecret, CancellationToken cancellationToken)
    {
        TokenCalls++;
        LastRequestBodyHint = $"clientId={clientId:D};schoolId-omitted";
        if (TokenError is not null)
        {
            throw TokenError;
        }

        return Task.FromResult(Token ?? throw new InvalidOperationException("Token non configuré."));
    }

    public Task<AgentCheckResult> CheckReleaseAsync(string accessToken, string channel, CancellationToken cancellationToken)
    {
        CheckCalls++;
        if (CheckError is not null)
        {
            throw CheckError;
        }

        return Task.FromResult(Check ?? new AgentCheckResult { StatusCode = HttpStatusCode.NoContent });
    }
}

internal sealed class FakeAcquire : IPackageAcquire
{
    public int Calls { get; private set; }
    public bool Reused { get; set; }
    public Exception? Error { get; set; }

    public Task<AcquiredPackages> AcquireAsync(AgentReleasePlan plan, CancellationToken cancellationToken)
    {
        Calls++;
        if (Error is not null)
        {
            throw Error;
        }

        return Task.FromResult(new AcquiredPackages
        {
            ApiZipPath = "api.zip",
            MigrationZipPath = "migration.zip",
            ExtractRoot = "extract",
            ReusedExisting = Reused,
        });
    }
}

internal sealed class ScriptedHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, string, HttpResponseMessage> Responder { get; init; } =
        (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return Responder(request, body);
    }
}

internal sealed class MapBytesHandler : HttpMessageHandler
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool InterruptAfterFirstRead { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (!Files.TryGetValue(path, out var bytes) && !Files.TryGetValue(request.RequestUri!.ToString(), out bytes))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        Stream stream = new MemoryStream(bytes);
        if (InterruptAfterFirstRead)
        {
            stream = new InterruptAfterReadStream(bytes);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream)
            {
                Headers = { ContentLength = bytes.Length },
            },
        });
    }

    private sealed class InterruptAfterReadStream : MemoryStream
    {
        private bool _readOnce;

        public InterruptAfterReadStream(byte[] buffer) : base(buffer)
        {
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_readOnce)
            {
                throw new IOException("téléchargement interrompu");
            }

            _readOnce = true;
            var slice = buffer.Length > 4 ? buffer[..4] : buffer;
            return base.ReadAsync(slice, cancellationToken);
        }
    }
}

internal static class TestPackages
{
    public static (string ApiZip, string MigrationZip, string ApiSha, string MigrationSha) WriteZips(
        string directory,
        string version,
        int fromSchema = 1,
        int toSchema = 1,
        int protocol = 2,
        bool corruptApiManifest = false)
    {
        Directory.CreateDirectory(directory);
        var apiSrc = Path.Combine(directory, "api-src");
        var migSrc = Path.Combine(directory, "mig-src");
        Directory.CreateDirectory(apiSrc);
        Directory.CreateDirectory(migSrc);

        var manifestVersion = corruptApiManifest ? "9.9.9" : version;
        File.WriteAllText(
            Path.Combine(apiSrc, AppSchemaContract.ApiManifestFileName),
            $$"""
            {
              "artifactType": "Api",
              "releaseVersion": "{{manifestVersion}}",
              "requiredSchemaVersion": {{toSchema}},
              "protocolVersion": {{protocol}},
              "runtime": "win-x64"
            }
            """);
        File.WriteAllText(Path.Combine(apiSrc, "SchoolManagement.API.dll"), "fake-api-" + version);

        var names = new List<string>();
        var files = new List<MigrationFileHash>();
        for (var v = fromSchema; v < toSchema; v++)
        {
            var name = MigrationManager.FileNameFor(v, v + 1);
            names.Add(name);
            var sqlPath = Path.Combine(migSrc, name);
            File.WriteAllText(sqlPath, $"-- test {v} -> {v + 1}");
            files.Add(new MigrationFileHash { Name = name, Sha256 = ArtifactHash.Sha256File(sqlPath) });
        }

        var manifest = new MigrationManifest
        {
            SchemaVersion = toSchema,
            FromSchemaVersion = fromSchema,
            ToSchemaVersion = toSchema,
            ReleaseVersion = version,
            Migrations = names,
            Files = files,
        };
        File.WriteAllText(
            Path.Combine(migSrc, MigrationPackage.ManifestFileName),
            System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }));

        var apiZip = Path.Combine(directory, "api.zip");
        var migZip = Path.Combine(directory, "migration.zip");
        if (File.Exists(apiZip))
        {
            File.Delete(apiZip);
        }

        if (File.Exists(migZip))
        {
            File.Delete(migZip);
        }

        ZipFile.CreateFromDirectory(apiSrc, apiZip);
        ZipFile.CreateFromDirectory(migSrc, migZip);
        return (apiZip, migZip, ArtifactHash.Sha256File(apiZip), ArtifactHash.Sha256File(migZip));
    }
}

internal sealed class ArtifactStaticServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string BaseUrl { get; }

    private ArtifactStaticServer(WebApplication app, string baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    public static async Task<ArtifactStaticServer> StartAsync(string directory)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(directory),
            RequestPath = "",
            EnableDefaultFiles = false,
        });
        await app.StartAsync();
        return new ArtifactStaticServer(app, app.Urls.First().TrimEnd('/'));
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

internal static class CycleFactory
{
    public static AgentCycle Create(
        TempWorkspace workspace,
        AgentOptions options,
        IBootstrapAgentClient bootstrap,
        IPackageAcquire packages,
        RecordingLogger<AgentCycle>? log = null,
        IDeployOrchestrator? deploy = null)
    {
        return new AgentCycle(
            workspace.Paths,
            new AgentCredentialStore(workspace.Paths, new DpapiSecretProtector()),
            new AgentStateStore(workspace.Paths),
            bootstrap,
            packages,
            options,
            log ?? new RecordingLogger<AgentCycle>(),
            deploy);
    }

    public static AgentCredential SampleCredential(string secret, Guid? schoolId = null) => new()
    {
        ClientId = Guid.NewGuid(),
        ClientSecret = secret,
        CredentialVersion = 1,
        SchoolId = schoolId ?? Guid.NewGuid(),
        ServerInstanceId = Guid.NewGuid(),
    };

    public static AgentOptions Options(TempWorkspace workspace, string channel = "PROD") => new()
    {
        BootstrapBaseUrl = "http://127.0.0.1/",
        Channel = channel,
        AllowedHosts = ["127.0.0.1", "localhost"],
        DataRoot = workspace.Paths.Root,
        ApiInstallRoot = workspace.ApiInstallRoot,
        RunOnce = true,
        AutoDeploy = false,
        ExpectedDatabaseName = "SchoolManagementRDC_Test",
        MinFreeDiskBytes = 1,
        MinBackupBytes = 1,
        HealthIntervalMs = 1,
        HealthBudgetSeconds = 1,
        HealthSuccessRequired = 3,
        StopTimeoutSeconds = 5,
        StartTimeoutSeconds = 5,
    };

    public static AgentArtifactDto Artifact(string type, string version, string url, string sha, Guid? releaseId = null) =>
        new()
        {
            ArtifactId = Guid.NewGuid(),
            ReleaseId = releaseId,
            Type = type,
            Version = version,
            Url = url,
            Size = 12,
            Sha256 = sha,
        };
}
