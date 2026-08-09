using System.Net.Http;
using System.Net.Http.Json;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.Desktop.Services;

public sealed class SecurityNavigationApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SecurityNavigationApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NavigationTreeDto> GetDesktopNavigationAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync("api/v1/security/navigation?channel=Desktop", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NavigationTreeDto>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse navigation invalide.");

        if (!body.Success || body.Data is null)
        {
            throw new InvalidOperationException(body.Message ?? "Impossible de charger la navigation.");
        }

        return body.Data;
    }
}
