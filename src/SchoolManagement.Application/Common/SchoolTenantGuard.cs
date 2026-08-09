namespace SchoolManagement.Application.Common;

using SchoolManagement.Domain.Common;

public static class SchoolTenantGuard
{
    public static void EnsureSameSchool(Guid entitySchoolId, Guid expectedSchoolId, string? entityHint = null)
    {
        if (entitySchoolId != expectedSchoolId)
        {
            throw new UnauthorizedAccessException(
                entityHint is null
                    ? "Accès refusé : donnée d'un autre établissement."
                    : $"Accès refusé : {entityHint} appartient à un autre établissement.");
        }
    }

    public static void EnsureSameSchool(ISchoolScoped entity, Guid expectedSchoolId, string? entityHint = null) =>
        EnsureSameSchool(entity.SchoolId, expectedSchoolId, entityHint);

    public static void EnsureNotEmpty(Guid schoolId)
    {
        if (schoolId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Établissement non identifié.");
        }
    }
}
