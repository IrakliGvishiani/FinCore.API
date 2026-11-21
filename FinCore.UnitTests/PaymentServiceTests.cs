using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Domain.Services;
using FinCore.Tests.Domain;
using FinCore.Tests.Support;
using FluentAssertions;

namespace FinCore.Tests.Payments;

/// <summary>Requirement 3: an old-shape request on a card with auto-conversion off behaves as before.</summary>
public class OldShapeCompatibilityTests
{
    [Fact]
    public async Task OldShape_Completes_ChargedFromMainAccountInItsCurrency_WithoutRates()
    {
        var h = new PaymentHarness();

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        // fields old clients read: unchanged meaning and values
        dto.Status.Should().Be(SimulatedPaymentStatus.Completed);
        dto.FailureReason.Should().BeNull();
        dto.Amount.Should().Be(25m);
        dto.Currency.Should().Be(CurrencyCode.GEL);
        dto.Merchant.Should().Be("Demo Shop");
        dto.Description.Should().Be("Order 1");
        dto.Last4Digits.Should().Be("4242");
        dto.VirtualCardId.Should().Be(7);
        dto.Reference.Should().StartWith("PAY-");

        // new fields: identity pricing
        dto.SourceBankAccountId.Should().Be(h.Gel.Id);
        dto.ChargedAmount.Should().Be(25m);
        dto.ChargedCurrency.Should().Be(CurrencyCode.GEL);
        dto.ExchangeRate.Should().Be(1m);
        dto.FeeAmount.Should().Be(0m);
        dto.LimitAmount.Should().Be(25m);
        dto.RateDate.Should().BeNull();
        dto.IsConversion.Should().BeFalse();

        h.Gel.Balance.Should().Be(9_975m);
        h.Rates.Calls.Should().Be(0);

        // ledger exactly as before: one CardPayment transaction, reference = payment reference
        var tx = h.LedgerTransactions.Items.Should().ContainSingle().Subject;
        tx.Reference.Should().Be(dto.Reference);
        tx.Currency.Should().Be(CurrencyCode.GEL);
        tx.TransactionType.Should().Be(LedgerTransactionType.CardPayment);
        tx.Entries.Select(e => (e.BankAccountId, e.LedgerAccountId, e.Type, e.Amount)).Should().BeEquivalentTo(new[]
        {
            ((int?)h.Gel.Id, (int?)null, EntryType.Debit, 25m),
            ((int?)null, (int?)h.LedgerId(LedgerAccountCodes.CardSettlement(CurrencyCode.GEL)), EntryType.Credit, 25m)
        });

        h.Uow.Commits.Should().Be(1);
    }

