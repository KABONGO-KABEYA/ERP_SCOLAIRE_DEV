using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace SchoolManagement.Updates;

public sealed class DownloadProgress
{
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public double BytesPerSecond { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public double Percent =>
        TotalBytes is > 0 ? Math.Clamp(100.0 * BytesReceived / TotalBytes.Value, 0, 100) : 0;
}

public sealed class DownloadManager
{
    private readonly HttpClient _httpClient;
    private readonly Func<Uri, bool> _isUrlAllowed;

    public DownloadManager(HttpClient httpClient, Func<Uri, bool> isUrlAllowed)
    {
        _httpClient = httpClient;
        _isUrlAllowed = isUrlAllowed;
    }

    public async Task<string> DownloadAsync(
        string url,
        string destinationPath,
        long? expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !_isUrlAllowed(uri))
        {
            throw new InvalidOperationException("URL de téléchargement non autorisée.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        long existing = File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existing > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existing, null);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var resume = response.StatusCode == System.Net.HttpStatusCode.PartialContent && existing > 0;
        if (!resume && response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            response.EnsureSuccessStatusCode();
        }

        var total = expectedSize
                    ?? response.Content.Headers.ContentLength + (resume ? existing : 0);

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            destinationPath,
            resume ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        var buffer = new byte[81920];
        long received = resume ? existing : 0;
        var started = DateTime.UtcNow;
        long lastBytes = received;
        var lastTick = started;

        while (true)
        {
            var read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;

            var now = DateTime.UtcNow;
            var elapsed = (now - lastTick).TotalSeconds;
            if (elapsed >= 0.25 || received == total)
            {
                var delta = received - lastBytes;
                var speed = elapsed > 0 ? delta / elapsed : 0;
                TimeSpan? eta = null;
                if (speed > 0 && total is > 0)
                {
                    eta = TimeSpan.FromSeconds(Math.Max(0, (total.Value - received) / speed));
                }

                progress?.Report(new DownloadProgress
                {
                    BytesReceived = received,
                    TotalBytes = total,
                    BytesPerSecond = speed,
                    EstimatedRemaining = eta
                });

                lastBytes = received;
                lastTick = now;
            }
        }

        return destinationPath;
    }

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool HashesMatch(string? expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return string.Equals(
            expected.Trim().Replace("-", "", StringComparison.Ordinal),
            actual.Trim().Replace("-", "", StringComparison.Ordinal),
            StringComparison.OrdinalIgnoreCase);
    }
}
