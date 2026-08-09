namespace SchoolManagement.Application.Common;

/// <summary>
/// Accès refusé ou ressource absente pour l'établissement courant (ne pas distinguer en API).
/// </summary>
public sealed class SchoolTenancyAccessDeniedException : UnauthorizedAccessException
{
    public SchoolTenancyAccessDeniedException()
        : base("Ressource introuvable ou accès refusé pour cet établissement.")
    {
    }

    public SchoolTenancyAccessDeniedException(string entityName)
        : base($"Ressource introuvable ou accès refusé pour cet établissement ({entityName}).")
    {
    }
}
