namespace SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Domain.Common;

/// <summary>
/// Vérifications explicites d'appartenance à un établissement (entités directes et indirectes).
/// </summary>
public interface ISchoolTenancyService
{
    /// <summary>Vérifie que l'entité appartient à l'école ; sinon lève <see cref="SchoolTenancyAccessDeniedException"/>.</summary>
    Task EnsureBelongsToSchoolAsync<TEntity>(Guid schoolId, Guid entityId, CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity;

    /// <summary>Charge l'entité si elle appartient à l'école ; sinon null.</summary>
    Task<TEntity?> TryGetForSchoolAsync<TEntity>(Guid schoolId, Guid entityId, CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity;

    /// <summary>Charge l'entité ou lève <see cref="SchoolTenancyAccessDeniedException"/>.</summary>
    Task<TEntity> RequireForSchoolAsync<TEntity>(Guid schoolId, Guid entityId, CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity;

    Task<Guid?> TryResolveSchoolIdAsync<TEntity>(Guid entityId, CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity;
}
