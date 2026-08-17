using System.Linq.Expressions;
using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.UnitTests.TestSupport;

internal sealed class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly List<T> _items;

    public InMemoryRepository(List<T> items) => _items = items;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var prop = typeof(T).GetProperty("Id");
        var found = _items.FirstOrDefault(x => prop?.GetValue(x) is Guid gid && gid == id);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<T>>(_items.ToList());

    public Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<T>>(_items.Where(predicate.Compile()).ToList());

    public Task<IReadOnlyList<T>> FindIncludingDeletedAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        FindAsync(predicate, cancellationToken);

    public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        _items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _items.Remove(entity);
        return Task.CompletedTask;
    }
}

internal sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) =>
        await action(cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
