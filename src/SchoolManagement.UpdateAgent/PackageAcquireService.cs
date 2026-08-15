using System.IO.Compression;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public sealed class AcquiredPackages
{
    public required string ApiZipPath { get; init; }

    public required string MigrationZipPath { get; init; }

    public required string ExtractRoot { get; init; }

    public required bool ReusedExisting { get; init; }
}

public interface IPackageAcquire
{
    Task<AcquiredPackages> AcquireAsync(AgentReleasePlan plan, CancellationToken cancellationToken);
}

public sealed class PackageAcquireService : IPackageAcquire
{
    private readonly AgentPaths _paths;
    private readonly DownloadManager _download;
    private readonly IReadOnlyList<string> _allowedHosts;

    public PackageAcquireService(AgentPaths paths, DownloadManager download, IReadOnlyList<string> allowedHosts)
    {
        _paths = paths;
        _download = download;
        _allowedHosts = allowedHosts;
    }

    public async Task<AcquiredPackages> AcquireAsync(AgentReleasePlan plan, CancellationToken cancellationToken)
    {
        _paths.EnsureDirectories();
        var packDir = Path.Combine(_paths.Packages, plan.Version);
        Directory.CreateDirectory(packDir);
        _paths.EnsureNotApiInstall(packDir);

        var apiZip = Path.Combine(packDir, "api.zip");
        var migZip = Path.Combine(packDir, "migration.zip");
        var apiReused = await EnsureZipAsync(plan.Api, apiZip, cancellationToken);
        var migReused = await EnsureZipAsync(plan.Migration, migZip, cancellationToken);

        var extract = Path.Combine(_paths.Staging, "extract", plan.ReleaseId.ToString("N"));
        _paths.EnsureNotApiInstall(extract);
        if (Directory.Exists(extract))
        {
            Directory.Delete(extract, recursive: true);
        }

        var apiDir = Path.Combine(extract, "api");
        var migDir = Path.Combine(extract, "migration");
        try
        {
            SafeExtract(apiZip, apiDir);
            SafeExtract(migZip, migDir);
            ReleasePackageVerifier.VerifyPair(
                apiDir,
                migDir,
                plan.Version,
                plan.FromSchemaVersion,
                plan.SchemaVersion,
                plan.ProtocolVersion);
        }
        catch
        {
            TryDeleteDirectory(extract);
            throw;
        }

        return new AcquiredPackages
        {
            ApiZipPath = apiZip,
            MigrationZipPath = migZip,
            ExtractRoot = extract,
            ReusedExisting = apiReused && migReused,
        };
    }

    private async Task<bool> EnsureZipAsync(
        AgentArtifactDto artifact,
        string destination,
        CancellationToken cancellationToken)
    {
        _paths.EnsureNotApiInstall(destination);
        if (File.Exists(destination))
        {
            var existing = await DownloadManager.ComputeSha256Async(destination, cancellationToken);
            if (DownloadManager.HashesMatch(artifact.Sha256, existing))
            {
                return true;
            }

            File.Delete(destination);
        }

        var tmp = Path.Combine(_paths.Staging, "tmp-" + Guid.NewGuid().ToString("N") + ".zip");
        _paths.EnsureNotApiInstall(tmp);
        try
        {
            if (!Uri.TryCreate(artifact.Url, UriKind.Absolute, out var uri)
                || !UpdateUrlGuard.IsAllowed(uri, _allowedHosts, allowHttpForLocalHosts: true))
            {
                throw new AgentException("URL d'artifact non autorisée.");
            }

            await _download.DownloadAsync(artifact.Url, tmp, artifact.Size, progress: null, cancellationToken);
            var hash = await DownloadManager.ComputeSha256Async(tmp, cancellationToken);
            if (!DownloadManager.HashesMatch(artifact.Sha256, hash))
            {
                throw new AgentException($"SHA256 invalide pour {artifact.Type}.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(tmp, destination, overwrite: true);
            return false;
        }
        catch
        {
            TryDelete(tmp);
            TryDelete(destination);
            throw;
        }
    }

    internal static void SafeExtract(string zipPath, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var targetFull = Path.GetFullPath(targetDir)
                         .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
            {
                continue;
            }

            var dest = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            if (!dest.StartsWith(targetFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new AgentException("ZIP path traversal refusé.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
