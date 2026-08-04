using Microsoft.Extensions.Options;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;
using SchoolManagement.Bootstrap.API.Options;

namespace SchoolManagement.Bootstrap.API.Services;

/// <summary>Authentification relay sortante provisoire (<c>TD-RELAY-01</c>).</summary>
public sealed class StaticSharedKeyBootstrapRelayOutboundAuth : IBootstrapRelayOutboundAuth
{
    private readonly BootstrapOptions _options;

    public StaticSharedKeyBootstrapRelayOutboundAuth(IOptions<BootstrapOptions> options)
    {
        _options = options.Value;
    }

    public void Apply(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_options.RelayApiKey))
        {
            throw new InvalidOperationException(
                "Bootstrap relay non configuré (Bootstrap:RelayApiKey).");
        }

        request.Headers.Remove(BootstrapRelayAuthConstants.LegacySharedKeyHeaderName);
        request.Headers.Add(BootstrapRelayAuthConstants.LegacySharedKeyHeaderName, _options.RelayApiKey);
    }
}
