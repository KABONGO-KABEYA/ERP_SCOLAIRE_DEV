using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Domain.Entities.Students;

/// <summary>
/// Compteur atomique de matricules élèves par établissement et année calendaire.
/// NextValue = prochain numéro à allouer (indépendant de COUNT(Students)).
/// </summary>
public class RegistrationNumberCounter : AuditableEntity, IAggregateRoot, ISchoolScoped
{
    public Guid SchoolId { get; set; }

    /// <summary>Année calendaire (UTC) du préfixe ELV-YYYY-…</summary>
    public int Year { get; set; }

    /// <summary>Prochaine séquence à attribuer (1-based).</summary>
    public int NextValue { get; set; } = 1;

    public School School { get; set; } = null!;
}