    [Fact]
    public async Task OldShape_SameKeySameRequest_ReplaysStoredResult_WithoutSecondDebit()
    {
        var h = new PaymentHarness();
        var service = h.Service();

        var first = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));
        var second = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        second.Should().BeEquivalentTo(first);
        h.Payments.Items.Should().ContainSingle();
        h.LedgerTransactions.Items.Should().ContainSingle();
        h.Gel.Balance.Should().Be(9_975m);
    }

    [Fact]
    public async Task OldShape_ReplayIsWhitespaceInsensitive_AsBefore()
    {
        var h = new PaymentHarness();
        var service = h.Service();
        var first = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m, key: "key-1", merchant: "Demo Shop"));

        var second = await service.PayAsync(PaymentHarness.Owner,
            PaymentHarness.OldShape(25m, key: "  key-1 ", merchant: " Demo Shop ", description: " Order 1 "));

        second.Id.Should().Be(first.Id);
    }

    [Theory]
    [InlineData("26")]           // amount
    [InlineData("merchant")]     // merchant
    [InlineData("description")]  // description
    public async Task OldShape_SameKeyDifferentRequest_Returns409WithSameMessage(string change)
    {
        var h = new PaymentHarness();
        var service = h.Service();
        await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        var changed = change switch
        {
            "26" => PaymentHarness.OldShape(26m),
            "merchant" => PaymentHarness.OldShape(25m, merchant: "Other Shop"),
            _ => PaymentHarness.OldShape(25m, description: "Order 2")
        };

        var act = () => service.PayAsync(PaymentHarness.Owner, changed);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Idempotency Key already used for a differenc payment");
    }

    [Fact]
    public async Task OldShape_ReplayOfLegacyBackfilledRow_ReturnsIt()
    {
        // A payment as it exists after the migration backfill: source = card account, charged = amount,
        // rate 1, fee 0, limit = amount, RateDate null.
        var h = new PaymentHarness();
        var legacy = SimulatedPayment.Create(h.Card, h.Gel, CurrencyCode.GEL, 40m, "Demo Shop", "Order 1", "legacy-key", DateTime.UtcNow.AddDays(-3));
        legacy.ApplyPricing(40m, 1m, 0m, 40m, null);
        legacy.Fail(PaymentDeclineReason.InsufficientFunds);
        h.Payments.SeedCommitted(legacy);

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(40m, key: "legacy-key"));

        dto.Id.Should().Be(legacy.Id);
        dto.Status.Should().Be(SimulatedPaymentStatus.Failed);
        dto.FailureReason.Should().Be(PaymentDeclineReason.InsufficientFunds);
        h.Payments.Items.Should().ContainSingle();
        h.Gel.Balance.Should().Be(10_000m);
    }

    [Fact]
    public async Task OldShape_StoredDecline_IsReplayedAsDecline()
    {
        var h = new PaymentHarness(balance: 10m);
        var service = h.Service();

        var first = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));
        h.Gel.Credit(1_000m); // even if the account is funded now, the stored decline is replayed
        var second = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        first.FailureReason.Should().Be(PaymentDeclineReason.InsufficientFunds);
        second.Should().BeEquivalentTo(first);
        h.Payments.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task OldShape_Declines_InTheSameOrderAsBefore()
    {
        // frozen card and insufficient funds and over the limit: CardNotActive wins, as before
        var h = new PaymentHarness(transactionLimit: 10m, dailyLimit: 10m, balance: 1m);
        h.Card.Freeze();
        (await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m)))
            .FailureReason.Should().Be(PaymentDeclineReason.CardNotActive);

        // frozen account and over the limit: AccountNotActive
        h = new PaymentHarness(transactionLimit: 10m, dailyLimit: 10m);
        h.Gel.Freeze();
        (await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m)))
            .FailureReason.Should().Be(PaymentDeclineReason.AccountNotActive);

        // over the transaction limit and insufficient funds: TransactionLimitExceeded
        h = new PaymentHarness(transactionLimit: 10m, dailyLimit: 100m, balance: 1m);
        (await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m)))
            .FailureReason.Should().Be(PaymentDeclineReason.TransactionLimitExceeded);

        // over the daily limit and insufficient funds: DailyLimitExceeded
        h = new PaymentHarness(transactionLimit: 30m, dailyLimit: 30m);
        await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(20m, key: "a"));
        h.Gel.Debit(h.Gel.Balance - 1m);
        (await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(20m, key: "b")))
            .FailureReason.Should().Be(PaymentDeclineReason.DailyLimitExceeded);

        // only insufficient funds
        h = new PaymentHarness(balance: 1m);
        (await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m)))
            .FailureReason.Should().Be(PaymentDeclineReason.InsufficientFunds);
    }

    [Fact]
    public async Task OldShape_DeclineAfterPricing_IsStoredWithIdentityPricing_NoLedger()
    {
        var h = new PaymentHarness(balance: 1m);

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        dto.Status.Should().Be(SimulatedPaymentStatus.Failed);
        dto.ChargedAmount.Should().Be(25m);
        dto.LimitAmount.Should().Be(25m);
        h.LedgerTransactions.Items.Should().BeEmpty();
        h.Gel.Balance.Should().Be(1m);
        h.Uow.Commits.Should().Be(1);
    }

    [Fact]
    public async Task OldShape_UnknownCard_404()
    {
        var h = new PaymentHarness();
        var request = PaymentHarness.OldShape(25m);
        request.VirtualCardId = 999;

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, request))
            .Should().ThrowAsync<NotFoundException>().WithMessage("Virtual card not found");
    }

    [Fact]
    public async Task OldShape_SomeoneElsesCard_404()
    {
        var h = new PaymentHarness();
        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Stranger, PaymentHarness.OldShape(25m)))
            .Should().ThrowAsync<NotFoundException>();
    }
}

