using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.Analytics
{
    public class CurrencyAnalyticsDto
    {
        public CurrencyCode Currency { get; set; }

        public decimal Balance { get; set; }

        public decimal Income { get; set; }

        public decimal Expenses { get; set; }

        public decimal NetCashFlow => Income - Expenses;
    }
}
