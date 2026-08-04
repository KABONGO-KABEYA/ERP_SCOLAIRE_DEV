using Microsoft.Extensions.Options;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;

namespace SchoolManagement.API.Services.BootstrapRelay;

/// <summary>Validation provisoire par comparaison de clé statique (<c>TD-RELAY-01</c>).</summary>
public sealed class StaticSharedKeyBootstrapRelayRequestValidator : IBootstrapRelayRequestValidator
{
    private readonly BootstrapRelaySchoolOptions _options;

    public StaticSharedKeyBootstrapRelayRequestValidator(IOptions<BootstrapRelaySchoolOptions> options)
    {
        _options = options.Value;
    }

    public Task<BootstrapRelayValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string?> requestHeaders,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(_options.BootstrapRelayKey))
        {
            return Task.FromResult(BootstrapRelayValidationResult.Fail(
                "Activation relay non configurée (Activation:BootstrapRelayKey).",
                httpStatusCode: 503));
        }

        if (!TryGetHeader(requestHeaders, BootstrapRelayAuthConstants.LegacySharedKeyHeaderName, out var provided)
            || !string.Equals(provided, _options.BootstrapRelayKey, StringComparison.Ordinal))
        {
            return Task.FromResult(BootstrapRelayValidationResult.Fail(
                "Clé relay Bootstrap invalide.",
                httpStatusCode: 401));
        }

        return Task.FromResult(BootstrapRelayValidationResult.Ok());
    }

    private static bool TryGetHeader(
        IReadOnlyDictionary<string, string?> headers,
        string headerName,
        out string? value)
    {
        value = null;
        foreach (var pair in headers)
        {
            if (!string.Equals(pair.Key, headerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = pair.Value;
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}
