using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsRepository _repo;


        public AnalyticsService(IAnalyticsRepository repository)
        {
            _repo = repository;
        }
        public async Task<AnalyticsOverviewDto> GetOverviewAsync(string userId,
            DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException("User ID is required.");

            if (fromDate >= toDate)
                throw new BadRequestException(
                    "From date must be earlier than to date.");

            var currencies = await _repo.GetCurrencyAnalyticsAsync(
                userId,
                fromDate,
                toDate,
                ct
                );

            var monthlyBreakdown = await _repo.GetMonthlyAnalyticsAsync(
                userId,
                fromDate,
                toDate,
                ct
                );

            return new AnalyticsOverviewDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                Currencies = currencies,
                MonthlyBreakdown = monthlyBreakdown
            };
        }
    }
}
