using FinCore.Application.Exceptions;
using FinCore.Domain.Enums;
using FinCore.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text;

namespace FinCore.Tests.Infrastructure;

/// <summary>Pins the NBG rate source that both conversions and card payments rely on.</summary>
public class NbgExchangeRateProviderTests
{
    private const string Json = """
        [{
          "date": "2026-11-20T00:00:00",
          "currencies": [
            { "code": "USD", "rate": 2.7,    "quantity": 1,   "validFromDate": "2026-11-20T00:00:00" },
            { "code": "EUR", "rate": 2.9,    "quantity": 1,   "validFromDate": "2026-11-20T00:00:00" },
            { "code": "GBP", "rate": 3.4,    "quantity": 1,   "validFromDate": "2026-11-20T00:00:00" },
            { "code": "JPY", "rate": 1.75,   "quantity": 100, "validFromDate": "2026-11-20T00:00:00" }
          ]
        }]
        """;

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(respond());
        }
    }

    private static (NbgExchangeRateProvider Provider, StubHandler Handler) Create(Func<HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://nbg.test/") };
        return (new NbgExchangeRateProvider(http, new MemoryCache(new MemoryCacheOptions())), handler);
    }

    private static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK) { Content = new StringContent(Json, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData(CurrencyCode.USD, CurrencyCode.GEL, "2.7")]
    [InlineData(CurrencyCode.GEL, CurrencyCode.USD, "0.37037037")]
    [InlineData(CurrencyCode.EUR, CurrencyCode.USD, "1.07407407")]
    [InlineData(CurrencyCode.USD, CurrencyCode.GBP, "0.79411765")]
    public async Task GetRate_ComputesCrossRatesViaGel_RoundedTo8Decimals(CurrencyCode from, CurrencyCode to, string expected)
    {
        var (provider, _) = Create(Ok);

        var rate = await provider.GetRateAsync(from, to);

        rate.Rate.Should().Be(decimal.Parse(expected));
        rate.RateDate.Should().Be(new DateTime(2026, 11, 20));
    }

    [Fact]
    public async Task GetRate_UsesCachedTable()
    {
        var (provider, handler) = Create(Ok);

        await provider.GetRateAsync(CurrencyCode.USD, CurrencyCode.GEL);
        await provider.GetRateAsync(CurrencyCode.EUR, CurrencyCode.GBP);

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task GetRate_SameCurrency_ThrowsBadRequest()
    {
        var (provider, _) = Create(Ok);

        var act = () => provider.GetRateAsync(CurrencyCode.GEL, CurrencyCode.GEL);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task GetRate_NbgUnavailable_ThrowsExternalServiceException()
    {
        var (provider, _) = Create(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var act = () => provider.GetRateAsync(CurrencyCode.USD, CurrencyCode.GEL);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }
}
