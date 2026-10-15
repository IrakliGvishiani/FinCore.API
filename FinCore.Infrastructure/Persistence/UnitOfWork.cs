using FinCore.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinCore.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork, IAsyncDisposable
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction? _transaction;

        public UnitOfWork(ApplicationDbContext context) => _context = context;

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            if (_transaction != null)
                throw new InvalidOperationException("Transaction already started.");
            _transaction = await _context.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitTransactionAsync(CancellationToken ct = default)
        {
            if (_transaction == null)
                throw new InvalidOperationException("No active transaction.");
            try { await _transaction.CommitAsync(ct); }
            finally { await ReleaseTransactionAsync(); }
        }

        public async Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            if (_transaction == null) return;
            try { await _transaction.RollbackAsync(ct); }
            finally { await ReleaseTransactionAsync(); }
        }

        private async Task ReleaseTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public ValueTask DisposeAsync() => new(ReleaseTransactionAsync());

        public async Task ClearTracking()
        {
            _context.ChangeTracker.Clear();
        }
    }
}