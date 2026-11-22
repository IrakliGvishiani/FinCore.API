using FinCore.Application.Exceptions;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Infrastructure;
using FinCore.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Tests.Payments;

public class PaymentQuoteTests
{
    private static PaymentQuoteRequestDto Request(decimal amount, CurrencyCode currency) =>
        new() { VirtualCardId = 7, Amount = amount, Currency = currency };

    /// <summary>Requirement 5: unit of work that throws if touched, repositories that throw on any write.</summary>
    private static PaymentHarness ReadOnly(PaymentHarness h)
    {
        h.Payments.ThrowOnWrite = h.Cards.ThrowOnWrite = h.Accounts.ThrowOnWrite =
            h.LedgerAccounts.ThrowOnWrite = h.LedgerTransactions.ThrowOnWrite = true;
        return h;
    }

    [Fact]
    public async Task Quote_NeverWrites_NeverOpensATransaction_NeverChangesState()
    {
        var h = ReadOnly(new PaymentHarness(allowAutoConversion: true));
        var balances = h.Accounts.Items.ToDictionary(a => a.Id, a => a.Balance);

        var quote = await h.Service(new ThrowingUnitOfWork()).QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD));

        quote.Accounts.Should().HaveCount(5);
        h.Accounts.Items.Should().OnlyContain(a => a.Balance == balances[a.Id]);
        h.Card.LastAuthorizedAt.Should().BeNull();
        h.Audit.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ThrowingUnitOfWork_ReallyThrows_SoTheTestAboveMeansSomething()
    {
        var h = new PaymentHarness();
        await h.Service(new ThrowingUnitOfWork()).Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(1m)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Quote_FlagOn_ListsAllActiveOwnAccounts_MainFirstThenCurrencyCodeThenId()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.AddAccount(6, CurrencyCode.USD, 0m);
        var frozen = h.AddAccount(8, CurrencyCode.EUR, 5_000m);
        frozen.Freeze();

        var quote = await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD));

        quote.Accounts.Select(a => a.AccountId).Should().Equal(1, 3, 4, 5, 2, 6); // GEL main, EUR, GBP, GEL(5), USD(2), USD(6)
        quote.Accounts.Should().NotContain(a => a.AccountId == frozen.Id || a.AccountId == h.StrangerUsd.Id);
    }

    [Fact]
    public async Task Quote_AmountsPerAccount_AreWhatPayWouldCharge()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        var service = h.Service();

        var quote = await service.QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD));

        var gel = quote.Accounts.Single(a => a.AccountId == h.Gel.Id);
        gel.Should().BeEquivalentTo(new
        {
            Currency = CurrencyCode.GEL, IsMainAccount = true, ChargedAmount = 271.37m, FeeAmount = 1.36m,
            ExchangeRate = 0.37037037m, FeeRate = 0.005m, IsConversion = true, HasSufficientBalance = true
        });
        var usd = quote.Accounts.Single(a => a.AccountId == h.Usd.Id);
        usd.Should().BeEquivalentTo(new { ChargedAmount = 100m, FeeAmount = 0m, ExchangeRate = 1m, IsConversion = false, RateDate = (DateTime?)null });
        quote.LimitAmount.Should().Be(270m);
        quote.LimitCurrency.Should().Be(CurrencyCode.GEL);

        // every quoted amount is accepted by PayAsync as expectedChargedAmount
        var i = 0;
        foreach (var account in quote.Accounts)
        {
            var dto = await service.PayAsync(PaymentHarness.Owner,
                PaymentHarness.NewShape(100m, CurrencyCode.USD, account.AccountId, expected: account.ChargedAmount, key: $"q{i++}"));
            dto.ChargedAmount.Should().Be(account.ChargedAmount);
        }
    }

    [Fact]
    public async Task Recommend_Rule1_SameCurrencyWithEnoughBalance_MainFirstThenLowestId()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.AddAccount(6, CurrencyCode.USD, 10_000m);

        (await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD)))
            .RecommendedAccountId.Should().Be(h.Usd.Id); // 2 before 6

        // paying GEL: the main GEL account beats GEL(5) even though both have enough
        (await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.GEL)))
            .RecommendedAccountId.Should().Be(h.Gel.Id);
    }

    [Fact]
    public async Task Recommend_Rule1_SkipsSameCurrencyWithoutEnough()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Usd.Debit(h.Usd.Balance - 50m);
        var usd6 = h.AddAccount(6, CurrencyCode.USD, 500m);

        (await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD)))
            .RecommendedAccountId.Should().Be(usd6.Id);
    }

    [Fact]
    public async Task Recommend_Rule2_MainAccountWhenNoSameCurrencyAccountHasEnough()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Usd.Debit(h.Usd.Balance - 50m);

        var quote = await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD));

        quote.Accounts.Single(a => a.AccountId == h.Usd.Id).HasSufficientBalance.Should().BeFalse();
        quote.RecommendedAccountId.Should().Be(h.Gel.Id);
    }

    [Fact]
    public async Task Recommend_Rule3_OtherAccountsByCurrencyCodeThenId()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Usd.Debit(h.Usd.Balance - 50m);
        h.Gel.Debit(h.Gel.Balance - 50m);
        // left with enough: EUR(3), GBP(4), GEL(5) -> currency code order EUR < GBP < GEL
        (await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD)))
            .RecommendedAccountId.Should().Be(h.Eur.Id);

        h.Eur.Debit(h.Eur.Balance - 1m);
        (await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD)))
            .RecommendedAccountId.Should().Be(h.Gbp.Id);
    }

    [Fact]
    public async Task Recommend_None_WhenNoAccountHasEnough()
    {
        var h = new PaymentHarness(allowAutoConversion: true, balance: 1m);

        var quote = await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD));

        quote.Accounts.Should().NotBeEmpty().And.OnlyContain(a => !a.HasSufficientBalance);
        quote.RecommendedAccountId.Should().BeNull();
        quote.DeclineReason.Should().BeNull();
    }

    [Fact]
    public async Task Quote_FlagOff_OnlyMainAccount()
    {
        var h = new PaymentHarness();

        var quote = await h.Service().QuoteAsync(PaymentHarness.Owner, Request(10m, CurrencyCode.GEL));

        quote.Accounts.Should().ContainSingle().Which.AccountId.Should().Be(h.Gel.Id);
        quote.RecommendedAccountId.Should().Be(h.Gel.Id);
        h.Rates.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Quote_FlagOff_ForeignCurrency_NoAccounts_ReasonAutoConversionNotAllowed_NoRates()
    {
        var h = new PaymentHarness();

        var quote = await h.Service().QuoteAsync(PaymentHarness.Owner, Request(10m, CurrencyCode.USD));

        quote.Accounts.Should().BeEmpty();
        quote.RecommendedAccountId.Should().BeNull();
        quote.DeclineReason.Should().Be(PaymentDeclineReason.AutoConversionNotAllowed);
        quote.LimitAmount.Should().BeNull();
        h.Rates.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Quote_FrozenCard_ReasonCardNotActive()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Card.Freeze();

        var quote = await h.Service().QuoteAsync(PaymentHarness.Owner, Request(10m, CurrencyCode.USD));

        quote.Accounts.Should().BeEmpty();
        quote.DeclineReason.Should().Be(PaymentDeclineReason.CardNotActive);
    }

    [Fact]
    public async Task Quote_ReportsLimits()
    {
        var h = new PaymentHarness(allowAutoConversion: true, transactionLimit: 250m, dailyLimit: 300m);
        await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(40m));

        var quote = await h.Service().QuoteAsync(PaymentHarness.Owner, Request(100m, CurrencyCode.USD));

        quote.SpentToday.Should().Be(40m);
        quote.LimitAmount.Should().Be(270m);
        quote.WithinTransactionLimit.Should().BeFalse();
        quote.WithinDailyLimit.Should().BeFalse();
    }

    [Fact]
    public async Task Quote_NbgUnavailable_503()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Rates.Unavailable = true;

        await h.Service().Invoking(s => s.QuoteAsync(PaymentHarness.Owner, Request(10m, CurrencyCode.USD)))
            .Should().ThrowAsync<ExternalServiceException>();
    }

    [Theory]
    [InlineData(0, CurrencyCode.USD)]
    [InlineData(1.001, CurrencyCode.USD)]
    [InlineData(10, null)]
    [InlineData(10, (CurrencyCode)42)]
    public async Task Quote_InvalidRequest_400(double amount, CurrencyCode? currency)
    {
        var h = new PaymentHarness();
        await h.Service().Invoking(s => s.QuoteAsync(PaymentHarness.Owner,
                new PaymentQuoteRequestDto { VirtualCardId = 7, Amount = (decimal)amount, Currency = currency }))
            .Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Quote_SomeoneElsesCard_404()
    {
        var h = new PaymentHarness();
        await h.Service().Invoking(s => s.QuoteAsync(PaymentHarness.Stranger, Request(10m, CurrencyCode.GEL)))
            .Should().ThrowAsync<NotFoundException>();
    }
}

