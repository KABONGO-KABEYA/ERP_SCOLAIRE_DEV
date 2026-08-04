using System.Net.Http.Json;
using System.Text.Json;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Services;

namespace SchoolManagement.Bootstrap.API.Services;

public sealed class BootstrapOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SchoolRegistry _registry;
    private readonly BootstrapSessionStore _sessions;
    private readonly IBootstrapRelayOutboundAuth _relayAuth;

    public BootstrapOrchestrator(
        IHttpClientFactory httpClientFactory,
        SchoolRegistry registry,
        BootstrapSessionStore sessions,
        IBootstrapRelayOutboundAuth relayAuth)
    {
        _httpClientFactory = httpClientFactory;
        _registry = registry;
        _sessions = sessions;
        _relayAuth = relayAuth;
    }

    public async Task<ActivationSessionDto> StartAsync(
        ActivationStartRequest mobileRequest,
        CancellationToken cancellationToken)
    {
        var schoolId = ActivationTokenRoutingReader.TryReadSchoolId(mobileRequest.Token);
        var tokenId = ActivationTokenRoutingReader.TryReadTokenId(mobileRequest.Token);
        var school = _registry.Resolve(schoolId);

        var bootstrapSessionId = Guid.NewGuid();
        var relayRequest = new ActivationStartRequest(
            mobileRequest.Token,
            mobileRequest.DeviceId,
            bootstrapSessionId,
            mobileRequest.ClientHints);

        var client = _httpClientFactory.CreateClient("school-relay");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{school.ActivationBaseUrl.TrimEnd('/')}/api/v1/activation/start");
        _relayAuth.Apply(httpRequest);
        httpRequest.Content = JsonContent.Create(relayRequest, options: JsonOptions);

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"École injoignable ou token refusé ({(int)response.StatusCode}) : {body}");
        }

        var schoolSession = JsonSerializer.Deserialize<ActivationSessionDto>(body, JsonOptions)
                            ?? throw new InvalidOperationException("Réponse activation/start invalide.");

        _sessions.Create(
            schoolId,
            schoolSession.ActivationSessionId,
            tokenId,
            mobileRequest.DeviceId,
            schoolSession.ExpiresAt);

        return new ActivationSessionDto(
            bootstrapSessionId,
            tokenId,
            mobileRequest.DeviceId,
            schoolId,
            "pending",
            DateTime.UtcNow,
            schoolSession.ExpiresAt,
            mobileRequest.ClientHints);
    }

    public async Task<SchoolBindingDto> CompleteAsync(
        ActivationCompleteRequest mobileRequest,
        CancellationToken cancellationToken)
    {
        var state = _sessions.Get(mobileRequest.ActivationSessionId);
        if (!string.Equals(state.DeviceId, mobileRequest.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DeviceId incompatible.");
        }

        var school = _registry.Resolve(state.SchoolId);
        var relayRequest = new ActivationCompleteRequest(
            state.SchoolActivationSessionId,
            mobileRequest.DeviceId);

        var client = _httpClientFactory.CreateClient("school-relay");
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{school.ActivationBaseUrl.TrimEnd('/')}/api/v1/activation/complete");
        _relayAuth.Apply(httpRequest);
        httpRequest.Content = JsonContent.Create(relayRequest, options: JsonOptions);

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Finalisation activation refusée ({(int)response.StatusCode}) : {body}");
        }

        var binding = JsonSerializer.Deserialize<SchoolBindingDto>(body, JsonOptions)
                      ?? throw new InvalidOperationException("Réponse activation/complete invalide.");

        if (string.IsNullOrWhiteSpace(binding.CloudBaseUrl)
            && !string.IsNullOrWhiteSpace(school.CloudBaseUrl))
        {
            binding = binding with { CloudBaseUrl = school.CloudBaseUrl.TrimEnd('/') };
        }

        _sessions.MarkCompleted(mobileRequest.ActivationSessionId);
        return binding;
    }
}
