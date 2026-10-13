using FinCore.Application.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FinCore.Infrastructure.Persistence
{
    public class RepositoryBase<T> : IRepositoryBase<T> where T : class
    {
        private readonly DbSet<T> _dbSet;

        public RepositoryBase(ApplicationDbContext context)
        {
            _dbSet = context.Set<T>();
        }

        public async Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            int? pageNumber = null,
            int? pageSize = null,
            Expression<Func<T, object>>? orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default,
            bool tracking = true,
            params Func<IQueryable<T>, IQueryable<T>>[] includes)
        {
            IQueryable<T> query = _dbSet;

            if (!tracking)
                query = query.AsNoTracking();

            if (filter != null)
                query = query.Where(filter);

            int totalCount = await query.CountAsync(cancellationToken);

            foreach (var include in includes)
                query = include(query);

            if (orderBy != null)
            {
                query = ascending
                    ? query.OrderBy(orderBy)
                    : query.OrderByDescending(orderBy);
            }

            if (pageNumber.HasValue && pageSize.HasValue)
            {
                if (orderBy == null)
                    throw new ArgumentException("OrderBy is required for pagination.");

                int page = Math.Max(pageNumber.Value, 1);
                int size = Math.Clamp(pageSize.Value, 1, 100);

                query = query.Skip((page - 1) * size).Take(size);
            }

            var items = await query.ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public async Task<T?> GetAsync(
            Expression<Func<T, bool>> filter,
            bool tracking = true,
            CancellationToken cancellationToken = default,
            params Func<IQueryable<T>, IQueryable<T>>[] includes)
        {
            IQueryable<T> query = _dbSet;

            if (!tracking)
                query = query.AsNoTracking();

            foreach (var include in includes)
                query = include(query);

            return await query.FirstOrDefaultAsync(filter, cancellationToken);
        }

        public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
            => await _dbSet.AddAsync(entity, cancellationToken);

        public void Remove(T entity) => _dbSet.Remove(entity);

        public void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);

        public Task<bool> ExistsAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
            => _dbSet.AnyAsync(predicate, cancellationToken);

        public Task<int> CountAsync(
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
            => predicate == null
                ? _dbSet.CountAsync(cancellationToken)
                : _dbSet.CountAsync(predicate, cancellationToken);
    }
}