/// <summary>Checks the EF model (no database connection is opened).</summary>
public class PaymentModelConfigurationTests
{
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=model-only;Database=none;Connect Timeout=1").Options);

    [Fact]
    public void VirtualCard_RowVersion_IsAConcurrencyToken()
    {
        using var ctx = Context();
        var p = ctx.Model.FindEntityType(typeof(VirtualCard))!.FindProperty(nameof(VirtualCard.RowVersion))!;
        p.IsConcurrencyToken.Should().BeTrue();
        p.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void Payment_IdempotencyKey_IsUniquePerCard()
    {
        using var ctx = Context();
        ctx.Model.FindEntityType(typeof(SimulatedPayment))!.GetIndexes()
            .Should().Contain(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "VirtualCardId", "IdempotencyKey" }));
    }

    [Fact]
    public void Payment_PricingColumns_AreNullable_SourceAndChargedCurrency_AreRequired()
    {
        using var ctx = Context();
        var t = ctx.Model.FindEntityType(typeof(SimulatedPayment))!;
        foreach (var n in new[] { "ChargedAmount", "ExchangeRate", "FeeAmount", "LimitAmount", "RateDate" })
            t.FindProperty(n)!.IsNullable.Should().BeTrue(n);
        t.FindProperty("SourceBankAccountId")!.IsNullable.Should().BeFalse();
        t.FindProperty("ChargedCurrency")!.IsNullable.Should().BeFalse();
    }
}
