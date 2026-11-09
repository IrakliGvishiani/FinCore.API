using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace FinCore.Application.Contracts.Persistence
{
    public interface IUnitOfWork
    {
        Task BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken ct = default);
        Task CommitTransactionAsync(CancellationToken ct = default);
        Task RollbackTransactionAsync(CancellationToken ct = default);

        Task<int> SaveChangesAsync(CancellationToken ct = default);

        Task ClearTracking();
    }
}
