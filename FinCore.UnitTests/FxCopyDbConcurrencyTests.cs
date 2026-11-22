using FinCore.Application.Contracts.Services;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Application.Services;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Infrastructure;
using FinCore.Infrastructure.Persistence;
using FinCore.Tests.Support;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;

namespace FinCore.Tests.Integration;

/// <summary>
/// Runs only when FINCORE_FXCOPY_TESTS_CONNECTION is set, and only against a database named FinCoreDb_fxcopy.
/// Uses the real EF Core stack (RepositoryBase, UnitOfWork, SQL Server transactions, rowversion, unique indexes).
/// </summary>
public sealed class FxCopyDbFactAttribute : FactAttribute
{
    public const string EnvVar = "FINCORE_FXCOPY_TESTS_CONNECTION";

    public FxCopyDbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar)))
            Skip = $"Set {EnvVar} to a FinCoreDb_fxcopy connection string to run SQL Server concurrency tests.";
    }
}

/// <summary>Runs a callback once, right before the first SaveChanges of the context it is attached to.</summary>
internal sealed class BeforeFirstSaveInterceptor(Func<Task> callback) : SaveChangesInterceptor
{
    private bool _fired;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!_fired)
        {
            _fired = true;
            await callback();
        }
        return result;
    }
}

[Trait("Category", "FxCopyDb")]
public class FxCopyDbConcurrencyTests
{
    private static string ConnectionString
    {
        get
        {
            var cs = Environment.GetEnvironmentVariable(FxCopyDbFactAttribute.EnvVar)!;
            var db = new SqlConnectionStringBuilder(cs).InitialCatalog;
            if (db != "FinCoreDb_fxcopy")
                throw new InvalidOperationException($"Refusing to run against '{db}': only FinCoreDb_fxcopy is allowed.");
            return cs;
        }
    }

    private static ApplicationDbContext NewContext(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new ApplicationDbContext(builder.Options);
    }

    private static SimulatedPaymentService NewService(ApplicationDbContext ctx) => new(
        new RepositoryBase<SimulatedPayment>(ctx),
        new RepositoryBase<VirtualCard>(ctx),
        new RepositoryBase<BankAccount>(ctx),
        new RepositoryBase<LedgerAccount>(ctx),
        new RepositoryBase<LedgerTransaction>(ctx),
        new UnitOfWork(ctx),
        new Mock<IAuditService>().Object,
        new FxPricingService(new FakeNbgRates(), Options.Create(new ConversionOptions())));

    private sealed record Fixture(string UserId, int CardId, int MainGelId, int SecondGelId, int UsdId);

    /// <summary>A fresh user with funded accounts (through real deposit ledger transactions) and an auto-conversion card.</summary>
    private static async Task<Fixture> SeedAsync(decimal dailyLimit, decimal transactionLimit)
    {
        await using var ctx = NewContext();
        var tag = Guid.NewGuid().ToString("N");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = $"fxtest-{tag}@test.local",
            NormalizedUserName = $"FXTEST-{tag}@TEST.LOCAL",
            Email = $"fxtest-{tag}@test.local",
            NormalizedEmail = $"FXTEST-{tag}@TEST.LOCAL",
            SecurityStamp = Guid.NewGuid().ToString(),
            PersonalNumber = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(),
            FirstName = "Fx",
            LastName = "Test"
        };
        ctx.Users.Add(user);

        BankAccount Open(CurrencyCode c, int n) => BankAccount.Create($"FXT{tag[..14]}{n}", user.Id, c);
        var gel = Open(CurrencyCode.GEL, 1);
        var gel2 = Open(CurrencyCode.GEL, 2);
        var usd = Open(CurrencyCode.USD, 3);
        ctx.BankAccounts.AddRange(gel, gel2, usd);
        await ctx.SaveChangesAsync();

        foreach (var a in new[] { gel, gel2, usd })
        {
            var cash = await ctx.LedgerAccounts.SingleAsync(x => x.AccountCode == LedgerAccountCodes.Cash(a.Currency));
            ctx.LedgerTransactions.Add(LedgerTransaction.CreateDeposit($"DEP-FXT-{Guid.NewGuid():N}", a, cash, 1_000m));
            a.Credit(1_000m);
        }

        var card = VirtualCard.Create(user.Id, gel, "FX test card", "0001", dailyLimit, transactionLimit, DateTime.UtcNow, allowAutoConversion: true);
        ctx.VirtualCards.Add(card);
        await ctx.SaveChangesAsync();

