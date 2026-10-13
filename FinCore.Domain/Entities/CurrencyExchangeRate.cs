using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Domain.Entities
{
    public class CurrencyExchangeRate
    {
        public int Id { get; set; }

        public string FromCurrency { get; set; }

        public string ToCurrency { get; set; }

        public decimal Rate { get; set; }

        public DateTime EffectiveAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
