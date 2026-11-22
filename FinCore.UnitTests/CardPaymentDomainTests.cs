using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Tests.Support;
using FluentAssertions;

namespace FinCore.Tests.Domain;

public class VirtualCardAuthorizationTests
{
    private const string Owner = "owner";
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private static BankAccount Account(int id, CurrencyCode ccy, decimal balance = 1000m, string owner = Owner)
    {
        var a = BankAccount.Create($"ACC{id}", owner, ccy);
        TestEntity.SetId(a, id);
        if (balance > 0) a.Credit(balance);
        return a;
    }

    private static VirtualCard Card(BankAccount main, bool auto = false)
    {
        var c = VirtualCard.Create(Owner, main, "Card", "1234", 1000m, 500m, Now, auto);
        TestEntity.SetId(c, 7);
        return c;
    }

    [Fact]
    public void AutoConversion_DefaultsToOff_AndCanBeToggled()
    {
        var card = Card(Account(1, CurrencyCode.GEL));
        card.AllowAutoConversion.Should().BeFalse();

        card.SetAutoConversion(true);
        card.AllowAutoConversion.Should().BeTrue();
    }

    [Fact]
    public void AutoConversion_CannotChangeOnClosedCard()
    {
        var card = Card(Account(1, CurrencyCode.GEL));
        card.Close();

        card.Invoking(c => c.SetAutoConversion(true)).Should().Throw<DomainException>();
    }

    [Fact]
    public void FlagOff_OwnAccountOwnCurrency_Passes()
    {
        var main = Account(1, CurrencyCode.GEL);
        Card(main).AuthorizeSource(main, CurrencyCode.GEL, CurrencyCode.GEL, Now).Should().BeNull();
    }

    [Fact]
    public void FlagOff_ForeignCurrency_DeclinesAutoConversionNotAllowed()
    {
        var main = Account(1, CurrencyCode.GEL);
        Card(main).AuthorizeSource(main, CurrencyCode.USD, CurrencyCode.GEL, Now)
            .Should().Be(PaymentDeclineReason.AutoConversionNotAllowed);
    }

    [Fact]
    public void FlagOff_OtherAccount_DeclinesSourceAccountNotAllowed()
    {
        var main = Account(1, CurrencyCode.GEL);
        var other = Account(2, CurrencyCode.GEL);
        Card(main).AuthorizeSource(other, CurrencyCode.GEL, CurrencyCode.GEL, Now)
            .Should().Be(PaymentDeclineReason.SourceAccountNotAllowed);
    }

    [Fact]
    public void FlagOn_OtherAccountForeignCurrency_Passes()
    {
        var main = Account(1, CurrencyCode.GEL);
        var usd = Account(2, CurrencyCode.USD);
        Card(main, auto: true).AuthorizeSource(usd, CurrencyCode.EUR, CurrencyCode.GEL, Now).Should().BeNull();
    }

    [Fact]
    public void SourceOfAnotherUser_Throws()
    {
        var main = Account(1, CurrencyCode.GEL);
        var foreign = Account(2, CurrencyCode.GEL, owner: "someone-else");
        Card(main, auto: true).Invoking(c => c.AuthorizeSource(foreign, CurrencyCode.GEL, CurrencyCode.GEL, Now))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Order_CardNotActive_BeatsEverything()
    {
        var main = Account(1, CurrencyCode.GEL);
        var other = Account(2, CurrencyCode.USD);
        other.Freeze();
        var card = Card(main);
        card.Freeze();

        // also wrong source, wrong currency and frozen source: CardNotActive wins
        card.AuthorizeSource(other, CurrencyCode.EUR, CurrencyCode.GEL, Now).Should().Be(PaymentDeclineReason.CardNotActive);
    }

    [Fact]
    public void Order_CardExpired_BeforeSourceChecks()
    {
        var main = Account(1, CurrencyCode.GEL);
        var other = Account(2, CurrencyCode.GEL);
        Card(main).AuthorizeSource(other, CurrencyCode.USD, CurrencyCode.GEL, Now.AddYears(4))
            .Should().Be(PaymentDeclineReason.CardExpired);
    }

    [Fact]
    public void Order_SourceAccountNotAllowed_BeforeAutoConversionNotAllowed()
    {
        var main = Account(1, CurrencyCode.GEL);
        var other = Account(2, CurrencyCode.USD);
        Card(main).AuthorizeSource(other, CurrencyCode.USD, CurrencyCode.GEL, Now)
            .Should().Be(PaymentDeclineReason.SourceAccountNotAllowed);
    }

    [Fact]
    public void Order_AutoConversionNotAllowed_BeforeAccountNotActive()
    {
        var main = Account(1, CurrencyCode.GEL);
        var card = Card(main);
        main.Freeze();
        card.AuthorizeSource(main, CurrencyCode.USD, CurrencyCode.GEL, Now)
            .Should().Be(PaymentDeclineReason.AutoConversionNotAllowed);
    }

    [Fact]
    public void InactiveSource_DeclinesAccountNotActive()
    {
        var main = Account(1, CurrencyCode.GEL);
        var other = Account(2, CurrencyCode.USD);
        other.Freeze();
        Card(main, auto: true).AuthorizeSource(other, CurrencyCode.USD, CurrencyCode.GEL, Now)
            .Should().Be(PaymentDeclineReason.AccountNotActive);
    }

    [Theory]
    // limitAmount, chargedAmount, spentToday, balance -> expected (card: tx limit 500, daily 1000)
    [InlineData("500.00", "500.00", "0", "1000", null)]
    [InlineData("500.01", "1.00", "0", "1000", PaymentDeclineReason.TransactionLimitExceeded)]
    [InlineData("500.01", "5000.00", "999", "1", PaymentDeclineReason.TransactionLimitExceeded)] // tx limit wins
    [InlineData("100.00", "5000.00", "900.01", "1", PaymentDeclineReason.DailyLimitExceeded)]   // daily before funds
    [InlineData("100.00", "1000.01", "900.00", "1000", PaymentDeclineReason.InsufficientFunds)]
    public void AuthorizeAmounts_FixedOrder(string limit, string charged, string spent, string balance, PaymentDeclineReason? expected)
    {
        var main = Account(1, CurrencyCode.GEL, decimal.Parse(balance));
        Card(main).AuthorizeAmounts(main, decimal.Parse(limit), decimal.Parse(charged), decimal.Parse(spent))
            .Should().Be(expected);
    }

    [Fact]
    public void RegisterAuthorization_TouchesTheCard()
    {
        var card = Card(Account(1, CurrencyCode.GEL));
        card.RegisterAuthorization(Now);
        card.LastAuthorizedAt.Should().Be(Now);
    }
}

public class SimulatedPaymentSettlementTests
{
    private const string Owner = "owner";
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly BankAccount _gel;
    private readonly BankAccount _usd;
    private readonly VirtualCard _card;
    private readonly Dictionary<string, LedgerAccount> _ledger = new();

