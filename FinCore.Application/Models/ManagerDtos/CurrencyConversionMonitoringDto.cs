using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ManagerDtos
{
    public class CurrencyConversionMonitoringDto
    {
        public int Id { get; set; }
        public string Reference { get; set; } = null!;

        public string InitiatedByUserId { get; set; } = null!;

        public string FromAccountNumber { get; set; } = null!;
        public string ToAccountNumber { get; set; } = null!;

        public CurrencyCode FromCurrency { get; set; }
        public CurrencyCode ToCurrency { get; set; }

        public decimal FromAmount { get; set; }
        public decimal ToAmount { get; set; }

        public decimal FeeRate { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal NetAmount { get; set; }

        public decimal Rate { get; set; }
        public DateTime RateDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
