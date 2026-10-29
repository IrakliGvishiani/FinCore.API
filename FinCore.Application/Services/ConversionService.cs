using CloudinaryDotNet;
using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class ConversionService : IConversionService
    {
        private readonly IExchangeRateProvider _rates;
        private readonly IUnitOfWork _uow;
        private readonly IRepositoryBase<CurrencyConversion> _conversions;
        private readonly IRepositoryBase<BankAccount> _accounts;
        private readonly IRepositoryBase<LedgerTransaction> _ledgerTransactions;
        private readonly IRepositoryBase<LedgerAccount> _ledgerAccounts;
        private readonly IOptions<ConversionOptions> _options;
        private readonly IAuditService _audit;


        public ConversionService(
            IExchangeRateProvider exchangeRateProvider,
            IUnitOfWork unitOfWork,
            IRepositoryBase<CurrencyConversion> conversions,
            IRepositoryBase<BankAccount> accounts,
            IRepositoryBase<LedgerTransaction> ledgerTransactions,
            IRepositoryBase<LedgerAccount> ledgerAccounts,
            IOptions<ConversionOptions> options,
            IAuditService auditService
            )
        {
            _rates = exchangeRateProvider;
            _uow = unitOfWork;
            _conversions = conversions;
            _accounts = accounts;
            _ledgerTransactions = ledgerTransactions;
            _ledgerAccounts = ledgerAccounts;
            _options = options;
            _audit = auditService;
        }


        public async Task<ConversionDto> ConvertAsync(string userId, 
            string fromNo, string toNo, decimal amount,
            string idempotencyKey,
            decimal? expectedRate = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
                throw new BadRequestException("Idempotency key is required (max 100 chars).");

            idempotencyKey = idempotencyKey.Trim();

            const int maxAttempts = 3;
            for (var attempt = 1; ;attempt++)
            {
                try
                {
                    return await TryConvertAsync(userId,fromNo,toNo,amount,idempotencyKey,expectedRate,ct);
                }
                catch(Exception ex) when ((ex is ConcurrencyConflictException or DuplicateKeyException) && attempt < maxAttempts)
                {
                    await _uow.ClearTracking();
                    await Task.Delay(Random.Shared.Next(20, 80) * attempt, ct);
                }
            }
        }
        public async Task<ConversionQuoteDto> QuoteAsync(string userId,
            string fromNo, string toNo,
            decimal amount, CancellationToken ct = default)
        {
            var from = await _accounts.GetAsync(
                a => a.AccountNumber == fromNo && a.UserId == userId, tracking: false, cancellationToken: ct)
                ?? throw new NotFoundException("Source account not found.");

            var to = await _accounts.GetAsync(
                a => a.AccountNumber == toNo && a.UserId == userId, tracking: false, cancellationToken: ct)
                ?? throw new NotFoundException("Destination account not found.");


            var rate = await _rates.GetRateAsync(from.Currency, to.Currency, ct);

            var preview = CurrencyConversion.Create(
             userId, from, to, amount, rate.Rate, rate.RateDate,_options.Value.FeeRate, "quote");

            return ToQuoteDto(preview, fromNo, toNo);
        }

        public async Task<(IEnumerable<ConversionDto> Items,int TotalCount)> GetMyConversionsAsync(
            string userId,
            ConversionFilter filter,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken ct = default
            )
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException("User ID is required.");
            if (pageNumber < 1)
                throw new BadRequestException("Page number must be greater than zero.");
            if (pageSize < 1 || pageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");
            if (filter.FromDate > filter.ToDate)
                throw new BadRequestException("FromDate cannot be after ToDate.");

            var fromDate = filter.FromDate?.Date;
            var toExclusive = filter.ToDate?.Date.AddDays(1);
            var fromCur = filter.FromCurrency;
            var toCur = filter.ToCurrency;

            var (items, total) = await _conversions.GetAllAsync(
                filter: c => c.InitiatedByUserId == userId
                && (fromDate == null || c.CreatedAt >= fromDate)
                && (toExclusive == null || c.CreatedAt < toExclusive)
                && (fromCur == null || c.FromCurrency == fromCur)
                && (toCur == null || c.ToCurrency == toCur),
                pageNumber,
                pageSize,
                orderBy: c => c.CreatedAt,
                ascending: false,
                tracking: false,
                cancellationToken: ct,
                includes: q => q.Include(c => c.FromAccount).Include(c => c.ToAccount)
                );

            return (items.Select(
                c => ToDto(c,c.FromAccount.AccountNumber,c.ToAccount.AccountNumber)
                ).ToList(), total);
        }


        private async Task<ConversionDto> TryConvertAsync(
            string userId,string fromNo,string toNo,decimal amount,
            string key,decimal? expectedRate,CancellationToken ct
            )
        {
            var fromProbe = await _accounts.GetAsync(
               a => a.AccountNumber == fromNo && a.UserId == userId, tracking: false, cancellationToken: ct)
               ?? throw new NotFoundException("Source account not found.");


            var toProbe = await _accounts.GetAsync(
                a => a.AccountNumber == toNo && a.UserId == userId, tracking: false, cancellationToken: ct)
                ?? throw new NotFoundException("Destination account not found.");

            var rate = await _rates.GetRateAsync(fromProbe.Currency, toProbe.Currency, ct);

            if (expectedRate is not null && expectedRate != rate.Rate)
                throw new ConflictException("The exchange rate has changed. Please confirm the new rate.");

            try
            {
                await _uow.BeginTransactionAsync(ct);

                var existing = await _conversions.GetAsync(
                    x => x.InitiatedByUserId == userId && x.IdempotencyKey == key,
                    tracking: false,
                    cancellationToken: ct
                    );

                if (existing is not null)
                {
                    if (existing.FromAccountId == fromProbe.Id && existing.ToAccountId == toProbe.Id
                        && existing.FromAmount == amount)
                    {
                        await _uow.RollbackTransactionAsync(CancellationToken.None);
                        return ToDto(existing, fromNo, toNo);
                    }
                    throw new ConflictException("Idempotency key already used for a different conversion.");
                }
                var from = await _accounts.GetAsync(a => a.Id == fromProbe.Id, cancellationToken: ct)
                    ?? throw new NotFoundException("Source Account not found");
                var to = await _accounts.GetAsync(a => a.Id == toProbe.Id, cancellationToken: ct)
                    ?? throw new NotFoundException("Destination Account not found");

                var fxFrom = await GetFxAccountAsync(from.Currency,ct);

                var fxTo = await GetFxAccountAsync(to.Currency,ct);

                var feeAccount = await GetLedgerAccountAsync(LedgerAccountCodes.Fee(from.Currency),ct);

                var conversion = CurrencyConversion.Create(
                    userId,
                    from,
                    to,
                    amount,
                    rate.Rate,
                    rate.RateDate,
                    _options.Value.FeeRate,
                    key
                    );

                

                await _conversions.AddAsync(conversion, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var tx in conversion.Execute(from!,to!,fxFrom,fxTo,feeAccount))
                    await _ledgerTransactions.AddAsync(tx, ct);

                await _audit.RecordAsync(userId,AuditActions.ConversionCompleted,AuditEntities.Conversion,
                    conversion.Reference,
                    $"fromAccount={from.AccountNumber}; toAccount={to.AccountNumber}; fromAmount={conversion.FromAmount} {conversion.FromCurrency};" +
                    $"toAmount={conversion.ToAmount} {conversion.ToCurrency}",ct);

                await _uow.SaveChangesAsync(ct);                 
                await _uow.CommitTransactionAsync(ct);

                return ToDto(conversion, fromNo, toNo);
            }
            catch
            {
                await _uow.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }
        private static ConversionDto ToDto(CurrencyConversion c, string fromNumber, string toNumber) => new()
        {
            Id = c.Id,
            FromAccountNumber = fromNumber,
            ToAccountNumber = toNumber,
            FromAmount = c.FromAmount,
            FromCurrency = c.FromCurrency,
            ToAmount = c.ToAmount,
            ToCurrency = c.ToCurrency,
            FeeRate = c.FeeRate,
            FeeAmount = c.FeeAmount,
            NetAmount = c.NetAmount,
            Rate = c.Rate,
            RateDate = c.RateDate,
            CreatedAt = c.CreatedAt
        };

        private static ConversionQuoteDto ToQuoteDto(CurrencyConversion c, string fromNumber, string toNumber) => new()
        {
            FromAccountNumber = fromNumber,
            ToAccountNumber = toNumber,
            FromAmount = c.FromAmount,
            FromCurrency = c.FromCurrency,
            ToAmount = c.ToAmount,
            ToCurrency = c.ToCurrency,
            FeeRate = c.FeeRate,
            FeeAmount = c.FeeAmount,
            NetAmount = c.NetAmount,
            Rate = c.Rate,
            RateDate = c.RateDate
        };

        private async Task<LedgerAccount> GetFxAccountAsync(CurrencyCode currency, CancellationToken ct)
        {
            var code = LedgerAccountCodes.Fx(currency);

            return await _ledgerAccounts.GetAsync(
                x => x.AccountCode == code,
                tracking: false,
                cancellationToken: ct)
                ?? throw new InvalidOperationException($"FX ledger account for {currency} is not configured.");
        }

        private async Task<LedgerAccount> GetLedgerAccountAsync(string code, CancellationToken ct)
           => await _ledgerAccounts.GetAsync(
           x => x.AccountCode == code, tracking: false, cancellationToken: ct)
       ?? throw new InvalidOperationException($"Ledger account {code} is not configured.");
    }
}