    public SimulatedPaymentSettlementTests()
    {
        _gel = BankAccount.Create("GEL1", Owner, CurrencyCode.GEL);
        TestEntity.SetId(_gel, 1);
        _gel.Credit(1000m);
        _usd = BankAccount.Create("USD1", Owner, CurrencyCode.USD);
        TestEntity.SetId(_usd, 2);
        _usd.Credit(1000m);
        _card = VirtualCard.Create(Owner, _gel, "Card", "1234", 1000m, 500m, Now, allowAutoConversion: true);
        TestEntity.SetId(_card, 7);

        var id = 100;
        foreach (var c in Enum.GetValues<CurrencyCode>())
            foreach (var code in new[] { LedgerAccountCodes.Fx(c), LedgerAccountCodes.Fee(c), LedgerAccountCodes.CardSettlement(c) })
            {
                var la = LedgerAccount.Create(code, code, c);
                TestEntity.SetId(la, id++);
                _ledger[code] = la;
            }
    }

    private SimulatedPayment NewPayment(BankAccount source, CurrencyCode ccy, decimal amount) =>
        SimulatedPayment.Create(_card, source, ccy, amount, "Shop", null, "key", Now);

    [Fact]
    public void Create_SetsSourceAndChargedCurrency_LeavesPricingNull()
    {
        var p = NewPayment(_gel, CurrencyCode.USD, 100m);

        p.SourceBankAccountId.Should().Be(1);
        p.ChargedCurrency.Should().Be(CurrencyCode.GEL);
        p.IsPriced.Should().BeFalse();
        p.ChargedAmount.Should().BeNull();
        p.LimitAmount.Should().BeNull();
    }

    [Fact]
    public void DeclineBeforePricing_KeepsPricingNull()
    {
        var p = NewPayment(_gel, CurrencyCode.USD, 100m);
        p.Fail(PaymentDeclineReason.CardNotActive);

        p.Status.Should().Be(SimulatedPaymentStatus.Failed);
        p.ChargedAmount.Should().BeNull();
    }

