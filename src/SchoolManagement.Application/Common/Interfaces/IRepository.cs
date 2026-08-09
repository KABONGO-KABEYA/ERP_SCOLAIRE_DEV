using System.Linq.Expressions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Common.Interfaces;

public interface IRepository<T> where T : class
{
    /// <summary>
    /// Charge une entité par Id en respectant les filtres globaux (tenant + soft-delete).
    /// Pour les entités sans SchoolId direct, le filtre indirect EF s'applique automatiquement.
    /// Préférez <see cref="SchoolScopedRepositoryExtensions.GetByIdForSchoolAsync{T}"/> ou
    /// <see cref="ISchoolTenancyService.RequireForSchoolAsync{T}"/> lorsque l'établissement est connu.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindIncludingDeletedAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork : IAsyncDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute l'action dans une transaction compatible avec SqlServerRetryingExecutionStrategy.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}

public interface ICurrentUserService
{
    Guid? UserId { get; }

    Guid? SchoolId { get; }

    string? UserName { get; }

    IReadOnlyList<string> Permissions { get; }

    IReadOnlyList<string> Roles { get; }

    bool HasPermission(string permission);

    /// <summary>Alias compatibilité : équivalent à <see cref="HasPermission"/> avec <c>admin.full</c> uniquement (plus de fallback rôle JWT).</summary>
    bool IsAdministrator { get; }
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
