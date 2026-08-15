using FluentAssertions;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class ReleasePackageVerifierTests
{
    [Fact]
    public void Baseline_RequiredSchema_Is_One()
    {
        AppSchemaContract.RequiredSchemaVersion.Should().Be(1);
        AppSchemaContract.RequiredSchemaVersion.Should().Be(MigrationManager.BaselineSchemaVersion);
    }

    [Fact]
    public void Secret_Files_Are_Excluded_From_Api_Zip()
    {
        AppSchemaContract.IsExcludedFromApiZip("ServeurDonnees.txt").Should().BeTrue();
        AppSchemaContract.IsExcludedFromApiZip("ServeurFichiers.txt").Should().BeTrue();
        AppSchemaContract.IsExcludedFromApiZip("SchoolManagement.API.dll").Should().BeFalse();
    }

    [Fact]
    public void Version_Stamp_Strips_Git_Metadata()
    {
        ReleaseVersionStamp.FromInformational("1.2.0+deadbeef").Should().Be("1.2.0");
        var act = () => ReleaseVersionStamp.EnsureMatchesRelease("1.2.0+abc", "1.3.0");
        act.Should().Throw<MigrationException>().WithMessage("*≠*");
        ReleaseVersionStamp.EnsureMatchesRelease("1.2.0+abc", "1.2.0");
    }

    [Fact]
    public void Pair_1_To_1_Is_Accepted()
    {
        var (apiDir, migDir) = CreatePair(
            releaseVersion: "1.2.0",
            from: 1,
            to: 1,
            protocol: 2,
            includeSql: false);
        var act = () => ReleasePackageVerifier.VerifyPair(apiDir, migDir, "1.2.0", 1, 1, 2);
        act.Should().NotThrow();
    }

    [Fact]
    public void Pair_1_To_3_With_File_Hashes_Succeeds()
    {
        var (apiDir, migDir) = CreatePair("1.2.0", 1, 3, 2, includeSql: true, correctHashes: true);
        ReleasePackageVerifier.VerifyPair(apiDir, migDir, "1.2.0", 1, 3, 2);
    }

    [Fact]
    public void Wrong_Sql_Hash_Is_Refused_Before_Execution()
    {
        var (_, migDir) = CreatePair("1.2.0", 1, 3, 2, includeSql: true, correctHashes: false);
        var act = () => MigrationPackage.Load(migDir);
        act.Should().Throw<MigrationException>().WithMessage("*SHA256*");
    }

    [Fact]
    public void RequiredSchema_Mismatch_Is_Refused()
    {
        var (apiDir, migDir) = CreatePair("1.2.0", 1, 3, 2, includeSql: true, correctHashes: true);
        var apiPath = Path.Combine(apiDir, AppSchemaContract.ApiManifestFileName);
        File.WriteAllText(apiPath, """
            {"artifactType":"Api","releaseVersion":"1.2.0","requiredSchemaVersion":2,"protocolVersion":2,"runtime":"win-x64"}
            """);
        var act = () => ReleasePackageVerifier.VerifyPair(apiDir, migDir, "1.2.0", 1, 3, 2);
        act.Should().Throw<MigrationException>().WithMessage("*requiredSchemaVersion*");
    }

    [Fact]
    public void Catalogue_From_To_Mismatch_Is_Refused()
    {
        var (apiDir, migDir) = CreatePair("1.2.0", 1, 3, 2, includeSql: true, correctHashes: true);
        var act = () => ReleasePackageVerifier.VerifyPair(apiDir, migDir, "1.2.0", 2, 3, 2);
        act.Should().Throw<MigrationException>().WithMessage("*fromSchemaVersion*");
    }

    [Fact]
    public void ReleaseVersion_Mismatch_Is_Refused()
    {
        var (apiDir, migDir) = CreatePair("1.2.0", 1, 1, 2, includeSql: false);
        var act = () => ReleasePackageVerifier.VerifyPair(apiDir, migDir, "1.3.0", 1, 1, 2);
        act.Should().Throw<MigrationException>().WithMessage("*releaseVersion*");
    }

    [Fact]
    public void Remote_Uri_Is_Refused()
    {
        var act = () => ApiArtifactManifest.Load("https://example.com/api");
        act.Should().Throw<MigrationException>().WithMessage("*local*");
    }

    private static (string ApiDir, string MigDir) CreatePair(
        string releaseVersion,
        int from,
        int to,
        int protocol,
        bool includeSql,
        bool correctHashes = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "lot2b2-" + Guid.NewGuid().ToString("N"));
        var apiDir = Path.Combine(root, "api");
        var migDir = Path.Combine(root, "migration");
        Directory.CreateDirectory(apiDir);
        Directory.CreateDirectory(migDir);

        File.WriteAllText(
            Path.Combine(apiDir, AppSchemaContract.ApiManifestFileName),
            $$"""
            {
              "artifactType": "Api",
              "releaseVersion": "{{releaseVersion}}",
              "requiredSchemaVersion": {{to}},
              "protocolVersion": {{protocol}},
              "runtime": "win-x64"
            }
            """);

        var names = new List<string>();
        var files = new List<MigrationFileHash>();
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Updates", "Fixtures", "sql");
        for (var v = from; v < to; v++)
        {
            var name = MigrationManager.FileNameFor(v, v + 1);
            names.Add(name);
            var dest = Path.Combine(migDir, name);
            File.Copy(Path.Combine(fixtures, name), dest);
            var sha = correctHashes
                ? ArtifactHash.Sha256File(dest)
                : "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            files.Add(new MigrationFileHash { Name = name, Sha256 = sha });
        }

        var manifest = new MigrationManifest
        {
            SchemaVersion = to,
            FromSchemaVersion = from,
            ToSchemaVersion = to,
            ReleaseVersion = releaseVersion,
            Migrations = names,
            Files = includeSql ? files : [],
        };
        File.WriteAllText(
            Path.Combine(migDir, MigrationPackage.ManifestFileName),
            System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }));

        return (apiDir, migDir);
    }
}
