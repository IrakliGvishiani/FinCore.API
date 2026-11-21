using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Application.Services;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;

namespace FinCore.Tests.Support;

/// <summary>Deterministic stand-in for the NBG provider: same cross-rate formula via GEL, 8-decimal rounding.</summary>
internal sealed class FakeNbgRates : IExchangeRateProvider
{
    public static readonly DateTime RateDate = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly Dictionary<CurrencyCode, decimal> _lariPerUnit = new()
    {
        [CurrencyCode.GEL] = 1m, [CurrencyCode.USD] = 2.7m, [CurrencyCode.EUR] = 2.9m, [CurrencyCode.GBP] = 3.4m
    };

    public int Calls { get; private set; }
    public bool Unavailable { get; set; }

    public void SetLariPerUnit(CurrencyCode c, decimal value) => _lariPerUnit[c] = value;

    public static decimal Rate(CurrencyCode from, CurrencyCode to, IReadOnlyDictionary<CurrencyCode, decimal>? table = null)
    {
        table ??= new FakeNbgRates()._lariPerUnit;
        return decimal.Round(table[from] / table[to], 8);
    }

    public Task<ExchangeRate> GetRateAsync(CurrencyCode from, CurrencyCode to, CancellationToken ct = default)
    {
        Calls++;
        if (Unavailable) throw new ExternalServiceException("Exchange rates are temporarily unavailable.");
        if (from == to) throw new BadRequestException("Currencies must be different");
        return Task.FromResult(new ExchangeRate(from, to, Rate(from, to, _lariPerUnit), RateDate));
    }
}

/// <summary>
/// One customer with accounts in every currency (plus a second GEL account), a GEL-main card,
/// seeded FX/FEE/CARD ledger accounts, and the real payment service over in-memory stores.
/// </summary>
internal sealed class PaymentHarness
{
    public const string Owner = "owner-1";
    public const string Stranger = "stranger-9";

    public readonly InMemoryRepository<SimulatedPayment> Payments = new();
    public readonly InMemoryRepository<VirtualCard> Cards = new();
    public readonly InMemoryRepository<BankAccount> Accounts = new();
    public readonly InMemoryRepository<LedgerAccount> LedgerAccounts = new();
    public readonly InMemoryRepository<LedgerTransaction> LedgerTransactions = new();
    public readonly RecordingUnitOfWork Uow;
    public readonly Mock<IAuditService> Audit = new();
    public readonly FakeNbgRates Rates = new();

    public readonly BankAccount Gel, Usd, Eur, Gbp, Gel2, StrangerUsd;
    public VirtualCard Card { get; private set; }

    public PaymentHarness(bool allowAutoConversion = false, decimal dailyLimit = 100_000m, decimal transactionLimit = 100_000m,
        decimal balance = 10_000m, CurrencyCode mainCurrency = CurrencyCode.GEL)
    {
        Uow = new RecordingUnitOfWork(Payments, Cards, Accounts, LedgerAccounts, LedgerTransactions);

        Gel = AddAccount(1, CurrencyCode.GEL, balance);
        Usd = AddAccount(2, CurrencyCode.USD, balance);
        Eur = AddAccount(3, CurrencyCode.EUR, balance);
        Gbp = AddAccount(4, CurrencyCode.GBP, balance);
        Gel2 = AddAccount(5, CurrencyCode.GEL, balance);
        StrangerUsd = AddAccount(9, CurrencyCode.USD, balance, Stranger);

        var main = Accounts.Items.First(a => a.Currency == mainCurrency && a.UserId == Owner);
        Card = VirtualCard.Create(Owner, main, "Main card", "4242", dailyLimit, transactionLimit,
            DateTime.UtcNow.AddDays(-1), allowAutoConversion);
        Cards.Seed(Card, 7);

        var id = 100;
        foreach (var c in Enum.GetValues<CurrencyCode>())
            foreach (var code in new[] { LedgerAccountCodes.Fx(c), LedgerAccountCodes.Fee(c), LedgerAccountCodes.CardSettlement(c) })
                LedgerAccounts.Seed(LedgerAccount.Create(code, code, c), id++);
    }

    public BankAccount AddAccount(int id, CurrencyCode ccy, decimal balance, string owner = Owner)
    {
        var a = BankAccount.Create($"GE00TEST{id:D4}", owner, ccy);
        Accounts.Seed(a, id);
        if (balance > 0) a.Credit(balance);
        return a;
    }

    public SimulatedPaymentService Service(IUnitOfWork? uow = null) => new(
        Payments, Cards, Accounts, LedgerAccounts, LedgerTransactions, uow ?? Uow, Audit.Object,
        new FxPricingService(Rates, Options.Create(new ConversionOptions())));

    public int LedgerId(string code) => LedgerAccounts.Items.Single(a => a.AccountCode == code).Id;

    public static CreatePaymentDto OldShape(decimal amount, string key = "key-1", string merchant = "Demo Shop", string? description = "Order 1")
        => new() { VirtualCardId = 7, Amount = amount, Merchant = merchant, Description = description, IdempotencyKey = key };

    public static CreatePaymentDto NewShape(decimal amount, CurrencyCode currency, int? sourceAccountId = null,
        decimal? expected = null, string key = "key-1")
        => new()
        {
            VirtualCardId = 7, Amount = amount, Merchant = "Demo Shop", Description = "Order 1", IdempotencyKey = key,
            Currency = currency, SourceAccountId = sourceAccountId, ExpectedChargedAmount = expected
        };

    /// <summary>Ledger transactions that survived (committed) for one payment reference.</summary>
    public List<LedgerTransaction> LedgerFor(string paymentReference) =>
        LedgerTransactions.Items.Where(t => t.Reference == paymentReference || t.Reference.StartsWith(paymentReference + "-")).ToList();
}
