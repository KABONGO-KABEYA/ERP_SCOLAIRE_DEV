namespace SchoolManagement.Application.ParentActivation.BootstrapRelay;

/// <summary>Configuration API école — validation des appels relay Bootstrap.</summary>
public sealed class BootstrapRelaySchoolOptions
{
    public const string SectionName = "Activation";

    /// <summary>Clé partagée provisoire (<c>TD-RELAY-01</c>). Futur : clés publiques / JWKS Bootstrap.</summary>
    public string BootstrapRelayKey { get; set; } = string.Empty;
}
