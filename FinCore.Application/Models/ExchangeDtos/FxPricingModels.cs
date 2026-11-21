using FinCore.Domain.Enums;

namespace FinCore.Application.Models.ExchangeDtos
{
    /// <summary>
    /// What a payment of <see cref="TargetAmount"/> <see cref="TargetCurrency"/> costs in <see cref="SourceCurrency"/>.
    /// <see cref="Rate"/> is target units per 1 source unit (1 when no conversion happens).
    /// </summary>
    public sealed record FxChargeQuote(
        CurrencyCode SourceCurrency,
        CurrencyCode TargetCurrency,
        decimal TargetAmount,
        decimal ChargedAmount,
        decimal FeeAmount,
        decimal NetAmount,
        decimal Rate,
        decimal FeeRate,
        DateTime? RateDate)
    {
        public bool IsConversion => SourceCurrency != TargetCurrency;
    }

    /// <summary>An amount expressed in a card's limit currency. <see cref="Rate"/> is limit units per 1 original unit.</summary>
    public sealed record FxLimitAmount(decimal Amount, CurrencyCode Currency, decimal Rate, DateTime? RateDate);
}
