using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;
using SchoolManagement.Application.SchoolEstablishment;

namespace SchoolManagement.Infrastructure.SchoolEstablishment;

public sealed class BootstrapSchoolRegistryClient : IBootstrapSchoolRegistryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly SchoolBootstrapRegistryOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BootstrapSchoolRegistryClient> _logger;

    public BootstrapSchoolRegistryClient(
        HttpClient http,
        IOptions<SchoolBootstrapRegistryOptions> options,
        IConfiguration configuration,
        ILogger<BootstrapSchoolRegistryClient> logger)
    {
        _http = http;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task UpsertSchoolAsync(
        BootstrapRegistryUpsertPayload payload,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = RequireBaseUrl();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/registry/schools/upsert");
        ApplyRelayKey(request);
        request.Content = JsonContent.Create(new
        {
            schoolId = payload.SchoolId,
            schoolName = payload.SchoolName,
            activationBaseUrl = payload.ActivationBaseUrl,
            cloudBaseUrl = payload.CloudBaseUrl,
            publicKeyFingerprint = payload.PublicKeyFingerprint,
            keyVersion = payload.KeyVersion,
            serverInstanceId = payload.ServerInstanceId,
            licenseId = payload.LicenseId,
            credential = new
            {
                credentialId = payload.Credential.CredentialId,
                credentialVersion = payload.Credential.CredentialVersion,
                secretHash = payload.Credential.SecretHash,
                tokenType = payload.Credential.TokenType,
            },
        }, options: JsonOptions);

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "upsert", payload.SchoolId, cancellationToken);
    }

    public async Task RotateCredentialAsync(
        Guid schoolId,
        BootstrapRegistryCredentialPayload credential,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = RequireBaseUrl();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/registry/schools/{schoolId:D}/credentials/rotate");
        ApplyRelayKey(request);
        request.Content = JsonContent.Create(new
        {
            reason,
            credential = new
            {
                credentialId = credential.CredentialId,
                credentialVersion = credential.CredentialVersion,
                secretHash = credential.SecretHash,
                tokenType = credential.TokenType,
            },
        }, options: JsonOptions);

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "rotate", schoolId, cancellationToken);
    }

    private string RequireBaseUrl()
    {
        var baseUrl = _options.RegistryBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "Bootstrap:RegistryBaseUrl non configuré — publication registre impossible.");
        }

        return baseUrl;
    }

    private void ApplyRelayKey(HttpRequestMessage request)
    {
        var key = FirstNonEmpty(_options.RelayApiKey, _configuration["Activation:BootstrapRelayKey"]);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Bootstrap relay key absente (Bootstrap:RelayApiKey ou Activation:BootstrapRelayKey).");
        }

        request.Headers.Remove(BootstrapRelayAuthConstants.LegacySharedKeyHeaderName);
        request.Headers.TryAddWithoutValidation(
            BootstrapRelayAuthConstants.LegacySharedKeyHeaderName,
            key);
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Bootstrap registry {Operation} OK pour école {SchoolId}",
                operation,
                schoolId);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Bootstrap registry {Operation} échoué pour école {SchoolId} — HTTP {Status} : {Body}",
            operation,
            schoolId,
            (int)response.StatusCode,
            Truncate(body, 500));

        throw new BootstrapRegistryClientException(
            response.StatusCode,
            $"Publication Bootstrap ({operation}) refusée ({(int)response.StatusCode}).");
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= max ? value
        : value[..max] + "…";
}

public sealed class BootstrapRegistryClientException : Exception
{
    public BootstrapRegistryClientException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

/// <summary>Résolution des URLs publiées vers le registre (ActivationBaseUrl / CloudBaseUrl).</summary>
public sealed class SchoolBootstrapPublishUrls
{
    private readonly SchoolBootstrapRegistryOptions _options;
    private readonly IConfiguration _configuration;

    public SchoolBootstrapPublishUrls(
        IOptions<SchoolBootstrapRegistryOptions> options,
        IConfiguration configuration)
    {
        _options = options.Value;
        _configuration = configuration;
    }

    public string ActivationBaseUrl =>
        FirstNonEmpty(_options.ActivationBaseUrl, _configuration["PublicBaseUrl"])?.TrimEnd('/')
        ?? string.Empty;

    public string CloudBaseUrl =>
        FirstNonEmpty(_options.CloudBaseUrl, _configuration["Activation:CloudBaseUrl"])?.TrimEnd('/')
        ?? string.Empty;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
