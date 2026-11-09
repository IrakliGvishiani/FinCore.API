using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.Analytics
{
    public class AnalyticsOverviewDto
    {
        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public List<CurrencyAnalyticsDto> Currencies { get; set; } = new();

        public List<MonthlyAnalyticsDto> MonthlyBreakdown { get; set; } = new();
    }
}