public class PaymentValidationTests
{
    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return new object[] { "null body", null!, "Request body is required." };
        yield return new object[] { "zero amount", PaymentHarness.OldShape(0m), "Amount must be positive with at most 2 decimals." };
        yield return new object[] { "3 decimals", PaymentHarness.OldShape(1.001m), "Amount must be positive with at most 2 decimals." };
        yield return new object[] { "no merchant", PaymentHarness.OldShape(1m, merchant: " "), "Merchant is required (max 100 characters)." };
        yield return new object[] { "no key", new CreatePaymentDto { VirtualCardId = 7, Amount = 1m, Merchant = "M", IdempotencyKey = null! }, "Idempotency key is required." };
        yield return new object[] { "long key", PaymentHarness.OldShape(1m, key: new string('k', 101)), "Idempotency key is too long." };
        yield return new object[] { "bad currency", PaymentHarness.NewShape(1m, (CurrencyCode)42), "Currency must be GEL, USD, EUR or GBP." };
        yield return new object[] { "bad expected", PaymentHarness.NewShape(1m, CurrencyCode.GEL, expected: 0.001m), "Expected charged amount must be positive with at most 2 decimals." };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequest_400_NothingTouched(string _, CreatePaymentDto request, string message)
    {
        var h = new PaymentHarness();

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, request))
            .Should().ThrowAsync<BadRequestException>().WithMessage(message);

        h.Uow.Begins.Should().Be(0);
        h.Payments.Items.Should().BeEmpty();
    }
}

