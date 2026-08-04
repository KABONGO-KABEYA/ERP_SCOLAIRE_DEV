namespace SchoolManagement.Application.ParentActivation.BootstrapRelay;

/// <summary>Contrat d'authentification relay Bootstrap → API école (évolution prévue : JWT de service).</summary>
public static class BootstrapRelayAuthConstants
{
    /// <summary>
    /// En-tête provisoire — clé partagée statique (dette <c>TD-RELAY-01</c>).
    /// </summary>
    public const string LegacySharedKeyHeaderName = "X-Bootstrap-Relay-Key";

    /// <summary>Schéma HTTP cible pour un jeton de service signé (non implémenté).</summary>
    public const string ServiceAuthorizationHeaderName = "Authorization";

    /// <summary>Valeur attendue du claim <c>typ</c> / scope pour un futur JWT relay.</summary>
    public const string ServiceTokenTypeClaimValue = "bootstrap_relay";
}
