using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Settings;

/// <summary>
/// Abonnement mensuel local de l'établissement (une ligne courante par SchoolId).
/// </summary>
public class SchoolSubscription : AuditableEntity, IAggregateRoot, ISchoolScoped
{
    /// <summary>
    /// Durée initiale à l'installation. Aucune convention métier existante :
    /// 12 mois = une année civile à compter de la date d'installation.
    /// </summary>
    public const int InitialDurationMonths = 12;

    public Guid SchoolId { get; set; }

    public DateTime InstallationDate { get; set; }

    public int DurationMonths { get; set; }

    public DateTime ExpirationDate { get; set; }

    public bool IsActive { get; set; } = true;

    public School School { get; set; } = null!;

    public static DateTime ComputeExpiration(DateTime installationUtc, int durationMonths) =>
        installationUtc.AddMonths(durationMonths);
}
