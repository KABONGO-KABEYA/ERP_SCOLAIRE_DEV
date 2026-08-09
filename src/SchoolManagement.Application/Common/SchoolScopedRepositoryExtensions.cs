namespace SchoolManagement.Application.Common;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Common;

public static class SchoolScopedRepositoryExtensions
{
    public static async Task<T?> GetByIdForSchoolAsync<T>(
        this IRepository<T> repository,
        Guid schoolId,
        Guid id,
        CancellationToken cancellationToken = default)
        where T : AuditableEntity, ISchoolScoped
    {
        var matches = await repository.FindAsync(e => e.Id == id && e.SchoolId == schoolId, cancellationToken);
        return matches.FirstOrDefault();
    }

    public static async Task<T> RequireByIdForSchoolAsync<T>(
        this IRepository<T> repository,
        Guid schoolId,
        Guid id,
        CancellationToken cancellationToken = default)
        where T : AuditableEntity, ISchoolScoped
    {
        var entity = await repository.GetByIdForSchoolAsync(schoolId, id, cancellationToken);
        if (entity is null)
        {
            throw new SchoolTenancyAccessDeniedException(typeof(T).Name);
        }

        return entity;
    }

    public static void EnsureEntityBelongsToSchool<T>(T entity, Guid schoolId)
        where T : ISchoolScoped
    {
        SchoolTenantGuard.EnsureSameSchool(entity, schoolId, typeof(T).Name);
    }

    public static async Task<T> RequireForSchoolAsync<T>(
        this ISchoolTenancyService tenancy,
        Guid schoolId,
        Guid id,
        CancellationToken cancellationToken = default)
        where T : AuditableEntity =>
        await tenancy.RequireForSchoolAsync<T>(schoolId, id, cancellationToken);
}
