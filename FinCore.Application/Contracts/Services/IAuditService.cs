using FinCore.Application.Models.Audit;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IAuditService
    {
        Task LogAsync(
            string? userId,
            string action,
            string entityName,
            string? entityId = null,
            string? details = null
            );

        Task RecordAsync(
            string? userId,string action,string entityName,string? entityId = null,
            string? details = null,CancellationToken ct = default
            );

        Task<(IEnumerable<AuditLogDto> Items, int TotalCount)> GetLogsAsync(
            AuditFilter filter,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken ct = default
            );
    }
}
