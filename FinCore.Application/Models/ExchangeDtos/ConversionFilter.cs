using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ExchangeDtos
{
    public class ConversionFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public CurrencyCode? FromCurrency { get; set; }
        public CurrencyCode? ToCurrency { get; set; }
    }
}
