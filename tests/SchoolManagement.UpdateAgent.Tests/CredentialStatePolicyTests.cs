using FluentAssertions;
using SchoolManagement.UpdateAgent;
using SchoolManagement.UpdateAgent.Tests.Support;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

public sealed class CredentialStoreTests
{
    private const string Secret = "unit-test-client-secret-DO-NOT-LOG-9f3c";

    [Fact]
    public void Dpapi_Roundtrip_Does_Not_Store_Plaintext()
    {
        using var ws = new TempWorkspace();
        var store = new AgentCredentialStore(ws.Paths, new DpapiSecretProtector());
        var credential = CycleFactory.SampleCredential(Secret);
        store.Save(credential);

        var json = File.ReadAllText(ws.Paths.CredentialFile);
        json.Should().NotContain(Secret);
        json.Should().Contain("DPAPI:");
        json.Should().Contain("clientSecretProtected");
        json.Should().NotContain("\"clientSecret\":");

        var loaded = store.Load();
        loaded.ClientId.Should().Be(credential.ClientId);
        loaded.SchoolId.Should().Be(credential.SchoolId);
        loaded.CredentialVersion.Should().Be(1);
        loaded.ServerInstanceId.Should().Be(credential.ServerInstanceId);
        loaded.ClientSecret.Should().Be(Secret);
    }

    [Fact]
    public void Plaintext_Secret_Is_Refused_On_Load()
    {
        using var ws = new TempWorkspace();
        File.WriteAllText(ws.Paths.CredentialFile, """
            {
              "clientId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "credentialVersion": 1,
              "schoolId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "clientSecretProtected": "not-protected"
            }
            """);
        var store = new AgentCredentialStore(ws.Paths, new DpapiSecretProtector());
        var act = () => store.Load();
        act.Should().Throw<AgentException>().WithMessage("*DPAPI*");
    }
}

public sealed class AgentStateStoreTests
{
    [Fact]
    public void State_Persists_Without_Secret()
    {
        using var ws = new TempWorkspace();
        var store = new AgentStateStore(ws.Paths);
        store.Save(new AgentState
        {
            CurrentRelease = "1.1.0",
            TargetRelease = "1.2.0",
            TargetReleaseId = Guid.NewGuid(),
            TargetSchemaVersion = 1,
            LastCheckUtc = DateTime.UtcNow,
            LastDownloadUtc = DateTime.UtcNow,
            LastResult = AgentResults.Downloaded,
            LastError = null,
        });

        var loaded = store.Load();
        loaded.TargetRelease.Should().Be("1.2.0");
        loaded.LastResult.Should().Be(AgentResults.Downloaded);
        File.ReadAllText(ws.Paths.StateFile).Should().NotContain("clientSecret");
        File.ReadAllText(ws.Paths.StateFile).Should().NotContain("ClientSecret");
    }
}

public sealed class AgentPathsTests
{
    [Fact]
    public void Writing_Under_Api_Install_Is_Refused()
    {
        using var ws = new TempWorkspace();
        var act = () => ws.Paths.EnsureNotApiInstall(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll"));
        act.Should().Throw<AgentException>().WithMessage("*API*");
    }
}

public sealed class PolicyGuardTests
{
    [Fact]
    public void Agent_Source_Has_No_Sql_Tls_Bypass_Or_Apply()
    {
        var dir = FindAgentSource();
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly);
        files.Should().NotBeEmpty();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("SqlConnection");
            text.Should().NotContain("MigrationManager");
            text.Should().NotContain("ApplyPackageAsync");
            text.Should().NotContain("Process.Start");
            text.Should().NotContain("SchoolManagement.API.exe");
            text.Should().NotContain("DangerousAcceptAnyServerCertificateValidator");
            text.Should().NotContain("ApplicationVersions");
        }
    }

    [Fact]
    public void Service_Identity_Is_Dedicated_Account()
    {
        AgentServiceNames.WindowsServiceName.Should().Be("ErpScolaireUpdateAgent");
        AgentServiceNames.WindowsAccountName.Should().Be("ErpScolaireUpdateAgent");
        AgentServiceNames.WindowsAccountName.Should().NotBe("LocalSystem");
    }

    [Fact]
    public void Tls_Policy_Does_Not_Bypass_Certificates()
    {
        SchoolManagement.Updates.UpdateTlsPolicy.AcceptsAnyServerCertificate.Should().BeFalse();
        using var handler = SchoolManagement.Updates.UpdateTlsPolicy.CreateHandler();
        handler.Should().BeOfType<HttpClientHandler>();
        ((HttpClientHandler)handler).ServerCertificateCustomValidationCallback.Should().BeNull();
    }

    private static string FindAgentSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SchoolManagement.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "src", "SchoolManagement.UpdateAgent");
    }
}