public class AutoConversionTests
{
    [Fact]
    public async Task FlagOff_ForeignCurrency_StoresAutoConversionNotAllowed_NoRatesNoLedger()
    {
        var h = new PaymentHarness();

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));

        dto.Status.Should().Be(SimulatedPaymentStatus.Failed);
        dto.FailureReason.Should().Be(PaymentDeclineReason.AutoConversionNotAllowed);
        dto.Currency.Should().Be(CurrencyCode.USD);
        dto.ChargedCurrency.Should().Be(CurrencyCode.GEL);
        dto.ChargedAmount.Should().BeNull();
        dto.LimitAmount.Should().BeNull();
        h.Rates.Calls.Should().Be(0);
        h.LedgerTransactions.Items.Should().BeEmpty();
        h.Gel.Balance.Should().Be(10_000m);
        h.Payments.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task FlagOff_OtherOwnAccount_StoresSourceAccountNotAllowed()
    {
        var h = new PaymentHarness();

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.GEL, h.Gel2.Id));

        dto.FailureReason.Should().Be(PaymentDeclineReason.SourceAccountNotAllowed);
        dto.SourceBankAccountId.Should().Be(h.Gel2.Id);
        h.Gel2.Balance.Should().Be(10_000m);
    }

    [Fact]
    public async Task FlagOff_ExplicitMainAccountAndCurrency_SameAsOldShape()
    {
        var h = new PaymentHarness();

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.GEL, h.Gel.Id));

        dto.Status.Should().Be(SimulatedPaymentStatus.Completed);
        h.Gel.Balance.Should().Be(9_990m);
    }

    [Fact]
    public async Task FlagOn_GelCardPaysUsdFromUsdAccount_NoFxLegs_LimitConvertedToGel()
    {
        var h = new PaymentHarness(allowAutoConversion: true);

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD, h.Usd.Id));

        dto.Status.Should().Be(SimulatedPaymentStatus.Completed);
        dto.IsConversion.Should().BeFalse();
        dto.ChargedAmount.Should().Be(100m);
        dto.ChargedCurrency.Should().Be(CurrencyCode.USD);
        dto.FeeAmount.Should().Be(0m);
        dto.ExchangeRate.Should().Be(1m);
        dto.LimitAmount.Should().Be(270.00m);           // 100 USD * 2.7, in GEL (card limit currency)
        dto.RateDate.Should().Be(FakeNbgRates.RateDate); // a rate was used, for the limit
        h.Usd.Balance.Should().Be(9_900m);
        h.Gel.Balance.Should().Be(10_000m);

        var tx = h.LedgerFor(dto.Reference).Should().ContainSingle().Subject;
        tx.Reference.Should().Be(dto.Reference);
        tx.Currency.Should().Be(CurrencyCode.USD);
    }

    [Fact]
    public async Task FlagOn_GelCardPaysUsdFromGelAccount_ConvertsWithFee()
    {
        var h = new PaymentHarness(allowAutoConversion: true);

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD));

        dto.Status.Should().Be(SimulatedPaymentStatus.Completed);
        dto.IsConversion.Should().BeTrue();
        dto.SourceBankAccountId.Should().Be(h.Gel.Id);
        dto.ChargedAmount.Should().Be(271.37m);
        dto.FeeAmount.Should().Be(1.36m);
        dto.ExchangeRate.Should().Be(0.37037037m);
        dto.LimitAmount.Should().Be(270.00m);
        dto.RateDate.Should().Be(FakeNbgRates.RateDate);
        h.Gel.Balance.Should().Be(10_000m - 271.37m);

        var legs = h.LedgerFor(dto.Reference);
        legs.Select(t => t.Reference).Should().BeEquivalentTo(new[] { dto.Reference + "-OUT", dto.Reference + "-IN" });
        legs.Should().OnlyContain(t => t.TransactionType == LedgerTransactionType.CardPayment);
        LedgerAssert.BalancedPerCurrency(legs);
    }

    [Fact]
    public async Task FlagOn_SourceOfAnotherUser_404_NothingStored()
    {
        var h = new PaymentHarness(allowAutoConversion: true);

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD, h.StrangerUsd.Id)))
            .Should().ThrowAsync<NotFoundException>().WithMessage("Source account not found.");

        h.Payments.Items.Should().BeEmpty();
        h.StrangerUsd.Balance.Should().Be(10_000m);
        h.Uow.Rollbacks.Should().Be(1);
    }

    [Fact]
    public async Task FlagOn_UnknownSource_404()
    {
        var h = new PaymentHarness(allowAutoConversion: true);

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD, 12345)))
            .Should().ThrowAsync<NotFoundException>();
        h.Payments.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task FlagOn_FrozenSource_StoresAccountNotActive_NoRates()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Usd.Freeze();

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD, h.Usd.Id));

        dto.FailureReason.Should().Be(PaymentDeclineReason.AccountNotActive);
        dto.ChargedAmount.Should().BeNull();
        h.Rates.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Limits_AreCheckedInMainAccountCurrency()
    {
        // tx limit 250 GEL; 100 USD = 270 GEL -> declined even though 100 < 250
        var h = new PaymentHarness(allowAutoConversion: true, transactionLimit: 250m, dailyLimit: 1000m);

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD, h.Usd.Id));

        dto.FailureReason.Should().Be(PaymentDeclineReason.TransactionLimitExceeded);
        dto.LimitAmount.Should().Be(270m);
        dto.ChargedAmount.Should().Be(100m); // priced, then declined
        h.Usd.Balance.Should().Be(10_000m);
    }

    [Fact]
    public async Task DailyLimit_SumsLimitAmounts_OfLegacyAndConvertedPayments()
    {
        var h = new PaymentHarness(allowAutoConversion: true, transactionLimit: 1000m, dailyLimit: 1000m);
        var legacy = SimulatedPayment.Create(h.Card, h.Gel, CurrencyCode.GEL, 700m, "Shop", null, "legacy", DateTime.UtcNow);
        legacy.ApplyPricing(700m, 1m, 0m, 700m, null);
        legacy.Settle(h.Gel, h.LedgerAccounts.Items.Single(a => a.AccountCode == LedgerAccountCodes.CardSettlement(CurrencyCode.GEL)));
        h.Payments.SeedCommitted(legacy);

        var service = h.Service();
        // 100 EUR from the EUR account = 290 GEL of limit: 700 + 290 = 990 <= 1000
        (await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.EUR, h.Eur.Id, key: "a")))
            .Status.Should().Be(SimulatedPaymentStatus.Completed);
        // 10 USD = 27 GEL: 990 + 27 > 1000
        (await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD, h.Usd.Id, key: "b")))
            .FailureReason.Should().Be(PaymentDeclineReason.DailyLimitExceeded);
        // 3.70 USD = 9.99 GEL: 990 + 9.99 <= 1000
        (await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(3.70m, CurrencyCode.USD, h.Usd.Id, key: "c")))
            .Status.Should().Be(SimulatedPaymentStatus.Completed);
    }
}

