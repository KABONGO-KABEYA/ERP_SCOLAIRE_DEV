namespace SchoolManagement.Application.ParentActivation.BootstrapRelay;

/// <summary>
/// Valide qu'une requête HTTP vers les endpoints relay école provient du Bootstrap autorisé.
/// Implémentation actuelle : clé partagée (<see cref="StaticSharedKeyBootstrapRelayRequestValidator"/>).
/// Évolution : JWT de service signé (même interface, nouvelle implémentation + mode dual).
/// </summary>
public interface IBootstrapRelayRequestValidator
{
    Task<BootstrapRelayValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, string?> requestHeaders,
        CancellationToken cancellationToken = default);
}
