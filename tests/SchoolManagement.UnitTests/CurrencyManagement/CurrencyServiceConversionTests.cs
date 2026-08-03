using SchoolManagement.Application.CurrencyManagement.DTOs;
using SchoolManagement.Application.CurrencyManagement.Services;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Exceptions;
using Xunit;

namespace SchoolManagement.UnitTests.CurrencyManagement;

public sealed class CurrencyServiceConversionTests
{
    [Fact]
    public async Task Convert_SameCurrency_ReturnsIdentityRate()
    {
        var cdfId = Guid.NewGuid();
        var currencies = new FakeRepository<CurrencyDefinition>(
        [
            new CurrencyDefinition
            {
                Id = cdfId,
                Code = "CDF",
                Name = "Franc",
                Symbol = "FC",
                DecimalPlaces = 0,
                IsActive = true,
                IsSystemLocal = true
            }
        ]);

        var service = new CurrencyService(
            currencies,
            new FakeRepository<SchoolCurrency>(),
            new FakeRepository<ExchangeRateType>(),
            new FakeRepository<ExchangeRate>(),
            new FakeRepository<ExchangeRateHistory>(),
            new FakeUnitOfWork());

        var result = await service.ConvertAsync(new CurrencyConversionRequest(cdfId, cdfId, 1000m));

        Assert.Equal(1m, result.AppliedRate);
        Assert.Equal(1000m, result.TargetAmount);
        Assert.Equal("CDF", result.SourceCurrencyCode);
    }

    [Fact]
    public async Task Convert_UsesOverrideRate_WhenProvided()
    {
        var usdId = Guid.NewGuid();
        var cdfId = Guid.NewGuid();
        var currencies = new FakeRepository<CurrencyDefinition>(
        [
            new CurrencyDefinition { Id = usdId, Code = "USD", Name = "Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true },
            new CurrencyDefinition { Id = cdfId, Code = "CDF", Name = "Franc", Symbol = "FC", DecimalPlaces = 0, IsActive = true, IsSystemLocal = true }
        ]);

        var service = new CurrencyService(
            currencies,
            new FakeRepository<SchoolCurrency>(),
            new FakeRepository<ExchangeRateType>(),
            new FakeRepository<ExchangeRate>(),
            new FakeRepository<ExchangeRateHistory>(),
            new FakeUnitOfWork());

        var result = await service.ConvertAsync(new CurrencyConversionRequest(
            usdId, cdfId, 100m, OverrideRate: 3250m));

        Assert.Equal(3250m, result.AppliedRate);
        Assert.Equal(325_000m, result.TargetAmount);
    }

    [Fact]
    public async Task Convert_Throws_WhenNoRate()
    {
        var usdId = Guid.NewGuid();
        var cdfId = Guid.NewGuid();
        var currencies = new FakeRepository<CurrencyDefinition>(
        [
            new CurrencyDefinition { Id = usdId, Code = "USD", Name = "Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true },
            new CurrencyDefinition { Id = cdfId, Code = "CDF", Name = "Franc", Symbol = "FC", DecimalPlaces = 0, IsActive = true }
        ]);

        var service = new CurrencyService(
            currencies,
            new FakeRepository<SchoolCurrency>(),
            new FakeRepository<ExchangeRateType>(),
            new FakeRepository<ExchangeRate>(),
            new FakeRepository<ExchangeRateHistory>(),
            new FakeUnitOfWork());

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ConvertAsync(new CurrencyConversionRequest(usdId, cdfId, 10m)));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            await action(cancellationToken);
    }

    private sealed class FakeRepository<T> : IRepository<T> where T : class
    {
        private readonly List<T> _items;

        public FakeRepository(IEnumerable<T>? items = null) => _items = items?.ToList() ?? [];

        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop is null) return Task.FromResult<T?>(null);
            var match = _items.FirstOrDefault(x => (Guid)prop.GetValue(x)! == id);
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<T>>(_items.ToList());

        public Task<IReadOnlyList<T>> FindAsync(
            System.Linq.Expressions.Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<T>>(_items.AsQueryable().Where(predicate).ToList());

        public Task<IReadOnlyList<T>> FindIncludingDeletedAsync(
            System.Linq.Expressions.Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            FindAsync(predicate, cancellationToken);

        public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Remove(entity);
            return Task.CompletedTask;
        }
    }
}
