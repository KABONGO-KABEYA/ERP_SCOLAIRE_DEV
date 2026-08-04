namespace SchoolManagement.Application.ParentActivation.BootstrapRelay;

/// <summary>
/// Futur : émission d'un JWT de service Bootstrap pour relay vers une école cible.
/// Non implémenté — voir <c>docs/architecture/bootstrap-relay-auth-evolution.md</c>.
/// </summary>
public interface IBootstrapRelayServiceTokenIssuer
{
    /// <summary>
    /// Produit un jeton court (ex. JWT) incluant au minimum <c>schoolId</c>, <c>aud</c>, <c>iss</c>, <c>exp</c>.
    /// </summary>
    Task<string> IssueAsync(
        Guid schoolId,
        string relayOperation,
        CancellationToken cancellationToken = default);
}
