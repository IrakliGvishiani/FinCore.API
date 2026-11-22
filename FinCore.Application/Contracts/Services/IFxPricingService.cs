using FinCore.Application.Models.ExchangeDtos;
using FinCore.Domain.Enums;

namespace FinCore.Application.Contracts.Services
{
    /// <summary>
    /// Single entry point for exchange rates and FX fees, shared by currency conversions and card payments.
    /// Rates come from <see cref="IExchangeRateProvider"/> (NBG, cached); the fee rate from <see cref="ConversionOptions"/>.
    /// Throws <c>ExternalServiceException</c> (503) when rates are unavailable.
    /// </summary>
    public interface IFxPricingService
    {
        /// <summary>The configured FX fee rate (0.5% by default).</summary>
        decimal FeeRate { get; }

        /// <summary>Raw provider rate. Same-currency requests are rejected, as for conversions.</summary>
        Task<ExchangeRate> GetRateAsync(CurrencyCode from, CurrencyCode to, CancellationToken ct = default);

        /// <summary>
        /// Prices a payment of <paramref name="targetAmount"/> in <paramref name="target"/> currency, funded from
        /// <paramref name="source"/> currency. Same currency: charged = target amount, rate 1, fee 0, no rate lookup.
        /// </summary>
        Task<FxChargeQuote> QuoteChargeAsync(CurrencyCode source, CurrencyCode target, decimal targetAmount, CancellationToken ct = default);

        /// <summary>
        /// Converts <paramref name="amount"/> to <paramref name="limitCurrency"/> at the mid rate (no fee), rounded up.
        /// Same currency: returned unchanged, no rate lookup.
        /// </summary>
        Task<FxLimitAmount> ToLimitAmountAsync(decimal amount, CurrencyCode from, CurrencyCode limitCurrency, CancellationToken ct = default);
    }
}
