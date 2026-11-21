using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Application.Services;
using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace FinCore.Tests.Services;

/// <summary>
/// Pins the behaviour of currency conversions (amounts, rounding, ledger legs, idempotency,
/// rate protection) so that moving the FX math into a shared service cannot change results.
/// Expected values were computed by hand from the original formulas:
///   fee = round(from * 0.005, AwayFromZero); net = from - fee; to = round(net * rate, ToZero).
/// </summary>
public class ConversionServiceCharacterizationTests
{
    private const string UserId = "user-1";
    private const decimal GelToUsd = 0.37037037m;
    private const decimal UsdToGel = 2.7m;
    private static readonly DateTime RateDate = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IExchangeRateProvider> _rates = new();
    private readonly InMemoryRepository<CurrencyConversion> _conversions = new();
    private readonly InMemoryRepository<BankAccount> _accounts = new();
    private readonly InMemoryRepository<LedgerTransaction> _ledgerTransactions = new();
    private readonly InMemoryRepository<LedgerAccount> _ledgerAccounts = new();
    private readonly RecordingUnitOfWork _uow = new();
    private readonly Mock<IAuditService> _audit = new();

    private readonly BankAccount _gel;
    private readonly BankAccount _usd;

    public ConversionServiceCharacterizationTests()
    {
        _gel = BankAccount.Create("GE00GEL", UserId, CurrencyCode.GEL);
        _usd = BankAccount.Create("GE00USD", UserId, CurrencyCode.USD);
        _accounts.Seed(_gel, 1).Seed(_usd, 2);
        _gel.Credit(10_000m);
        _usd.Credit(10_000m);

        var id = 10;
        foreach (var c in Enum.GetValues<CurrencyCode>())
        {
            _ledgerAccounts.Seed(LedgerAccount.Create(LedgerAccountCodes.Fx(c), $"FX {c}", c), id++);
            _ledgerAccounts.Seed(LedgerAccount.Create(LedgerAccountCodes.Fee(c), $"Fee {c}", c), id++);
        }

        _rates.Setup(x => x.GetRateAsync(CurrencyCode.GEL, CurrencyCode.USD, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ExchangeRate(CurrencyCode.GEL, CurrencyCode.USD, GelToUsd, RateDate));
        _rates.Setup(x => x.GetRateAsync(CurrencyCode.USD, CurrencyCode.GEL, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ExchangeRate(CurrencyCode.USD, CurrencyCode.GEL, UsdToGel, RateDate));
    }

    private IConversionService CreateService() => new ConversionService(
        new FxPricingService(_rates.Object, Options.Create(new ConversionOptions())),
        _uow, _conversions, _accounts, _ledgerTransactions, _ledgerAccounts, _audit.Object);

    [Theory]
    // from, to, amount, expected fee, expected net, expected toAmount
    [InlineData("GE00GEL", "GE00USD", "1000.00", "5.00", "995.00", "368.51")]  // 368.5185... truncated, not rounded
    [InlineData("GE00USD", "GE00GEL", "123.45", "0.62", "122.83", "331.64")]   // fee 0.61725 -> 0.62
    [InlineData("GE00USD", "GE00GEL", "1.00", "0.01", "0.99", "2.67")]         // fee 0.005 -> 0.01 (away from zero)
    [InlineData("GE00USD", "GE00GEL", "5.00", "0.03", "4.97", "13.41")]        // fee 0.025 -> 0.03 (banker's would give 0.02)
    [InlineData("GE00GEL", "GE00USD", "0.10", "0.00", "0.10", "0.03")]         // fee rounds to zero
    public async Task Quote_ReturnsUnchangedAmounts(string from, string to, string amount, string fee, string net, string toAmount)
    {
        var quote = await CreateService().QuoteAsync(UserId, from, to, decimal.Parse(amount));

        quote.FromAmount.Should().Be(decimal.Parse(amount));
        quote.FeeRate.Should().Be(0.005m);
        quote.FeeAmount.Should().Be(decimal.Parse(fee));
        quote.NetAmount.Should().Be(decimal.Parse(net));
        quote.ToAmount.Should().Be(decimal.Parse(toAmount));
        quote.RateDate.Should().Be(RateDate);
    }

    [Fact]
    public async Task Quote_TooSmallAmount_ThrowsDomainException()
    {
        var act = () => CreateService().QuoteAsync(UserId, "GE00GEL", "GE00USD", 0.01m);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Amount is too small to convert.");
    }

    [Fact]
    public async Task Convert_MovesMoneyAndWritesBalancedLegs()
    {
        var result = await CreateService().ConvertAsync(UserId, "GE00GEL", "GE00USD", 1000m, "key-1");

        result.ToAmount.Should().Be(368.51m);
        _gel.Balance.Should().Be(9_000m);
        _usd.Balance.Should().Be(10_368.51m);

        var txs = _ledgerTransactions.Added;
        txs.Should().HaveCount(2);
        txs.Should().OnlyContain(t => t.TransactionType == LedgerTransactionType.CurrencyConversion);

        var outTx = txs.Single(t => t.Reference.EndsWith("-OUT"));
        outTx.Currency.Should().Be(CurrencyCode.GEL);
        outTx.Entries.Select(e => (e.BankAccountId, e.LedgerAccountId, e.Type, e.Amount)).Should().BeEquivalentTo(new[]
        {
            ((int?)1, (int?)null, EntryType.Debit, 1000.00m),
            ((int?)null, (int?)Fx(CurrencyCode.GEL), EntryType.Credit, 995.00m),
            ((int?)null, (int?)Fee(CurrencyCode.GEL), EntryType.Credit, 5.00m),
        });

        var inTx = txs.Single(t => t.Reference.EndsWith("-IN"));
        inTx.Currency.Should().Be(CurrencyCode.USD);
        inTx.Entries.Select(e => (e.BankAccountId, e.LedgerAccountId, e.Type, e.Amount)).Should().BeEquivalentTo(new[]
        {
            ((int?)null, (int?)Fx(CurrencyCode.USD), EntryType.Debit, 368.51m),
            ((int?)2, (int?)null, EntryType.Credit, 368.51m),
        });

        _uow.Commits.Should().Be(1);
    }

    [Fact]
    public async Task Convert_WhenFeeRoundsToZero_OmitsFeeLine()
    {
        await CreateService().ConvertAsync(UserId, "GE00GEL", "GE00USD", 0.10m, "key-small");

        var outTx = _ledgerTransactions.Added.Single(t => t.Reference.EndsWith("-OUT"));
        outTx.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Convert_ExpectedRateDiffers_ThrowsConflictAndStoresNothing()
    {
        var act = () => CreateService().ConvertAsync(UserId, "GE00GEL", "GE00USD", 100m, "key-2", expectedRate: 0.4m);

        await act.Should().ThrowAsync<ConflictException>();
        _conversions.Added.Should().BeEmpty();
        _gel.Balance.Should().Be(10_000m);
    }

    [Fact]
    public async Task Convert_SameKeySameRequest_ReturnsStoredResult()
    {
        var service = CreateService();
        var first = await service.ConvertAsync(UserId, "GE00GEL", "GE00USD", 100m, "key-3");
        var second = await service.ConvertAsync(UserId, "GE00GEL", "GE00USD", 100m, "key-3");

        second.Id.Should().Be(first.Id);
        _conversions.Added.Should().HaveCount(1);
        _gel.Balance.Should().Be(9_900m);
    }

    [Fact]
    public async Task Convert_SameKeyDifferentAmount_ThrowsConflict()
    {
        var service = CreateService();
        await service.ConvertAsync(UserId, "GE00GEL", "GE00USD", 100m, "key-4");

        var act = () => service.ConvertAsync(UserId, "GE00GEL", "GE00USD", 101m, "key-4");

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Convert_RateProviderUnavailable_Propagates503Exception()
    {
        _rates.Setup(x => x.GetRateAsync(CurrencyCode.GEL, CurrencyCode.USD, It.IsAny<CancellationToken>()))
              .ThrowsAsync(new ExternalServiceException("Exchange rates are temporarily unavailable."));

        var act = () => CreateService().ConvertAsync(UserId, "GE00GEL", "GE00USD", 100m, "key-5");

        await act.Should().ThrowAsync<ExternalServiceException>();
        _conversions.Added.Should().BeEmpty();
    }

    private int Fx(CurrencyCode c) => _ledgerAccounts.Items.Single(a => a.AccountCode == LedgerAccountCodes.Fx(c)).Id;
    private int Fee(CurrencyCode c) => _ledgerAccounts.Items.Single(a => a.AccountCode == LedgerAccountCodes.Fee(c)).Id;
}
