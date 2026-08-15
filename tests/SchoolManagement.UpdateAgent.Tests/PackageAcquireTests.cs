using FluentAssertions;
using SchoolManagement.UpdateAgent;
using SchoolManagement.UpdateAgent.Tests.Support;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

public sealed class PackageAcquireTests
{
    [Fact]
    public async Task Downloads_And_Verifies_Pair()
    {
        using var ws = new TempWorkspace();
        var pack = Path.Combine(ws.Root, "pack");
        var zips = TestPackages.WriteZips(pack, "1.2.0");
        var handler = HandlerFor(zips);
        var acquire = CreateAcquire(ws, handler);
        var plan = Plan(zips);
        var result = await acquire.AcquireAsync(plan, CancellationToken.None);
        result.ReusedExisting.Should().BeFalse();
        File.Exists(result.ApiZipPath).Should().BeTrue();
        File.Exists(result.MigrationZipPath).Should().BeTrue();
        Directory.Exists(Path.Combine(result.ExtractRoot, "api")).Should().BeTrue();
        File.Exists(Path.Combine(result.ExtractRoot, "api", AppSchemaContract.ApiManifestFileName)).Should().BeTrue();
        result.ApiZipPath.Should().StartWith(ws.Paths.Packages);
        result.ApiZipPath.Should().NotContain(ws.ApiInstallRoot);
    }

    [Fact]
    public async Task Existing_Correct_Hash_Is_Reused()
    {
        using var ws = new TempWorkspace();
        var pack = Path.Combine(ws.Root, "pack");
        var zips = TestPackages.WriteZips(pack, "1.2.0");
        var handler = HandlerFor(zips);
        var acquire = CreateAcquire(ws, handler);
        var plan = Plan(zips);
        await acquire.AcquireAsync(plan, CancellationToken.None);
        handler.Files.Clear();
        var second = await acquire.AcquireAsync(plan, CancellationToken.None);
        second.ReusedExisting.Should().BeTrue();
    }

    [Fact]
    public async Task Corrupted_Package_Is_Redownloaded()
    {
        using var ws = new TempWorkspace();
        var pack = Path.Combine(ws.Root, "pack");
        var zips = TestPackages.WriteZips(pack, "1.2.0");
        var handler = HandlerFor(zips);
        var acquire = CreateAcquire(ws, handler);
        var plan = Plan(zips);
        var first = await acquire.AcquireAsync(plan, CancellationToken.None);
        File.WriteAllText(first.ApiZipPath, "corrupted-zip");
        var second = await acquire.AcquireAsync(plan, CancellationToken.None);
        second.ReusedExisting.Should().BeFalse();
        (await DownloadManager.ComputeSha256Async(second.ApiZipPath, CancellationToken.None))
            .Should().Be(zips.ApiSha);
    }

    [Fact]
    public async Task Wrong_Api_Sha_Deletes_Temp_And_Fails()
    {
        using var ws = new TempWorkspace();
        var pack = Path.Combine(ws.Root, "pack");
        var zips = TestPackages.WriteZips(pack, "1.2.0");
        var handler = HandlerFor(zips);
        var acquire = CreateAcquire(ws, handler);
        var plan = Plan(zips);
        plan.Api.Sha256 = new string('b', 64);
        var act = async () => await acquire.AcquireAsync(plan, CancellationToken.None);
        await act.Should().ThrowAsync<AgentException>().WithMessage("*SHA256*Api*");
        Directory.GetFiles(ws.Paths.Staging, "tmp-*.zip").Should().BeEmpty();
        File.Exists(Path.Combine(ws.Paths.Packages, "1.2.0", "api.zip")).Should().BeFalse();
    }

    [Fact]
    public async Task Wrong_Migration_Sha_Fails()
    {
        using var ws = new TempWorkspace();
        var pack = Path.Combine(ws.Root, "pack");
        var zips = TestPackages.WriteZips(pack, "1.2.0");
        var handler = HandlerFor(zips);
        var acquire = CreateAcquire(ws, handler);
        var plan = Plan(zips);
        plan.Migration.Sha256 = new string('c', 64);
        var act = async () => await acquire.AcquireAsync(plan, CancellationToken.None);
        await act.Should().ThrowAsync<AgentException>().WithMessage("*SHA256*Migration*");
        File.Exists(Path.Combine(ws.Paths.Packages, "1.2.0", "migration.zip")).Should().BeFalse();
    }

    [Fact]
    public async Task Wrong_Manifest_Fails_And_Cleans_Extract()
    {
        using var ws = new TempWorkspace();
        var pack = Path.Combine(ws.Root, "pack");
        var zips = TestPackages.WriteZips(pack, "1.2.0", corruptApiManifest: true);
        var handler = HandlerFor(zips);
        var acquire = CreateAcquire(ws, handler);
        var plan = Plan(zips);
        var act = async () => await acquire.AcquireAsync(plan, CancellationToken.None);
        await act.Should().ThrowAsync<MigrationException>();
        var extract = Path.Combine(ws.Paths.Staging, "extract", plan.ReleaseId.ToString("N"));
        Directory.Exists(extract).Should().BeFalse();
    }

    [Fact]
    public async Task Interrupted_Download_Cleans_Temp()
    {
        using var ws = new TempWorkspace();
        var pack = Path.Combine(ws.Root, "pack");
        var zips = TestPackages.WriteZips(pack, "1.2.0");
        var handler = HandlerFor(zips);
        handler.InterruptAfterFirstRead = true;
        var acquire = CreateAcquire(ws, handler);
        var act = async () => await acquire.AcquireAsync(Plan(zips), CancellationToken.None);
        await act.Should().ThrowAsync<IOException>().WithMessage("*interrompu*");
        Directory.GetFiles(ws.Paths.Staging, "tmp-*.zip").Should().BeEmpty();
        File.Exists(Path.Combine(ws.Paths.Packages, "1.2.0", "api.zip")).Should().BeFalse();
    }

    private static MapBytesHandler HandlerFor((string ApiZip, string MigrationZip, string ApiSha, string MigrationSha) zips)
    {
        return new MapBytesHandler
        {
            Files =
            {
                ["/api.zip"] = File.ReadAllBytes(zips.ApiZip),
                ["/migration.zip"] = File.ReadAllBytes(zips.MigrationZip),
            },
        };
    }

    private static PackageAcquireService CreateAcquire(TempWorkspace ws, MapBytesHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1/") };
        var download = new DownloadManager(
            http,
            uri => UpdateUrlGuard.IsAllowed(uri, ["127.0.0.1"], allowHttpForLocalHosts: true));
        return new PackageAcquireService(ws.Paths, download, ["127.0.0.1"]);
    }

    private static AgentReleasePlan Plan((string ApiZip, string MigrationZip, string ApiSha, string MigrationSha) zips) =>
        new()
        {
            ReleaseId = Guid.NewGuid(),
            Version = "1.2.0",
            ProtocolVersion = 2,
            FromSchemaVersion = 1,
            SchemaVersion = 1,
            Api = CycleFactory.Artifact("Api", "1.2.0", "http://127.0.0.1/api.zip", zips.ApiSha),
            Migration = CycleFactory.Artifact("Migration", "1.2.0", "http://127.0.0.1/migration.zip", zips.MigrationSha),
        };
}
