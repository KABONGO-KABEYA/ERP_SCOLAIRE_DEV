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
}