public class RateProtectionAndNbgTests
{
    [Fact]
    public async Task ExpectedChargedAmountMatches_Completes()
    {
        var h = new PaymentHarness(allowAutoConversion: true);

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD, expected: 271.37m));

        dto.Status.Should().Be(SimulatedPaymentStatus.Completed);
    }

    [Fact]
    public async Task ExpectedChargedAmountDiffers_409_NothingStored()
    {
        var h = new PaymentHarness(allowAutoConversion: true);

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD, expected: 271.36m)))
            .Should().ThrowAsync<ConflictException>()
            .WithMessage("The charged amount has changed: it is now 271.37 GEL (expected 271.36). Please request a new quote.");

        h.Payments.Items.Should().BeEmpty();
        h.LedgerTransactions.Items.Should().BeEmpty();
        h.Gel.Balance.Should().Be(10_000m);
        h.Card.LastAuthorizedAt.Should().BeNull(); // rolled back
        h.Uow.Commits.Should().Be(0);
        h.Uow.Rollbacks.Should().Be(1);
    }

    [Fact]
    public async Task ExpectedChargedAmount_AlsoAppliesWithoutConversion()
    {
        var h = new PaymentHarness();

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.GEL, expected: 10.01m)))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task RateChangesBetweenQuoteAndPay_409_ThenRequoteSucceeds()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        var service = h.Service();
        var quote = await service.QuoteAsync(PaymentHarness.Owner, new PaymentQuoteRequestDto { VirtualCardId = 7, Amount = 100m, Currency = CurrencyCode.USD });
        var quoted = quote.Accounts.Single(a => a.AccountId == h.Gel.Id).ChargedAmount;

        h.Rates.SetLariPerUnit(CurrencyCode.USD, 2.75m); // NBG publishes a new table

        await service.Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD, expected: quoted)))
            .Should().ThrowAsync<ConflictException>();

        var requote = await service.QuoteAsync(PaymentHarness.Owner, new PaymentQuoteRequestDto { VirtualCardId = 7, Amount = 100m, Currency = CurrencyCode.USD });
        var dto = await service.PayAsync(PaymentHarness.Owner,
            PaymentHarness.NewShape(100m, CurrencyCode.USD, expected: requote.Accounts.Single(a => a.AccountId == h.Gel.Id).ChargedAmount));
        dto.Status.Should().Be(SimulatedPaymentStatus.Completed);
    }

    [Fact]
    public async Task NbgUnavailable_ConversionNeeded_503_NothingStored()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Rates.Unavailable = true;

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD)))
            .Should().ThrowAsync<ExternalServiceException>();

        h.Payments.Items.Should().BeEmpty();
        h.Gel.Balance.Should().Be(10_000m);
    }

    [Fact]
    public async Task NbgUnavailable_SameCurrencyNoLimitConversion_StillWorks()
    {
        var h = new PaymentHarness();
        h.Rates.Unavailable = true;

        (await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(10m)))
            .Status.Should().Be(SimulatedPaymentStatus.Completed);
    }

    [Fact]
    public async Task NbgUnavailable_StatusDeclineNeedsNoRate_StillStored()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Card.Freeze();
        h.Rates.Unavailable = true;

        (await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD)))
            .FailureReason.Should().Be(PaymentDeclineReason.CardNotActive);
    }

    [Fact]
    public async Task NbgUnavailable_ReplayStillWorks()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        var service = h.Service();
        var first = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));
        h.Rates.Unavailable = true;

        var second = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));

        second.Should().BeEquivalentTo(first);
    }
}

public class NewFieldIdempotencyTests
{
    [Fact]
    public async Task SameKey_DifferentCurrency_409()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        var service = h.Service();
        await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));

        await service.Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.EUR)))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task SameKey_DifferentSource_409()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        var service = h.Service();
        await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));

        await service.Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD, h.Usd.Id)))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task OldShapeAfterExplicitDefaults_IsTheSameRequest()
    {
        var h = new PaymentHarness();
        var service = h.Service();
        var first = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.GEL, h.Gel.Id));
        var request = PaymentHarness.OldShape(10m);

        (await service.PayAsync(PaymentHarness.Owner, request)).Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task ExpectedChargedAmount_IsNotPartOfTheFingerprint()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        var service = h.Service();
        var first = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD, expected: 271.37m));

        var replay = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD, expected: 999m));

        replay.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task StoredAutoConversionDecline_IsReplayed_EvenAfterFlagIsTurnedOn()
    {
        var h = new PaymentHarness();
        var service = h.Service();
        var first = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));
        h.Card.SetAutoConversion(true);

        var second = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));

        second.Should().BeEquivalentTo(first);
        second.FailureReason.Should().Be(PaymentDeclineReason.AutoConversionNotAllowed);
    }
}

