namespace SchoolManagement.Application.EnrollmentWizard.Interfaces;

/// <summary>
/// Allocation concurrent-safe des matricules élèves au format ELV-YYYY-#####.
/// </summary>
public interface IRegistrationNumberAllocator
{
    /// <summary>
    /// Aperçu non réservé du prochain matricule (GET wizard).
    /// Ne consomme pas le compteur : un abandon de wizard ne perd aucun numéro.
    /// </summary>
    Task<string> PreviewNextAsync(
        Guid schoolId,
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alloue définitivement le prochain matricule (POST complete).
    /// Garantit l'unicité sous concurrence via verrouillage SQL Server.
    /// </summary>
    Task<string> AllocateAsync(
        Guid schoolId,
        int year,
        CancellationToken cancellationToken = default);
}
