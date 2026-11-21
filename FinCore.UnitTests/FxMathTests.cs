using FinCore.Application.Contracts.Services;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Application.Services;
using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Enums;
using FinCore.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinCore.Tests.Domain;

public class FxMathTests
{
    private const decimal Fee = 0.005m;

    [Fact]
    public void ChargeFor_KnownCase_NeedsOneCentMoreThanTheNaiveInverse()
    {
        // 100.00 USD from GEL at 0.37037037: the naive inverse 271.36 converts to only 99.99.
        var charge = FxMath.ChargeFor(100.00m, 0.37037037m, Fee);

        charge.ChargedAmount.Should().Be(271.37m);
        charge.FeeAmount.Should().Be(1.36m);
        charge.NetAmount.Should().Be(270.01m);
        charge.ConvertedAmount.Should().Be(100.00m);
    }

    public static IEnumerable<object[]> Grid()
    {
        var rates = new[] { 0.37037037m, 2.7m, 1.07407407m, 0.79411765m, 0.93103448m, 3.4m, 0.29411765m, 1.25925926m };
        var amounts = new[] { 0.01m, 0.02m, 0.99m, 1.00m, 9.99m, 10.00m, 33.33m, 100.00m, 123.45m, 999.99m, 1234.56m, 50_000.00m, 99_999.99m };
        foreach (var r in rates)
            foreach (var a in amounts)
                yield return new object[] { a, r };
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void ChargeFor_IsTheSmallestChargeThatCoversTheTarget(decimal target, decimal rate)
    {
        var charge = FxMath.ChargeFor(target, rate, Fee);

        // covers the merchant
        FxMath.Forward(charge.ChargedAmount, rate, Fee).ToAmount.Should().BeGreaterThanOrEqualTo(target);
        // and is minimal: one cent less would under-pay
        if (charge.ChargedAmount > 0.01m)
            FxMath.Forward(charge.ChargedAmount - 0.01m, rate, Fee).ToAmount.Should().BeLessThan(target);

        // consistent and in whole cents
        (charge.NetAmount + charge.FeeAmount).Should().Be(charge.ChargedAmount);
        decimal.Round(charge.ChargedAmount, 2).Should().Be(charge.ChargedAmount);
        charge.FeeAmount.Should().Be(decimal.Round(charge.ChargedAmount * Fee, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void ChargeFor_ZeroFee_ChargesNoFee()
    {
        var charge = FxMath.ChargeFor(10.00m, 2.7m, 0m);

        charge.FeeAmount.Should().Be(0m);
        charge.ChargedAmount.Should().Be(3.71m); // 3.70 * 2.7 = 9.99 < 10
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    public void ChargeFor_InvalidAmount_Throws(string amount)
    {
        var act = () => FxMath.ChargeFor(decimal.Parse(amount), 2.7m, Fee);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("100.00", "0.37037037", "37.04")]   // 37.037037 rounded UP
    [InlineData("100.00", "2.7", "270.00")]
    [InlineData("0.01", "0.37037037", "0.01")]      // 0.0037 rounds up to one cent, never to zero
    public void ConvertRoundedUp_RoundsUpToCents(string amount, string rate, string expected)
    {
        FxMath.ConvertRoundedUp(decimal.Parse(amount), decimal.Parse(rate)).Should().Be(decimal.Parse(expected));
    }

    [Fact]
    public void Forward_MatchesOriginalConversionFormula()
    {
        FxMath.Forward(1000.00m, 0.37037037m, Fee).Should().Be(new FxConversionAmounts(5.00m, 995.00m, 368.51m));
        FxMath.Forward(5.00m, 2.7m, Fee).Should().Be(new FxConversionAmounts(0.03m, 4.97m, 13.41m));
    }
}

public class FxPricingServiceTests
{
    private readonly Mock<IExchangeRateProvider> _rates = new(MockBehavior.Strict);
    private IFxPricingService Create() => new FxPricingService(_rates.Object, Options.Create(new ConversionOptions()));

    [Fact]
    public async Task QuoteCharge_SameCurrency_NoRateLookupNoFee()
    {
        var quote = await Create().QuoteChargeAsync(CurrencyCode.USD, CurrencyCode.USD, 42.50m);

        quote.ChargedAmount.Should().Be(42.50m);
        quote.FeeAmount.Should().Be(0m);
        quote.Rate.Should().Be(1m);
        quote.RateDate.Should().BeNull();
        quote.IsConversion.Should().BeFalse();
    }

    [Fact]
    public async Task QuoteCharge_CrossCurrency_UsesProviderRateAndConfiguredFee()
    {
        var date = new DateTime(2026, 11, 20);
        _rates.Setup(x => x.GetRateAsync(CurrencyCode.GEL, CurrencyCode.USD, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ExchangeRate(CurrencyCode.GEL, CurrencyCode.USD, 0.37037037m, date));

        var quote = await Create().QuoteChargeAsync(CurrencyCode.GEL, CurrencyCode.USD, 100.00m);

        quote.ChargedAmount.Should().Be(271.37m);
        quote.FeeAmount.Should().Be(1.36m);
        quote.FeeRate.Should().Be(0.005m);
        quote.Rate.Should().Be(0.37037037m);
        quote.RateDate.Should().Be(date);
    }

    [Fact]
    public async Task ToLimitAmount_SameCurrency_NoRateLookup()
    {
        var limit = await Create().ToLimitAmountAsync(15m, CurrencyCode.GEL, CurrencyCode.GEL);
        limit.Amount.Should().Be(15m);
        limit.Rate.Should().Be(1m);
    }

    [Fact]
    public async Task ToLimitAmount_CrossCurrency_MidRateRoundedUpWithoutFee()
    {
        _rates.Setup(x => x.GetRateAsync(CurrencyCode.USD, CurrencyCode.GEL, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ExchangeRate(CurrencyCode.USD, CurrencyCode.GEL, 2.7m, DateTime.UtcNow));

        var limit = await Create().ToLimitAmountAsync(33.33m, CurrencyCode.USD, CurrencyCode.GEL);

        limit.Amount.Should().Be(90.00m); // 89.991 rounded UP to the next cent
        limit.Currency.Should().Be(CurrencyCode.GEL);
    }
}
