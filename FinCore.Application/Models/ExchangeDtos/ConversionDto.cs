using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ExchangeDtos
{
    public class ConversionDto
    {
        public int Id { get; set; }

        public string FromAccountNumber { get; set; } = null!;
        public string ToAccountNumber { get; set; } = null!;

        public decimal FromAmount { get; set; }
        public CurrencyCode FromCurrency { get; set; }

        public decimal ToAmount { get; set; }
        public CurrencyCode ToCurrency { get; set; }

        public decimal Rate { get; set; }
        public decimal FeeRate { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal NetAmount { get; set; }

        public DateTime RateDate { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class ConversionQuoteDto
    {
        public string FromAccountNumber { get; set; } = null!;
        public string ToAccountNumber { get; set; } = null!;

        public decimal FromAmount { get; set; }
        public CurrencyCode FromCurrency { get; set; }

        public decimal ToAmount { get; set; }
        public CurrencyCode ToCurrency { get; set; }

        public decimal Rate { get; set; }

        public decimal FeeRate { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal NetAmount { get; set; }
        public DateTime RateDate { get; set; }
    }
}
