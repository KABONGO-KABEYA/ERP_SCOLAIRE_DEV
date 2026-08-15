using System.Net.Http;

namespace SchoolManagement.Updates;

/// <summary>
/// Politique TLS du client de mise à jour (HttpClient « UpdateApi » uniquement).
/// Cible production : HTTPS avec certificat valide — aucun contournement de validation.
/// Le HTTP LAN / loopback est une compatibilité transitoire gérée par <see cref="UpdateUrlGuard"/>,
/// pas par un bypass de certificat.
/// </summary>
public static class UpdateTlsPolicy
{
    /// <summary>Toujours faux : les certificats invalides ne sont pas acceptés.</summary>
    public static bool AcceptsAnyServerCertificate => false;

    public static HttpMessageHandler CreateHandler()
    {
        return new HttpClientHandler();
    }
}
