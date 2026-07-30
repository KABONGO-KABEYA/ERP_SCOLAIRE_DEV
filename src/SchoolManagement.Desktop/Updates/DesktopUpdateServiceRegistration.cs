using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Updates;

namespace SchoolManagement.Desktop.Updates;

public static class DesktopUpdateServiceRegistration
{
    public const string HttpClientName = "UpdateApi";

    public static IServiceCollection AddDesktopUpdates(this IServiceCollection services, IConfiguration configuration)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ERP_Scolaire",
            "Updates");
        Directory.CreateDirectory(dataDir);

        var settingsStore = new UpdateSettingsStore(dataDir);
        var historyStore = new UpdateHistoryStore(dataDir);
        var settings = settingsStore.Load();

        var configuredVersion = configuration["Updates:CurrentVersion"];
        if (!string.IsNullOrWhiteSpace(configuredVersion))
        {
            settings.CurrentVersion = configuredVersion.Trim();
            settingsStore.Save(settings);
        }

        var allowedHosts = configuration.GetSection("Updates:AllowedHosts").Get<string[]>()
                           ?? settings.AllowedHosts.ToArray();
        settings.AllowedHosts = allowedHosts.ToList();
        if (!string.IsNullOrWhiteSpace(configuration["Updates:CheckEndpoint"]))
        {
            settings.CheckEndpoint = configuration["Updates:CheckEndpoint"]!;
        }

        settings.CheckIntervalHours = configuration.GetValue("Updates:CheckIntervalHours", settings.CheckIntervalHours);
        settingsStore.Save(settings);

        services.AddSingleton(settingsStore);
        services.AddSingleton(historyStore);
        services.AddSingleton(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            bool Allow(Uri uri) => UpdateUrlGuard.IsAllowed(uri, settingsStore.Load().AllowedHosts);
            return new UpdateApiService(http, Allow);
        });
        services.AddSingleton(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            bool Allow(Uri uri) => UpdateUrlGuard.IsAllowed(uri, settingsStore.Load().AllowedHosts);
            return new DownloadManager(http, Allow);
        });
        services.AddSingleton(sp =>
        {
            var downloadDir = Path.Combine(dataDir, "packages");
            return new UpdateManager(
                sp.GetRequiredService<UpdateApiService>(),
                sp.GetRequiredService<DownloadManager>(),
                settingsStore,
                historyStore,
                downloadDir,
                UpdateClientPlatform.Desktop);
        });

        services.AddSingleton<DesktopUpdateCoordinator>();

        var apiBase = configuration["Api:BaseUrl"] ?? configuration["Updates:BaseUrl"] ?? "http://localhost:5041/";
        services.AddHttpClient(HttpClientName, client =>
        {
            client.BaseAddress = new Uri(apiBase);
            client.Timeout = TimeSpan.FromMinutes(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        return services;
    }
}
