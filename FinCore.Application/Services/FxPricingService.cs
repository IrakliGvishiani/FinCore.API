using FinCore.Application.Contracts.Services;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Domain.Enums;
using FinCore.Domain.Services;
using Microsoft.Extensions.Options;

namespace FinCore.Application.Services
{
    public class FxPricingService : IFxPricingService
    {
        private readonly IExchangeRateProvider _rates;
        private readonly IOptions<ConversionOptions> _options;

        public FxPricingService(IExchangeRateProvider rates, IOptions<ConversionOptions> options)
        {
            _rates = rates;
            _options = options;
        }

        public decimal FeeRate => _options.Value.FeeRate;

        public Task<ExchangeRate> GetRateAsync(CurrencyCode from, CurrencyCode to, CancellationToken ct = default)
            => _rates.GetRateAsync(from, to, ct);

        public async Task<FxChargeQuote> QuoteChargeAsync(
            CurrencyCode source, CurrencyCode target, decimal targetAmount, CancellationToken ct = default)
        {
            if (source == target)
                return new FxChargeQuote(source, target, targetAmount, targetAmount, 0m, targetAmount, 1m, 0m, null);

            var rate = await _rates.GetRateAsync(source, target, ct);
            var charge = FxMath.ChargeFor(targetAmount, rate.Rate, FeeRate);

            return new FxChargeQuote(
                source, target, targetAmount,
                charge.ChargedAmount, charge.FeeAmount, charge.NetAmount,
                rate.Rate, FeeRate, rate.RateDate);
        }

        public async Task<FxLimitAmount> ToLimitAmountAsync(
            decimal amount, CurrencyCode from, CurrencyCode limitCurrency, CancellationToken ct = default)
        {
            if (from == limitCurrency)
                return new FxLimitAmount(amount, limitCurrency, 1m, null);

            var rate = await _rates.GetRateAsync(from, limitCurrency, ct);
            return new FxLimitAmount(FxMath.ConvertRoundedUp(amount, rate.Rate), limitCurrency, rate.Rate, rate.RateDate);
        }
    }
}
