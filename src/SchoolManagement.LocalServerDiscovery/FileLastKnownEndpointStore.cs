using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.LocalServerDiscovery;

public sealed class FileLastKnownEndpointStore : ILastKnownEndpointStore
{
    private readonly string _filePath;
    private readonly ILogger<FileLastKnownEndpointStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileLastKnownEndpointStore(ILogger<FileLastKnownEndpointStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ERP_Scolaire",
            "Discovery");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "last-local-endpoint.json");
    }

    public async Task<string?> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_filePath);
            var dto = await JsonSerializer.DeserializeAsync<StoreDto>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(dto?.BaseUrl) ? null : HealthProbe.NormalizeBaseUrl(dto.BaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discovery] Lecture dernière IP impossible");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dto = new StoreDto
            {
                BaseUrl = HealthProbe.NormalizeBaseUrl(baseUrl),
                SavedAtUtc = DateTimeOffset.UtcNow
            };
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, dto, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discovery] Enregistrement dernière IP impossible");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discovery] Suppression dernière IP impossible");
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class StoreDto
    {
        public string? BaseUrl { get; set; }
        public DateTimeOffset SavedAtUtc { get; set; }
    }
}
