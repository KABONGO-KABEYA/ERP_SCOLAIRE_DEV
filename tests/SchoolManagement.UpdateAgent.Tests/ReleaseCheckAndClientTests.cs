using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using SchoolManagement.UpdateAgent;
using SchoolManagement.UpdateAgent.Tests.Support;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

public sealed class ReleaseCheckGuardTests
{
    private static readonly string ValidSha = new('a', 64);

    [Fact]
    public void Absence_Of_Release_Returns_Null()
    {
        ReleaseCheckGuard.Accept(new AgentCheckResult { StatusCode = HttpStatusCode.NoContent })
            .Should().BeNull();
    }

    [Fact]
    public void Desktop_Only_Is_Refused()
    {
        var body = new AgentReleaseCheckDto
        {
            ReleaseId = Guid.NewGuid(),
            Version = "1.2.0",
            ProtocolVersion = 1,
            FromSchemaVersion = 1,
            SchemaVersion = 1,
            Artifact = CycleFactory.Artifact("Desktop", "1.2.0", "https://example.com/d.exe", ValidSha),
        };
        var act = () => ReleaseCheckGuard.Accept(new AgentCheckResult
        {
            StatusCode = HttpStatusCode.OK,
            Body = body,
        });
        act.Should().Throw<AgentException>().WithMessage("*Desktop*");
    }

    [Fact]
    public void Api_And_Migration_Are_Accepted()
    {
        var releaseId = Guid.NewGuid();
        var plan = ReleaseCheckGuard.Accept(OkBody(releaseId, "1.2.0", "1.2.0", "1.2.0"));
        plan.Should().NotBeNull();
        plan!.ReleaseId.Should().Be(releaseId);
        plan.Api.Type.Should().Be("Api");
        plan.Migration.Type.Should().Be("Migration");
    }

    [Fact]
    public void Different_ReleaseId_Is_Refused()
    {
        var releaseId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var body = Body(releaseId, "1.2.0", "1.2.0", "1.2.0");
        body.Api!.ReleaseId = other;
        var act = () => ReleaseCheckGuard.Accept(new AgentCheckResult
        {
            StatusCode = HttpStatusCode.OK,
            Body = body,
        });
        act.Should().Throw<AgentException>().WithMessage("*ReleaseId*");
    }

    [Fact]
    public void Different_Artifact_Version_Is_Refused()
    {
        var act = () => ReleaseCheckGuard.Accept(OkBody(Guid.NewGuid(), "1.2.0", "1.3.0", "1.2.0"));
        act.Should().Throw<AgentException>().WithMessage("*Version d'artifact*");
    }

    private static AgentCheckResult OkBody(Guid releaseId, string version, string apiVersion, string migVersion) =>
        new()
        {
            StatusCode = HttpStatusCode.OK,
            Body = Body(releaseId, version, apiVersion, migVersion),
        };

    private static AgentReleaseCheckDto Body(Guid releaseId, string version, string apiVersion, string migVersion) =>
        new()
        {
            ReleaseId = releaseId,
            Version = version,
            ProtocolVersion = 2,
            FromSchemaVersion = 1,
            SchemaVersion = 1,
            Api = CycleFactory.Artifact("Api", apiVersion, "https://example.com/api.zip", ValidSha, releaseId),
            Migration = CycleFactory.Artifact("Migration", migVersion, "https://example.com/mig.zip", ValidSha, releaseId),
        };
}

public sealed class BootstrapAgentClientTests
{
    [Fact]
    public async Task Token_Request_Omits_SchoolId()
    {
        string? body = null;
        var handler = new ScriptedHandler
        {
            Responder = (_, raw) =>
            {
                body = raw;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"accessToken":"jwt","tokenType":"Bearer","expiresIn":1800,"schoolId":"cccccccc-cccc-cccc-cccc-cccccccccccc","clientId":"dddddddd-dddd-dddd-dddd-dddddddddddd"}""",
                        System.Text.Encoding.UTF8,
                        "application/json"),
                };
            },
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1/") };
        var client = new BootstrapAgentClient(http, new RecordingLogger<BootstrapAgentClient>());
        var token = await client.GetTokenAsync(Guid.NewGuid(), "s3cret", CancellationToken.None);
        token.AccessToken.Should().Be("jwt");
        body.Should().NotBeNull();
        body.Should().NotContain("schoolId");
        body.Should().Contain("clientId");
        body.Should().Contain("clientSecret");
    }

    [Fact]
    public async Task Wrong_Secret_Throws()
    {
        var handler = new ScriptedHandler
        {
            Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized),
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1/") };
        var client = new BootstrapAgentClient(http, new RecordingLogger<BootstrapAgentClient>());
        var act = async () => await client.GetTokenAsync(Guid.NewGuid(), "bad", CancellationToken.None);
        await act.Should().ThrowAsync<AgentException>().WithMessage("*401*");
    }

    [Fact]
    public async Task Check_Sends_Channel_Prod_And_ArtifactType_Api()
    {
        string? path = null;
        AuthenticationHeaderValue? auth = null;
        var handler = new ScriptedHandler
        {
            Responder = (req, _) =>
            {
                path = req.RequestUri!.Query;
                auth = req.Headers.Authorization;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            },
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1/") };
        var client = new BootstrapAgentClient(http, new RecordingLogger<BootstrapAgentClient>());
        var result = await client.CheckReleaseAsync("the-jwt", "PROD", CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
        path.Should().Contain("channel=PROD");
        path.Should().Contain("artifactType=Api");
        auth!.Scheme.Should().Be("Bearer");
        auth.Parameter.Should().Be("the-jwt");
    }
}
