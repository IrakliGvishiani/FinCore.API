using FinCore.Application.Models.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IAnalyticsService
    {

        Task<AnalyticsOverviewDto> GetOverviewAsync(string userId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct = default
            );
    }
}
