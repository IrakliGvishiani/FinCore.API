using FinCore.Application.Contracts.Persistence;
using System.Linq.Expressions;

namespace FinCore.Tests.Support;

/// <summary>
/// List-backed <see cref="IRepositoryBase{T}"/> that evaluates the real filter expressions,
/// so services can be tested without mocking every query by hand.
/// Ids are assigned on <see cref="AddAsync"/> (EF would assign them on SaveChanges).
/// Note: "tracking: false" returns the same instance; tests must not rely on detached copies.
/// </summary>
internal sealed class InMemoryRepository<T> : IRepositoryBase<T> where T : class
{
    private int _nextId = 1000;

    public List<T> Items { get; } = new();
    public List<T> Added { get; } = new();

    public InMemoryRepository<T> Seed(T entity, int id)
    {
        TestEntity.SetId(entity, id);
        Items.Add(entity);
        return this;
    }

    public Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        int? pageNumber = null,
        int? pageSize = null,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = true,
        CancellationToken cancellationToken = default,
        bool tracking = true,
        params Func<IQueryable<T>, IQueryable<T>>[] includes)
    {
        IEnumerable<T> query = filter is null ? Items : Items.AsQueryable().Where(filter);
        var list = query.ToList();
        var total = list.Count;

        if (orderBy is not null)
        {
            var key = orderBy.Compile();
            list = (ascending ? list.OrderBy(key) : list.OrderByDescending(key)).ToList();
        }

        if (pageNumber.HasValue && pageSize.HasValue)
        {
            var page = Math.Max(pageNumber.Value, 1);
            var size = Math.Clamp(pageSize.Value, 1, 100);
            list = list.Skip((page - 1) * size).Take(size).ToList();
        }

        return Task.FromResult<(IEnumerable<T>, int)>((list, total));
    }

    public Task<T?> GetAsync(
        Expression<Func<T, bool>> filter,
        bool tracking = true,
        CancellationToken cancellationToken = default,
        params Func<IQueryable<T>, IQueryable<T>>[] includes)
        => Task.FromResult(Items.AsQueryable().FirstOrDefault(filter));

    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (TestEntity.GetId(entity) == 0)
            TestEntity.SetId(entity, _nextId++);
        Items.Add(entity);
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public void Remove(T entity) => Items.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities)
    {
        foreach (var e in entities.ToList()) Items.Remove(e);
    }

    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.AsQueryable().Any(predicate));

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => Task.FromResult(predicate is null ? Items.Count : Items.AsQueryable().Count(predicate));

    public Task<int> CountDistinctAsync<TKey>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TKey>> selector,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Items.AsQueryable().Where(predicate).Select(selector).Distinct().Count());
}

/// <summary>Records unit-of-work calls; never touches a database.</summary>
internal sealed class RecordingUnitOfWork : IUnitOfWork
{
    public int Begins { get; private set; }
    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }
    public int Saves { get; private set; }
    public int ClearTrackingCalls { get; private set; }

    /// <summary>Optional hook run on every SaveChanges (e.g. to throw a simulated conflict).</summary>
    public Func<int, Task>? OnSave { get; set; }

    public Task BeginTransactionAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        Begins++;
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken ct = default)
    {
        Commits++;
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        Rollbacks++;
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Saves++;
        if (OnSave is not null) await OnSave(Saves);
        return 1;
    }

    public Task ClearTracking()
    {
        ClearTrackingCalls++;
        return Task.CompletedTask;
    }
}

internal static class TestEntity
{
    public static void SetId<T>(T entity, int id) =>
        entity!.GetType().GetProperty("Id")!.SetValue(entity, id);

    public static int GetId<T>(T entity) =>
        (int)entity!.GetType().GetProperty("Id")!.GetValue(entity)!;
}
