using System.IO;
using System.Text.Json;
using SchoolManagement.Application.Security.DTOs;

namespace SchoolManagement.Desktop.Navigation;

public interface IDesktopNavigationLocalCache
{
    Task SaveAsync(NavigationTreeDto tree, CancellationToken cancellationToken = default);

    Task<NavigationTreeDto?> TryLoadAsync(CancellationToken cancellationToken = default);
}

public sealed class DesktopNavigationLocalCache : IDesktopNavigationLocalCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;

    public DesktopNavigationLocalCache()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ERP_Administration_Scolaire",
            "cache");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "navigation-desktop.json");
    }

    public async Task SaveAsync(NavigationTreeDto tree, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, tree, JsonOptions, cancellationToken);
    }

    public async Task<NavigationTreeDto?> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<NavigationTreeDto>(stream, JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
