using FinCore.Application.Contracts.Persistence;
using System.Linq.Expressions;
using System.Reflection;

namespace FinCore.Tests.Support;

/// <summary>Something whose state a fake transaction can snapshot and roll back.</summary>
internal interface ITransactionalStore
{
    void TakeSnapshot();
    void RestoreSnapshot();
    void DiscardSnapshot();
}

/// <summary>
/// List-backed <see cref="IRepositoryBase{T}"/> that evaluates the real filter expressions.
/// Ids are assigned on <see cref="AddAsync"/>. "tracking: false" returns the same instance.
/// Together with <see cref="RecordingUnitOfWork"/> it behaves like a transaction: on rollback the
/// item list AND every field of every entity (balances, statuses, ...) are restored, so a retried
/// attempt starts from committed state exactly as with a real database.
/// </summary>
internal sealed class InMemoryRepository<T> : IRepositoryBase<T>, ITransactionalStore where T : class
{
    private int _nextId = 1000;
    private List<T>? _itemsSnapshot;
    private Dictionary<object, object?[]>? _fieldSnapshot;

    public List<T> Items { get; } = new();
    /// <summary>Every entity ever passed to AddAsync (including rolled-back attempts).</summary>
    public List<T> Added { get; } = new();

    /// <summary>When true, any write throws: proves an operation is read-only.</summary>
    public bool ThrowOnWrite { get; set; }

    public InMemoryRepository<T> Seed(T entity, int id)
    {
        TestEntity.SetId(entity, id);
        Items.Add(entity);
        return this;
    }

    /// <summary>Adds an entity as if another request had already committed it (survives rollbacks).</summary>
    public void SeedCommitted(T entity)
    {
        if (TestEntity.GetId(entity) == 0) TestEntity.SetId(entity, _nextId++);
        Items.Add(entity);
        _itemsSnapshot?.Add(entity);
        _fieldSnapshot?.Add(entity, FieldSnapshot.Capture(entity));
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
        EnsureWritable();
        if (TestEntity.GetId(entity) == 0)
            TestEntity.SetId(entity, _nextId++);
        Items.Add(entity);
        Added.Add(entity);
        return Task.CompletedTask;
    }

    public void Remove(T entity) { EnsureWritable(); Items.Remove(entity); }

    public void RemoveRange(IEnumerable<T> entities)
    {
        EnsureWritable();
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

    public void TakeSnapshot()
    {
        _itemsSnapshot = Items.ToList();
        _fieldSnapshot = Items.ToDictionary(x => (object)x, x => FieldSnapshot.Capture(x), ReferenceEqualityComparer.Instance);
    }

    public void RestoreSnapshot()
    {
        if (_itemsSnapshot is null || _fieldSnapshot is null) return;
        Items.Clear();
        Items.AddRange(_itemsSnapshot);
        foreach (var (entity, values) in _fieldSnapshot)
            FieldSnapshot.Restore(entity, values);
        DiscardSnapshot();
    }

    public void DiscardSnapshot()
    {
        _itemsSnapshot = null;
        _fieldSnapshot = null;
    }

    private void EnsureWritable()
    {
        if (ThrowOnWrite)
            throw new InvalidOperationException($"Write to {typeof(T).Name} repository during a read-only operation.");
    }
}

internal static class FieldSnapshot
{
    private static FieldInfo[] Fields(Type type)
    {
        var fields = new List<FieldInfo>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        return fields.ToArray();
    }

    public static object?[] Capture(object entity) => Fields(entity.GetType()).Select(f => f.GetValue(entity)).ToArray();

    public static void Restore(object entity, object?[] values)
    {
        var fields = Fields(entity.GetType());
        for (var i = 0; i < fields.Length; i++) fields[i].SetValue(entity, values[i]);
    }
}

/// <summary>
/// Fake unit of work with real transaction semantics over the registered stores:
/// Begin snapshots, Rollback restores, Commit keeps. Records every call.
/// </summary>
internal sealed class RecordingUnitOfWork : IUnitOfWork
{
    private readonly ITransactionalStore[] _stores;
    private bool _inTransaction;

    public RecordingUnitOfWork(params ITransactionalStore[] stores) => _stores = stores;

    public int Begins { get; private set; }
    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }
    public int Saves { get; private set; }
    public int ClearTrackingCalls { get; private set; }

    /// <summary>Optional hook run on every SaveChanges (1-based call number), e.g. to throw a simulated conflict.</summary>
    public Func<int, Task>? OnSave { get; set; }

    public Task BeginTransactionAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_inTransaction) throw new InvalidOperationException("Transaction already started.");
        _inTransaction = true;
        Begins++;
        foreach (var s in _stores) s.TakeSnapshot();
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (!_inTransaction) throw new InvalidOperationException("No active transaction.");
        _inTransaction = false;
        Commits++;
        foreach (var s in _stores) s.DiscardSnapshot();
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (!_inTransaction) return Task.CompletedTask;
        _inTransaction = false;
        Rollbacks++;
        foreach (var s in _stores) s.RestoreSnapshot();
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

/// <summary>A unit of work that fails the test if anything uses it.</summary>
internal sealed class ThrowingUnitOfWork : IUnitOfWork
{
    private static Exception Used(string what) => new InvalidOperationException($"Unit of work used during a read-only operation: {what}");
    public Task BeginTransactionAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default) => throw Used("BeginTransaction");
    public Task CommitTransactionAsync(CancellationToken ct = default) => throw Used("Commit");
    public Task RollbackTransactionAsync(CancellationToken ct = default) => throw Used("Rollback");
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => throw Used("SaveChanges");
    public Task ClearTracking() => throw Used("ClearTracking");
}

internal static class TestEntity
{
    public static void SetId<T>(T entity, int id) =>
        entity!.GetType().GetProperty("Id")!.SetValue(entity, id);

    public static int GetId<T>(T entity) =>
        (int)entity!.GetType().GetProperty("Id")!.GetValue(entity)!;
}
