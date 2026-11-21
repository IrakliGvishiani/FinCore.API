using FinCore.Domain.DomainExceptions;

namespace FinCore.Domain.Services
{
    /// <summary>Result of converting a source amount (the currency-conversion direction).</summary>
    public readonly record struct FxConversionAmounts(decimal FeeAmount, decimal NetAmount, decimal ToAmount);

    /// <summary>
    /// Result of pricing a target (merchant) amount: what the customer is charged in the source currency.
    /// <see cref="ChargedAmount"/> = <see cref="NetAmount"/> + <see cref="FeeAmount"/>, and
    /// <see cref="ConvertedAmount"/> (what the net amount converts to) is always &gt;= the target amount.
    /// </summary>
    public readonly record struct FxChargeAmounts(decimal ChargedAmount, decimal FeeAmount, decimal NetAmount, decimal ConvertedAmount);

    /// <summary>
    /// The single home of FX money math (fee, rounding), shared by currency conversions and card payments.
    /// All amounts are decimal with 2 decimals; rates are "target units per 1 source unit".
    /// </summary>
    public static class FxMath
    {
        private const decimal Cent = 0.01m;
        private const int MaxSearchSteps = 1_000;

        /// <summary>
        /// Converts <paramref name="fromAmount"/>: the fee is taken from the source amount
        /// (rounded half away from zero) and the converted amount is truncated, so the bank never over-pays.
        /// This is the original currency-conversion formula, unchanged.
        /// </summary>
        public static FxConversionAmounts Forward(decimal fromAmount, decimal rate, decimal feeRate)
        {
            EnsureAmount(fromAmount);
            EnsureRate(rate);
            EnsureFeeRate(feeRate);

            var feeAmount = decimal.Round(fromAmount * feeRate, 2, MidpointRounding.AwayFromZero);
            var netAmount = fromAmount - feeAmount;
            var toAmount = decimal.Round(netAmount * rate, 2, MidpointRounding.ToZero);

            return new FxConversionAmounts(feeAmount, netAmount, toAmount);
        }

        /// <summary>
        /// Returns the smallest charge (in cents) whose <see cref="Forward"/> conversion covers
        /// <paramref name="targetAmount"/>, so the merchant is never under-paid and the customer
        /// is charged under exactly the same fee and rounding rules as a currency conversion.
        /// </summary>
        public static FxChargeAmounts ChargeFor(decimal targetAmount, decimal rate, decimal feeRate)
        {
            EnsureAmount(targetAmount);
            EnsureRate(rate);
            EnsureFeeRate(feeRate);

            // Exact inverse, rounded up. Forward() is monotonic non-decreasing in the charge
            // (the fee grows by at most one cent per cent), so a short local search finds the minimum.
            var charge = Math.Max(Cent, CeilingToCents(targetAmount / (rate * (1m - feeRate))));

            var steps = 0;
            while (charge > Cent && Forward(charge - Cent, rate, feeRate).ToAmount >= targetAmount)
            {
                charge -= Cent;
                if (++steps > MaxSearchSteps) throw new DomainException("Unable to price the amount.");
            }

            while (Forward(charge, rate, feeRate).ToAmount < targetAmount)
            {
                charge += Cent;
                if (++steps > MaxSearchSteps) throw new DomainException("Unable to price the amount.");
            }

            var result = Forward(charge, rate, feeRate);
            return new FxChargeAmounts(charge, result.FeeAmount, result.NetAmount, result.ToAmount);
        }

        /// <summary>Converts at the mid rate (no fee), rounded up to whole cents. Used for card-limit amounts.</summary>
        public static decimal ConvertRoundedUp(decimal amount, decimal rate)
        {
            EnsureAmount(amount);
            EnsureRate(rate);
            return CeilingToCents(amount * rate);
        }

        /// <summary>Rounds up (towards +infinity) to 2 decimals.</summary>
        public static decimal CeilingToCents(decimal value)
            => decimal.Round(value, 2, MidpointRounding.ToPositiveInfinity);

        private static void EnsureAmount(decimal amount)
        {
            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new DomainException("Amount must be positive with at most 2 decimals.");
        }

        private static void EnsureRate(decimal rate)
        {
            if (rate <= 0)
                throw new DomainException("Invalid exchange rate.");
        }

        private static void EnsureFeeRate(decimal feeRate)
        {
            if (feeRate < 0 || feeRate >= 1)
                throw new DomainException("Invalid fee rate.");
        }
    }
}