    [Fact]
    public void ApplyPricing_WithoutConversion_MustBeIdentity()
    {
        var p = NewPayment(_gel, CurrencyCode.GEL, 10m);
        p.Invoking(x => x.ApplyPricing(10.05m, 1m, 0.05m, 10m, null)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Settle_WithoutPricing_Throws()
    {
        var p = NewPayment(_gel, CurrencyCode.GEL, 10m);
        p.Invoking(x => x.Settle(_gel, _ledger[LedgerAccountCodes.CardSettlement(CurrencyCode.GEL)]))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Settle_SameCurrency_OneTransactionLikeToday()
    {
        var p = NewPayment(_gel, CurrencyCode.GEL, 25m);
        p.ApplyPricing(25m, 1m, 0m, 25m, null);

        var txs = p.Settle(_gel, _ledger[LedgerAccountCodes.CardSettlement(CurrencyCode.GEL)]);

        txs.Should().ContainSingle();
        txs[0].Reference.Should().Be(p.Reference);
        txs[0].TransactionType.Should().Be(LedgerTransactionType.CardPayment);
        _gel.Balance.Should().Be(975m);
        p.Status.Should().Be(SimulatedPaymentStatus.Completed);
        LedgerAssert.BalancedPerCurrency(txs);
    }

    [Fact]
    public void Settle_Conversion_TwoLegsEachBalancedInItsCurrency()
    {
        // 100.00 USD charged from GEL: 271.37 GEL = 270.01 net + 1.36 fee
        var p = NewPayment(_gel, CurrencyCode.USD, 100m);
        p.ApplyPricing(271.37m, 0.37037037m, 1.36m, 271.37m, Now.Date);

        var txs = p.Settle(_gel,
            _ledger[LedgerAccountCodes.CardSettlement(CurrencyCode.USD)],
            _ledger[LedgerAccountCodes.Fx(CurrencyCode.GEL)],
            _ledger[LedgerAccountCodes.Fx(CurrencyCode.USD)],
            _ledger[LedgerAccountCodes.Fee(CurrencyCode.GEL)]);

        txs.Should().HaveCount(2);
        txs.Should().OnlyContain(t => t.TransactionType == LedgerTransactionType.CardPayment);

        var outTx = txs.Single(t => t.Reference == $"{p.Reference}-OUT");
        outTx.Currency.Should().Be(CurrencyCode.GEL);
        outTx.Entries.Select(e => (e.BankAccountId, e.LedgerAccountId, e.Type, e.Amount)).Should().BeEquivalentTo(new[]
        {
            ((int?)1, (int?)null, EntryType.Debit, 271.37m),
            ((int?)null, (int?)_ledger[LedgerAccountCodes.Fx(CurrencyCode.GEL)].Id, EntryType.Credit, 270.01m),
            ((int?)null, (int?)_ledger[LedgerAccountCodes.Fee(CurrencyCode.GEL)].Id, EntryType.Credit, 1.36m),
        });

        var inTx = txs.Single(t => t.Reference == $"{p.Reference}-IN");
        inTx.Currency.Should().Be(CurrencyCode.USD);
        inTx.Entries.Select(e => (e.BankAccountId, e.LedgerAccountId, e.Type, e.Amount)).Should().BeEquivalentTo(new[]
        {
            ((int?)null, (int?)_ledger[LedgerAccountCodes.Fx(CurrencyCode.USD)].Id, EntryType.Debit, 100.00m),
            ((int?)null, (int?)_ledger[LedgerAccountCodes.CardSettlement(CurrencyCode.USD)].Id, EntryType.Credit, 100.00m),
        });

        _gel.Balance.Should().Be(1000m - 271.37m);
        LedgerAssert.BalancedPerCurrency(txs);
    }

    [Fact]
    public void Settle_Conversion_WrongLedgerCurrency_Throws()
    {
        var p = NewPayment(_gel, CurrencyCode.USD, 100m);
        p.ApplyPricing(271.37m, 0.37037037m, 1.36m, 271.37m, Now.Date);

        p.Invoking(x => x.Settle(_gel,
            _ledger[LedgerAccountCodes.CardSettlement(CurrencyCode.USD)],
            _ledger[LedgerAccountCodes.Fx(CurrencyCode.USD)],   // wrong: should be FX-GEL
            _ledger[LedgerAccountCodes.Fx(CurrencyCode.USD)],
            _ledger[LedgerAccountCodes.Fee(CurrencyCode.GEL)])).Should().Throw<DomainException>();
        _gel.Balance.Should().Be(1000m);
    }

    [Fact]
    public void Settle_InsufficientFunds_ThrowsAndDoesNotComplete()
    {
        var p = NewPayment(_usd, CurrencyCode.USD, 1000.01m);
        p.ApplyPricing(1000.01m, 1m, 0m, 2700.03m, null);

        p.Invoking(x => x.Settle(_usd, _ledger[LedgerAccountCodes.CardSettlement(CurrencyCode.USD)]))
            .Should().Throw<InsufficientFundsException>();
        p.Status.Should().Be(SimulatedPaymentStatus.Pending);
    }
}

/// <summary>The double-entry invariant: per currency, total debits equal total credits.</summary>
internal static class LedgerAssert
{
    public static void BalancedPerCurrency(IEnumerable<LedgerTransaction> transactions)
    {
        var txs = transactions.ToList();

        foreach (var tx in txs)
            tx.Entries.Should().OnlyContain(e => e.Currency == tx.Currency,
                $"every entry of {tx.Reference} must be in the transaction currency");

        var perCurrency = txs.SelectMany(t => t.Entries)
            .GroupBy(e => e.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Debits = g.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount),
                Credits = g.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount)
            })
            .ToList();

        perCurrency.Should().NotBeEmpty();
        foreach (var c in perCurrency)
            c.Debits.Should().Be(c.Credits, $"debits and credits must balance in {c.Currency}");
    }
}