/// <summary>Requirement 4 (unit level; the real-database version is in FxCopyDbConcurrencyTests).</summary>
public class RetryTests
{
    [Fact]
    public async Task ConcurrencyConflict_Retried_DebitsOnce_WritesLedgerOnce()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        h.Uow.OnSave = n => n == 1 ? throw new ConcurrencyConflictException() : Task.CompletedTask;

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD));

        dto.Status.Should().Be(SimulatedPaymentStatus.Completed);
        h.Gel.Balance.Should().Be(10_000m - 271.37m);        // debited exactly once
        h.Payments.Items.Should().ContainSingle();
        h.LedgerTransactions.Items.Should().HaveCount(2);     // one -OUT and one -IN, for the final attempt only
        h.LedgerTransactions.Items.Should().OnlyContain(t => t.Reference.StartsWith(dto.Reference));
        h.Uow.Rollbacks.Should().Be(1);
        h.Uow.Commits.Should().Be(1);
        h.Uow.ClearTrackingCalls.Should().Be(1);
        LedgerAssert.BalancedPerCurrency(h.LedgerTransactions.Items);
    }

    [Fact]
    public async Task ConcurrencyConflict_OnEveryAttempt_409AfterThreeAttempts_NothingStored()
    {
        var h = new PaymentHarness();
        h.Uow.OnSave = _ => throw new ConcurrencyConflictException();

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(10m)))
            .Should().ThrowAsync<ConcurrencyConflictException>();

        h.Uow.Begins.Should().Be(3);
        h.Uow.Rollbacks.Should().Be(3);
        h.Uow.Commits.Should().Be(0);
        h.Gel.Balance.Should().Be(10_000m);
        h.Payments.Items.Should().BeEmpty();
        h.LedgerTransactions.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateKey_ConcurrentIdenticalRequestWon_ReplaysItsResult_NoSecondDebit()
    {
        var h = new PaymentHarness();
        SimulatedPayment? winner = null;
        h.Uow.OnSave = n =>
        {
            if (n != 1) return Task.CompletedTask;
            // Another request with the same key committed first (its own debit is not modelled here).
            winner = SimulatedPayment.Create(h.Card, h.Gel, CurrencyCode.GEL, 25m, "Demo Shop", "Order 1", "key-1", DateTime.UtcNow);
            winner.ApplyPricing(25m, 1m, 0m, 25m, null);
            winner.Fail(PaymentDeclineReason.InsufficientFunds);
            h.Payments.SeedCommitted(winner);
            throw new DuplicateKeyException();
        };

        var dto = await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        dto.Id.Should().Be(winner!.Id);
        dto.Reference.Should().Be(winner.Reference);
        dto.FailureReason.Should().Be(PaymentDeclineReason.InsufficientFunds); // the stored result, not ours
        h.Gel.Balance.Should().Be(10_000m);                                      // our attempt was rolled back
        h.Payments.Items.Should().ContainSingle();
        h.LedgerTransactions.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateKey_ConcurrentDifferentRequestWon_409()
    {
        var h = new PaymentHarness();
        h.Uow.OnSave = n =>
        {
            if (n != 1) return Task.CompletedTask;
            var other = SimulatedPayment.Create(h.Card, h.Gel, CurrencyCode.GEL, 99m, "Other", null, "key-1", DateTime.UtcNow);
            other.ApplyPricing(99m, 1m, 0m, 99m, null);
            other.Fail(PaymentDeclineReason.InsufficientFunds);
            h.Payments.SeedCommitted(other);
            throw new DuplicateKeyException();
        };

        await h.Service().Invoking(s => s.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m)))
            .Should().ThrowAsync<ConflictException>().WithMessage("Idempotency Key already used for a differenc payment");
        h.Gel.Balance.Should().Be(10_000m);
    }

    [Fact]
    public async Task EveryAuthorization_TouchesTheCard_IncludingDeclines()
    {
        var h = new PaymentHarness(balance: 1m);
        await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));
        h.Card.LastAuthorizedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Replay_DoesNotTouchTheCard()
    {
        var h = new PaymentHarness();
        var service = h.Service();
        await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));
        var touched = h.Card.LastAuthorizedAt;

        await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        h.Card.LastAuthorizedAt.Should().Be(touched);
    }
}

/// <summary>Requirement 6: double entry holds per currency for every pair, non-main sources and fee = 0.</summary>
public class PaymentLedgerInvariantTests
{
    private static readonly decimal[] Amounts = { 0.01m, 0.10m, 0.99m, 1.00m, 37.53m, 250.00m };