        return new Fixture(user.Id, card.Id, gel.Id, gel2.Id, usd.Id);
    }

    private static CreatePaymentDto Pay(Fixture f, decimal amount, CurrencyCode currency, int source, string key) => new()
    {
        VirtualCardId = f.CardId, Amount = amount, Currency = currency, SourceAccountId = source,
        Merchant = "FX test shop", IdempotencyKey = key
    };

    private static async Task<decimal> BalanceAsync(int accountId)
    {
        await using var ctx = NewContext();
        return await ctx.BankAccounts.Where(a => a.Id == accountId).Select(a => a.Balance).SingleAsync();
    }

    private static async Task<List<SimulatedPayment>> PaymentsAsync(Fixture f)
    {
        await using var ctx = NewContext();
        return await ctx.SimulatedPayments.AsNoTracking().Where(p => p.VirtualCardId == f.CardId).ToListAsync();
    }

    private static async Task<List<LedgerEntry>> LedgerEntriesAsync(string paymentReference)
    {
        await using var ctx = NewContext();
        return await ctx.LedgerEntries.AsNoTracking()
            .Where(e => e.LedgerTransaction.Reference == paymentReference || e.LedgerTransaction.Reference.StartsWith(paymentReference + "-"))
            .ToListAsync();
    }

    [FxCopyDbFact]
    public async Task TwoPaymentsOnOneCard_OnlyOneFitsTheDailyLimit_ExactlyOneCompletes()
    {
        // Different source accounts: the ONLY row both payments update is the card (RowVersion).
        var f = await SeedAsync(dailyLimit: 100m, transactionLimit: 100m);
        PaymentDto? b = null;

        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await using var ctxB = NewContext();
            b = await NewService(ctxB).PayAsync(f.UserId, Pay(f, 80m, CurrencyCode.GEL, f.SecondGelId, "race-b"));
        });

        await using var ctxA = NewContext(interceptor);
        var a = await NewService(ctxA).PayAsync(f.UserId, Pay(f, 80m, CurrencyCode.GEL, f.MainGelId, "race-a"));

        b!.Status.Should().Be(SimulatedPaymentStatus.Completed);
        a.Status.Should().Be(SimulatedPaymentStatus.Failed);
        a.FailureReason.Should().Be(PaymentDeclineReason.DailyLimitExceeded); // A retried after the card conflict

        var stored = await PaymentsAsync(f);
        stored.Should().HaveCount(2);
        stored.Count(p => p.Status == SimulatedPaymentStatus.Completed).Should().Be(1);
        stored.Count(p => p.Status == SimulatedPaymentStatus.Failed).Should().Be(1);

        (await BalanceAsync(f.SecondGelId)).Should().Be(920m);
        (await BalanceAsync(f.MainGelId)).Should().Be(1_000m);
        (await LedgerEntriesAsync(a.Reference)).Should().BeEmpty();
    }

    [FxCopyDbFact]
    public async Task ConflictRetry_NeverDebitsTwice_NeverWritesLedgerTwice()
    {
        var f = await SeedAsync(dailyLimit: 1_000m, transactionLimit: 500m);

        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await using var ctxB = NewContext();
            await NewService(ctxB).PayAsync(f.UserId, Pay(f, 10m, CurrencyCode.GEL, f.SecondGelId, "other"));
        });

        // A converts: 100 USD charged from the GEL main account (271.37 GEL incl. 1.36 fee)
        await using var ctxA = NewContext(interceptor);
        var a = await NewService(ctxA).PayAsync(f.UserId, Pay(f, 100m, CurrencyCode.USD, f.MainGelId, "conv-a"));

        a.Status.Should().Be(SimulatedPaymentStatus.Completed);
        a.ChargedAmount.Should().Be(271.37m);
        (await BalanceAsync(f.MainGelId)).Should().Be(1_000m - 271.37m);

        var paymentsA = (await PaymentsAsync(f)).Where(p => p.IdempotencyKey == "conv-a").ToList();
        paymentsA.Should().ContainSingle();

        var entries = await LedgerEntriesAsync(a.Reference);
        entries.Where(e => e.BankAccountId == f.MainGelId).Should().ContainSingle().Which.Amount.Should().Be(271.37m);
        entries.Should().HaveCount(5); // -OUT: customer, FX-GEL, FEE-GEL; -IN: FX-USD, CARDUSD
        foreach (var g in entries.GroupBy(e => (e.LedgerTransactionId, e.Currency)))
            g.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount)
             .Should().Be(g.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount));
    }

    [FxCopyDbFact]
    public async Task SameKeyRacingTwice_SecondGetsTheStoredResult_DebitedOnce()
    {
        var f = await SeedAsync(dailyLimit: 1_000m, transactionLimit: 500m);
        PaymentDto? b = null;

        var interceptor = new BeforeFirstSaveInterceptor(async () =>
        {
            await using var ctxB = NewContext();
            b = await NewService(ctxB).PayAsync(f.UserId, Pay(f, 50m, CurrencyCode.USD, f.UsdId, "same-key"));
        });

        await using var ctxA = NewContext(interceptor);
        var a = await NewService(ctxA).PayAsync(f.UserId, Pay(f, 50m, CurrencyCode.USD, f.UsdId, "same-key"));

        a.Id.Should().Be(b!.Id);
        a.Reference.Should().Be(b.Reference);
        (await PaymentsAsync(f)).Should().ContainSingle();
        (await BalanceAsync(f.UsdId)).Should().Be(950m);
        (await LedgerEntriesAsync(a.Reference)).Should().HaveCount(2);
    }

    [FxCopyDbFact]
    public async Task UniqueIndexViolation_OnCardAndKey_IsTranslatedToDuplicateKey()
    {
        // Proves the replay path is reachable: the (card, idempotency key) index raises 2601,
        // which UnitOfWork maps to DuplicateKeyException (the exception the retry loop turns into a replay).
        var f = await SeedAsync(dailyLimit: 1_000m, transactionLimit: 500m);
        await using (var ctx0 = NewContext())
            await NewService(ctx0).PayAsync(f.UserId, Pay(f, 5m, CurrencyCode.USD, f.UsdId, "dup"));

        await using var ctx = NewContext();
        var card = await ctx.VirtualCards.SingleAsync(c => c.Id == f.CardId);
        var usd = await ctx.BankAccounts.SingleAsync(x => x.Id == f.UsdId);
        var p = SimulatedPayment.Create(card, usd, CurrencyCode.USD, 5m, "FX test shop", null, "dup", DateTime.UtcNow);
        p.Fail(PaymentDeclineReason.CardNotActive);
        ctx.SimulatedPayments.Add(p);

        await new UnitOfWork(ctx).Invoking(u => u.SaveChangesAsync())
            .Should().ThrowAsync<FinCore.Application.Exceptions.DuplicateKeyException>();
    }
}
