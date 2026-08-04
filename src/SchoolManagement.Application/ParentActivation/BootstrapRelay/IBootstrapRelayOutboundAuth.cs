namespace SchoolManagement.Application.ParentActivation.BootstrapRelay;

/// <summary>
/// Applique les en-têtes d'authentification aux requêtes sortantes Bootstrap → API école.
/// Implémentation actuelle : clé partagée (<see cref="StaticSharedKeyBootstrapRelayOutboundAuth"/>).
/// </summary>
public interface IBootstrapRelayOutboundAuth
{
    void Apply(HttpRequestMessage request);
}