    public static IEnumerable<object[]> Cases()
    {
        var currencies = Enum.GetValues<CurrencyCode>();
        foreach (var main in currencies)               // card main-account currency (limit currency)
            foreach (var merchant in currencies)       // merchant currency
                foreach (var source in currencies)     // source account currency
                    yield return new object[] { main, merchant, source };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryPayment_BalancesPerCurrency(CurrencyCode main, CurrencyCode merchant, CurrencyCode sourceCcy)
    {
        var h = new PaymentHarness(allowAutoConversion: true, mainCurrency: main);
        var service = h.Service();
        // Use a non-main account whenever one exists for the source currency (GEL has two accounts).
        var source = h.Accounts.Items
            .Where(a => a.UserId == PaymentHarness.Owner && a.Currency == sourceCcy)
            .OrderBy(a => a.Id == h.Card.BankAccountId) // non-main first
            .First();

        var i = 0;
        foreach (var amount in Amounts)
        {
            var before = source.Balance;
            var dto = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(amount, merchant, source.Id, key: $"k{i++}"));

            dto.Status.Should().Be(SimulatedPaymentStatus.Completed, $"{amount} {merchant} from {sourceCcy}");
            var legs = h.LedgerFor(dto.Reference);

            // the invariant, for this payment alone
            LedgerAssert.BalancedPerCurrency(legs);

            // the customer side equals the balance change and the stored charge
            var customerDebits = legs.SelectMany(t => t.Entries).Where(e => e.BankAccountId == source.Id).ToList();
            customerDebits.Should().ContainSingle().Which.Amount.Should().Be(dto.ChargedAmount!.Value);
            (before - source.Balance).Should().Be(dto.ChargedAmount!.Value);

            // the merchant side: card settlement in the merchant currency receives exactly the amount
            var settlement = h.LedgerId(LedgerAccountCodes.CardSettlement(merchant));
            legs.SelectMany(t => t.Entries).Where(e => e.LedgerAccountId == settlement)
                .Should().ContainSingle().Which.Should().Match<LedgerEntry>(e =>
                    e.Type == EntryType.Credit && e.Amount == amount && e.Currency == merchant);

            if (merchant == sourceCcy)
            {
                legs.Should().ContainSingle();
                dto.FeeAmount.Should().Be(0m);
                dto.ChargedAmount.Should().Be(amount);
            }
            else
            {
                legs.Should().HaveCount(2);
                var fee = h.LedgerId(LedgerAccountCodes.Fee(sourceCcy));
                var feeLines = legs.SelectMany(t => t.Entries).Where(e => e.LedgerAccountId == fee).ToList();
                if (dto.FeeAmount == 0m) feeLines.Should().BeEmpty();
                else feeLines.Should().ContainSingle().Which.Amount.Should().Be(dto.FeeAmount!.Value);

                // the merchant is never under-paid by the conversion
                FxMath.Forward(dto.ChargedAmount!.Value, dto.ExchangeRate!.Value, 0.005m).ToAmount.Should().BeGreaterThanOrEqualTo(amount);
            }
        }

        // and for everything written in this scenario together
        LedgerAssert.BalancedPerCurrency(h.LedgerTransactions.Items);
    }

    [Fact]
    public async Task Grid_ContainsFeeZeroConversions_AndNonMainSameCurrencyPayments()
    {
        // Guards the grid above: it must exercise conversions whose fee rounds to 0,
        // and a same-currency payment from a non-main account.
        var h = new PaymentHarness(allowAutoConversion: true);
        var service = h.Service();

        var tiny = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(0.10m, CurrencyCode.USD, h.Gel.Id, key: "t"));
        tiny.IsConversion.Should().BeTrue();
        tiny.FeeAmount.Should().Be(0m);
        h.LedgerFor(tiny.Reference).Single(t => t.Reference.EndsWith("-OUT")).Entries.Should().HaveCount(2);

        var nonMain = await service.PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(5m, CurrencyCode.GEL, h.Gel2.Id, key: "n"));
        nonMain.SourceBankAccountId.Should().NotBe(h.Card.BankAccountId);
        nonMain.IsConversion.Should().BeFalse();
        h.Gel2.Balance.Should().Be(9_995m);

        LedgerAssert.BalancedPerCurrency(h.LedgerTransactions.Items);
    }
}
