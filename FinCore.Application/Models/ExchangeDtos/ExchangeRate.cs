using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.ExchangeDtos
{
    public record ExchangeRate(CurrencyCode From, CurrencyCode To, decimal Rate, DateTime RateDate);
}
