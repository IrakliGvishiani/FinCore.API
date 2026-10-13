using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Text;

namespace FinCore.Application.Contracts.Persistence
{
    public interface IRepositoryBase<T> where T : class
    {

        Task<(IEnumerable<T> Items, int TotalCount)> GetAllAsync(
Expression<Func<T, bool>> filter = null,
int? pageNumber = null,
int? pageSize = null,
Expression<Func<T, object>> orderBy = null,
bool ascending = true,
CancellationToken cancellationToken = default,
bool tracking = true,
params Func<IQueryable<T>, IQueryable<T>>[] includes);


        Task<T?> GetAsync(
            Expression<Func<T, bool>> filter,
            bool tracking = true,
            CancellationToken cancellationToken = default,
            params Func<IQueryable<T>, IQueryable<T>>[] includes);
        Task AddAsync(T entity,CancellationToken cancellationToken = default);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate,CancellationToken cancellationToken = default);

        Task<int> CountAsync(
    Expression<Func<T, bool>> predicate = null,CancellationToken cancellationToken = default);
    }
}

