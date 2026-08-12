using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.Application.Parent;

/// <summary>
/// Contexte école obligatoire pour les API parent (architecture v2 §4.9 / étape 7).
/// </summary>
public static class ParentApiSchoolContext
{
    public static Guid RequireSchoolId(ICurrentUserService currentUser) =>
        currentUser.SchoolId
        ?? throw new UnauthorizedAccessException("Contexte école (SchoolId) requis.");

    public static void EnsureResourceSchool(Guid resourceSchoolId, Guid expectedSchoolId)
    {
        if (resourceSchoolId != expectedSchoolId)
        {
            throw new UnauthorizedAccessException("Ressource hors contexte école.");
        }
    }

    /// <summary>
    /// Contrôle Parent ↔ Élève + SchoolId (pas de filtrage client).
    /// </summary>
    public static void EnsureChildAccess(bool hasGuardianLink, Guid? studentSchoolId, Guid schoolId)
    {
        if (!hasGuardianLink)
        {
            throw new UnauthorizedAccessException("Accès non autorisé à cet élève.");
        }

        if (studentSchoolId is null || studentSchoolId.Value != schoolId)
        {
            throw new UnauthorizedAccessException("Élève hors contexte école.");
        }
    }
}
