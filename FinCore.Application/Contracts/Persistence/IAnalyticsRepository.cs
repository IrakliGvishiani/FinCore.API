using FinCore.Application.Models.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Persistence
{
    public interface IAnalyticsRepository
    {
        Task<List<CurrencyAnalyticsDto>> GetCurrencyAnalyticsAsync(
            string userId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct = default
            );

        Task<List<MonthlyAnalyticsDto>> GetMonthlyAnalyticsAsync(
            string userId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct = default
            );

    }
}
