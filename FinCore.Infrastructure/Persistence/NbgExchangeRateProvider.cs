using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Domain.Enums;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FinCore.Infrastructure.Persistence
{
    public class NbgExchangeRateProvider : IExchangeRateProvider
    {
        private const string CacheKey = "nbg-rates";
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        public NbgExchangeRateProvider(HttpClient http, IMemoryCache cache)
        {
            _http = http;
            _cache = cache;
        }

        public async Task<ExchangeRate> GetRateAsync(CurrencyCode from,CurrencyCode to,CancellationToken ct = default)
        {
            if (from == to)
                throw new BadRequestException("Currencies must be different");

            var table = await GetTableAsync(ct);

            decimal LariPerUnit(CurrencyCode c) =>
                c == CurrencyCode.GEL ? 1m
                : table.Rates.TryGetValue(c, out var r) ? r
                : throw new BadRequestException($"Rate for {c} is not available");

            var rate = decimal.Round(LariPerUnit(from) / LariPerUnit(to), 8);

            return new ExchangeRate(from,to,rate,table.ValidFrom);
        }

        private async Task<RateTable> GetTableAsync(CancellationToken ct)
        {
            var table = await _cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                return await FetchAsync(ct);
            });

            return table!;
        }

        private async Task<RateTable> FetchAsync(CancellationToken ct)
        {
            try
            {
                var days = await _http.GetFromJsonAsync<List<NbgDay>>(
                    "gw/api/ct/monetarypolicy/currencies/en/json/",ct
                    );

                var day = days.FirstOrDefault() ??
                    throw new ExternalServiceException("Exchange rates are unavailable");

                var rates = new Dictionary<CurrencyCode, decimal>();

                foreach(var c in day.Currencies)
                {
                    if (!Enum.TryParse<CurrencyCode>(c.Code, out var code) || code == CurrencyCode.GEL)
                        continue;

                    var quantity = c.Quantity <= 0 ? 1 : c.Quantity;
                    rates[code] = c.Rate / quantity;
                }

                var validFrom = day.Currencies.FirstOrDefault()?.ValidFromDate ?? day.Date;
                return new RateTable(rates,validFrom);
            }
            catch(Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                throw new ExternalServiceException("Exchange rates are temporarily unavailable.");
            }
        }

        private record RateTable(Dictionary<CurrencyCode, decimal> Rates, DateTime ValidFrom);
        private record NbgDay(DateTime Date, List<NbgCurrency> Currencies);
        private record NbgCurrency(string Code, decimal Rate, int Quantity, DateTime ValidFromDate);

    }
}
