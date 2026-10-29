using FinCore.Application.Models.ExchangeDtos;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IExchangeRateProvider
    {
        Task<ExchangeRate> GetRateAsync(CurrencyCode from,CurrencyCode to,CancellationToken ct =default);
    }
}
