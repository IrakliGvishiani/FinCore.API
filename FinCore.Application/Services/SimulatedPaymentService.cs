using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.Common;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Mapster;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    /// <summary>
    /// Card payments, optionally funded from another account of the card owner with automatic FX conversion.
    ///
    /// Fixed order of checks in <see cref="PayAsync"/>:
    ///   A. Errors that store nothing
    ///      1. request validation ............................................. 400
    ///      2. card not found / not owned ...................................... 404
    ///      3. idempotency: same key + same request -> stored result (replay, incl. stored declines);
    ///                      same key + different request -> 409
    ///      4. source account not found / not owned by the card owner .......... 404
    ///   B. Declines without a rate (stored, pricing fields stay null)
    ///      5. CardNotActive  6. CardExpired  7. SourceAccountNotAllowed
    ///      8. AutoConversionNotAllowed  9. AccountNotActive (source)
    ///   C. Pricing (stores nothing on failure)
    ///     10. NBG rates, only when a conversion or limit conversion is needed . 503 if unavailable
    ///     11. expectedChargedAmount given and different ......................... 409
    ///   D. Declines on amounts (stored, priced)
    ///     12. TransactionLimitExceeded  13. DailyLimitExceeded  (on LimitAmount, main-account currency)
    ///     14. InsufficientFunds (on ChargedAmount, source currency)
    ///
    /// Concurrency: every authorization touches the card row (RowVersion), and the source account has its own
    /// RowVersion. A concurrency conflict or a duplicate (card, idempotency key) rolls the whole attempt back
    /// and retries from scratch (max 3 attempts); a retry after a duplicate key replays the stored payment.
    /// </summary>
    public class SimulatedPaymentService : ISimulatedPaymentService
    {
        private const int MaxAttempts = 3;

        private readonly IRepositoryBase<SimulatedPayment> _payments;
        private readonly IRepositoryBase<VirtualCard> _cards;
        private readonly IRepositoryBase<BankAccount> _accounts;
        private readonly IRepositoryBase<LedgerAccount> _ledgerAccounts;
        private readonly IRepositoryBase<LedgerTransaction> _ledgerTransactions;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;
        private readonly IFxPricingService _fx;


        public SimulatedPaymentService(
            IRepositoryBase<SimulatedPayment> payments,
            IRepositoryBase<VirtualCard> cards,
            IRepositoryBase<BankAccount> accounts,
            IRepositoryBase<LedgerAccount> ledgerAccounts,
            IRepositoryBase<LedgerTransaction> ledgerTransactions,
            IUnitOfWork unitOfWork,
            IAuditService audit,
            IFxPricingService fx)
        {
            _payments = payments;
            _cards = cards;
            _accounts = accounts;
            _ledgerAccounts = ledgerAccounts;
            _ledgerTransactions = ledgerTransactions;
            _unitOfWork = unitOfWork;
            _audit = audit;
            _fx = fx;
        }

        public async Task<PaymentDto> PayAsync(string userId, CreatePaymentDto model,
            CancellationToken ct = default)
        {
            var request = PaymentRequest.From(model);

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await TryPayAsync(userId, request, ct);
                }
                catch (Exception ex) when ((ex is ConcurrencyConflictException or DuplicateKeyException) && attempt < MaxAttempts)
                {
                    // The attempt was rolled back entirely (no debit, no ledger rows, no payment row).
                    // Drop tracked entities so the next attempt reloads balances, the card and the stored payments.
                    await _unitOfWork.ClearTracking();
                    await Task.Delay(Random.Shared.Next(20, 80) * attempt, ct);
                }
            }
        }

        private async Task<PaymentDto> TryPayAsync(string userId, PaymentRequest request, CancellationToken ct)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(ct: ct);

                // 2. card
                var card = await _cards.GetAsync(
                    x => x.Id == request.VirtualCardId
                    && x.UserId == userId,
                    cancellationToken: ct
                    ) ?? throw new NotFoundException("Virtual card not found");

                var mainAccount = await _accounts.GetAsync(
                    x => x.Id == card.BankAccountId,
                    cancellationToken: ct
                    ) ?? throw new NotFoundException("Bank account not found");

                var currency = request.Currency ?? mainAccount.Currency;
                var sourceAccountId = request.SourceAccountId ?? card.BankAccountId;

                // 3. idempotency
                var existing = await _payments.GetAsync(
                    x => x.VirtualCardId == card.Id
                    && x.IdempotencyKey == request.IdempotencyKey,
                    tracking: false,
                    cancellationToken: ct
                    );

                if (existing is not null)
                {
                    if (existing.Amount != request.Amount ||
                        existing.Currency != currency ||
                        existing.SourceBankAccountId != sourceAccountId ||
                        existing.Merchant != request.Merchant ||
                        existing.Description != request.Description)
                    {
                        throw new ConflictException("Idempotency Key already used for a differenc payment");
                    }

                    await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);

                    return ToDto(existing, card.Last4Digits);
                }

                // 4. source account (must belong to the card owner)
                var source = sourceAccountId == mainAccount.Id
                    ? mainAccount
                    : await _accounts.GetAsync(
                        x => x.Id == sourceAccountId && x.UserId == card.UserId,
                        cancellationToken: ct
                        ) ?? throw new NotFoundException("Source account not found.");

                var now = DateTime.UtcNow;

                var payment = SimulatedPayment.Create(
                    card,
                    source,
                    currency,
                    request.Amount,
                    request.Merchant,
                    request.Description,
                    request.IdempotencyKey,
                    now
                    );

                // Touch the card row: concurrent authorizations on this card now conflict on its RowVersion.
                card.RegisterAuthorization(now);

                // 5-9. status checks
                var declineReason = card.AuthorizeSource(source, currency, mainAccount.Currency, now);

                FxChargeQuote? charge = null;
                FxLimitAmount? limit = null;

                if (declineReason is null)
                {
                    // 10. pricing (may throw ExternalServiceException -> 503, nothing stored)
                    charge = await _fx.QuoteChargeAsync(source.Currency, currency, request.Amount, ct);
                    limit = await _fx.ToLimitAmountAsync(request.Amount, currency, mainAccount.Currency, ct);

                    // 11. rate protection
                    if (request.ExpectedChargedAmount is not null && request.ExpectedChargedAmount != charge.ChargedAmount)
                    {
                        throw new ConflictException(
                            $"The charged amount has changed: it is now {charge.ChargedAmount:0.00} {charge.SourceCurrency} " +
                            $"(expected {request.ExpectedChargedAmount:0.00}). Please request a new quote.");
                    }

                    payment.ApplyPricing(
                        charge.ChargedAmount,
                        charge.Rate,
                        charge.FeeAmount,
                        limit.Amount,
                        charge.RateDate ?? limit.RateDate);

                    // 12-14. amount checks
                    var spentToday = await GetSpentTodayAsync(card.Id, now, ct);

                    declineReason = card.AuthorizeAmounts(source, limit.Amount, charge.ChargedAmount, spentToday);
                }

                if (declineReason is null)
                {
                    var settlementAccount = await GetLedgerAccountAsync(
                        LedgerAccountCodes.CardSettlement(currency), currency, ct);

                    IReadOnlyList<LedgerTransaction> transactions;

                    if (payment.IsConversion)
                    {
                        var fxSource = await GetLedgerAccountAsync(LedgerAccountCodes.Fx(source.Currency), source.Currency, ct);
                        var fxMerchant = await GetLedgerAccountAsync(LedgerAccountCodes.Fx(currency), currency, ct);
                        var feeAccount = await GetLedgerAccountAsync(LedgerAccountCodes.Fee(source.Currency), source.Currency, ct);

                        transactions = payment.Settle(source, settlementAccount, fxSource, fxMerchant, feeAccount);
                    }
                    else
                    {
                        transactions = payment.Settle(source, settlementAccount);
                    }

                    foreach (var transaction in transactions)
                        await _ledgerTransactions.AddAsync(transaction, ct);
                }
                else
                {
                    payment.Fail(declineReason.Value);
                }

                await _payments.AddAsync(payment, ct);

                await _audit.RecordAsync(
                    userId,
                    declineReason is null ?
                    AuditActions.CardPaymentCompleted :
                    AuditActions.CardPaymentDeclined,
                    AuditEntities.VirtualCard,
                    card.Id.ToString(),
                    AuditDetails(payment, source, mainAccount.Currency, declineReason),
                    ct
                    );

                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitTransactionAsync(ct);

                return ToDto(payment, card.Last4Digits);

            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task<PaymentQuoteDto> QuoteAsync(
            string userId, PaymentQuoteRequestDto model, CancellationToken ct = default)
        {
            // Read-only: no transaction, no tracking, no writes.
            if (model is null)
                throw new BadRequestException("Request body is required.");
            PaymentRequest.EnsureAmount(model.Amount);
            if (model.Currency is null || !Enum.IsDefined(model.Currency.Value))
                throw new BadRequestException("Currency is required (GEL, USD, EUR or GBP).");

            var currency = model.Currency.Value;
            var now = DateTime.UtcNow;

            var card = await _cards.GetAsync(
                x => x.Id == model.VirtualCardId && x.UserId == userId,
                tracking: false,
                cancellationToken: ct)
                ?? throw new NotFoundException("Virtual card not found");

            var mainAccount = await _accounts.GetAsync(
                x => x.Id == card.BankAccountId,
                tracking: false,
                cancellationToken: ct)
                ?? throw new NotFoundException("Bank account not found");

            List<BankAccount> candidates;
            if (card.AllowAutoConversion)
            {
                var (accounts, _) = await _accounts.GetAllAsync(
                    filter: x => x.UserId == card.UserId && x.Status == AccountStatus.Active,
                    tracking: false,
                    cancellationToken: ct);
                candidates = accounts.ToList();
            }
            else
            {
                candidates = new List<BankAccount> { mainAccount };
            }

            candidates = candidates
                .OrderByDescending(a => a.Id == mainAccount.Id)
                .ThenBy(a => a.Currency.ToString(), StringComparer.Ordinal)
                .ThenBy(a => a.Id)
                .ToList();

            var usable = candidates
                .Where(a => card.AuthorizeSource(a, currency, mainAccount.Currency, now) is null)
                .ToList();

            var spentToday = await GetSpentTodayAsync(card.Id, now, ct);

            var result = new PaymentQuoteDto
            {
                VirtualCardId = card.Id,
                MerchantAmount = model.Amount,
                MerchantCurrency = currency,
                AllowAutoConversion = card.AllowAutoConversion,
                MainAccountId = mainAccount.Id,
                LimitCurrency = mainAccount.Currency,
                TransactionLimit = card.TransactionLimit,
                DailyLimit = card.DailyLimit,
                SpentToday = spentToday
            };

            if (usable.Count == 0)
            {
                result.DeclineReason =
                    card.AuthorizeSource(mainAccount, currency, mainAccount.Currency, now)
                    ?? PaymentDeclineReason.AccountNotActive;
                return result;
            }

            var limit = await _fx.ToLimitAmountAsync(model.Amount, currency, mainAccount.Currency, ct);
            result.LimitAmount = limit.Amount;
            result.WithinTransactionLimit = limit.Amount <= card.TransactionLimit;
            result.WithinDailyLimit = spentToday + limit.Amount <= card.DailyLimit;

            foreach (var account in usable)
            {
                var charge = await _fx.QuoteChargeAsync(account.Currency, currency, model.Amount, ct);

                result.Accounts.Add(new PaymentQuoteAccountDto
                {
                    AccountId = account.Id,
                    AccountNumber = account.AccountNumber,
                    AccountName = account.Name,
                    Currency = account.Currency,
                    IsMainAccount = account.Id == mainAccount.Id,
                    ChargedAmount = charge.ChargedAmount,
                    ExchangeRate = charge.Rate,
                    FeeRate = charge.FeeRate,
                    FeeAmount = charge.FeeAmount,
                    RateDate = charge.RateDate,
                    IsConversion = charge.IsConversion,
                    HasSufficientBalance = charge.ChargedAmount <= account.Balance
                });
            }

            result.RecommendedAccountId = Recommend(result.Accounts, currency)?.AccountId;
            return result;
        }

        /// <summary>
        /// 1. same-currency account with enough balance (main first, then lowest id);
        /// 2. the main account if it has enough;
        /// 3. any other account with enough balance, by currency code, then id.
        /// </summary>
        internal static PaymentQuoteAccountDto? Recommend(IEnumerable<PaymentQuoteAccountDto> accounts, CurrencyCode merchantCurrency)
        {
            var sufficient = accounts.Where(a => a.HasSufficientBalance).ToList();

            return sufficient
                       .Where(a => a.Currency == merchantCurrency)
                       .OrderByDescending(a => a.IsMainAccount)
                       .ThenBy(a => a.AccountId)
                       .FirstOrDefault()
                   ?? sufficient.FirstOrDefault(a => a.IsMainAccount)
                   ?? sufficient
                       .OrderBy(a => a.Currency.ToString(), StringComparer.Ordinal)
                       .ThenBy(a => a.AccountId)
                       .FirstOrDefault();
        }

        private async Task<decimal> GetSpentTodayAsync(int cardId, DateTime utcNow, CancellationToken ct)
        {
            var dayStart = utcNow.Date;

            var (todayPayments, _) = await _payments.GetAllAsync(
                filter: x => x.VirtualCardId == cardId
                && x.Status == SimulatedPaymentStatus.Completed
                && x.CreatedAt >= dayStart,
                tracking: false,
                cancellationToken: ct
                );

            // LimitAmount is set on every completed payment (backfilled = Amount for older rows).
            return todayPayments.Sum(x => x.LimitAmount ?? x.Amount);
        }

        /// <summary>
        /// e.g. "merchantAmount=100.00 USD; charged=271.37 GEL; rate=0.37037037; fee=1.36 GEL; limitAmount=270.00 GEL;
        /// sourceAccount=GE00...; merchant=Demo Shop; reason=" (n/a when declined before pricing). Invariant culture.
        /// </summary>
        internal static string AuditDetails(
            SimulatedPayment payment, BankAccount source, CurrencyCode limitCurrency, PaymentDeclineReason? reason)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string Money(decimal? v, CurrencyCode c) => v is null ? "n/a" : $"{v.Value.ToString("0.00", inv)} {c}";

            return
                $"merchantAmount={Money(payment.Amount, payment.Currency)}; " +
                $"charged={Money(payment.ChargedAmount, payment.ChargedCurrency)}; " +
                $"rate={(payment.ExchangeRate is null ? "n/a" : payment.ExchangeRate.Value.ToString("0.########", inv))}; " +
                $"fee={Money(payment.FeeAmount, payment.ChargedCurrency)}; " +
                $"limitAmount={Money(payment.LimitAmount, limitCurrency)}; " +
                $"sourceAccount={source.AccountNumber}; " +
                $"merchant={payment.Merchant}; reason={reason}";
        }

        private static PaymentDto ToDto(SimulatedPayment payment, string last4Digits)
        {
            var dto = payment.Adapt<PaymentDto>();
            dto.Last4Digits = last4Digits;
            return dto;
        }

        /// <summary>Validated, normalized payment request.</summary>
        private sealed record PaymentRequest(
            int VirtualCardId, decimal Amount, string Merchant, string? Description, string IdempotencyKey,
            CurrencyCode? Currency, int? SourceAccountId, decimal? ExpectedChargedAmount)
        {
            public static PaymentRequest From(CreatePaymentDto? model)
            {
                if (model is null)
                    throw new BadRequestException("Request body is required.");

                EnsureAmount(model.Amount);

                if (string.IsNullOrWhiteSpace(model.Merchant) || model.Merchant.Trim().Length > 100)
                    throw new BadRequestException("Merchant is required (max 100 characters).");

                if (string.IsNullOrWhiteSpace(model.IdempotencyKey))
                    throw new BadRequestException("Idempotency key is required.");
                var key = model.IdempotencyKey.Trim();
                if (key.Length > 100)
                    throw new BadRequestException("Idempotency key is too long.");

                if (model.Currency is not null && !Enum.IsDefined(model.Currency.Value))
                    throw new BadRequestException("Currency must be GEL, USD, EUR or GBP.");

                if (model.ExpectedChargedAmount is not null)
                    EnsureAmount(model.ExpectedChargedAmount.Value, "Expected charged amount");

                return new PaymentRequest(
                    model.VirtualCardId,
                    model.Amount,
                    model.Merchant.Trim(),
                    string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                    key,
                    model.Currency,
                    model.SourceAccountId,
                    model.ExpectedChargedAmount);
            }

            public static void EnsureAmount(decimal amount, string name = "Amount")
            {
                if (amount <= 0 || decimal.Round(amount, 2) != amount)
                    throw new BadRequestException($"{name} must be positive with at most 2 decimals.");
            }
        }

        public async Task<PaymentDto> GetByIdAsync(
            string userId,
            int paymentId,
            CancellationToken ct = default)
        {
            var payment = await _payments.GetAsync(
                x => x.Id == paymentId,
                tracking: false,
                cancellationToken: ct)
                ?? throw new NotFoundException("Payment not found.");

            var card = await _cards.GetAsync(
                x => x.Id == payment.VirtualCardId
                  && x.UserId == userId,
                tracking: false,
                cancellationToken: ct)
                ?? throw new NotFoundException("Payment not found.");

            var dto = payment.Adapt<PaymentDto>();
            dto.Last4Digits = card.Last4Digits;

            return dto;
        }

        public async Task<PaginatedResult<PaymentDto>> GetAllAsync(
            string userId,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            var (cards, _) = await _cards.GetAllAsync(
                filter: x => x.UserId == userId,
                tracking: false,
                cancellationToken: ct);

            var cardList = cards.ToList();

            var cardIds = cardList
                .Select(x => x.Id)
                .ToList();

            var cardLast4Digits = cardList.ToDictionary(
                x => x.Id,
                x => x.Last4Digits);

            if (cardIds.Count == 0)
            {
                return new PaginatedResult<PaymentDto>
                {
                    Items = [],
                    TotalCount = 0,
                    PageNumber = Math.Max(1, pageNumber),
                    PageSize = Math.Clamp(pageSize, 1, 100)
                };
            }

            var (payments, totalCount) = await _payments.GetAllAsync(
                filter: x => cardIds.Contains(x.VirtualCardId),
                pageNumber: pageNumber,
                pageSize: pageSize,
                orderBy: x => x.CreatedAt,
                ascending: false,
                tracking: false,
                cancellationToken: ct);

            var items = payments.Select(payment =>
            {
                var dto = payment.Adapt<PaymentDto>();
                dto.Last4Digits = cardLast4Digits[payment.VirtualCardId];

                return dto;
            }).ToList();

            return new PaginatedResult<PaymentDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = Math.Max(1, pageNumber),
                PageSize = Math.Clamp(pageSize, 1, 100)
            };
        }

        public async Task<PaginatedResult<PaymentDto>> GetByCardAsync(
            string userId,
            int cardId,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            var card = await _cards.GetAsync(
                x => x.Id == cardId && x.UserId == userId,
                tracking: false,
                cancellationToken: ct)
                ?? throw new NotFoundException("Virtual card not found.");

            var (payments, totalCount) = await _payments.GetAllAsync(
                filter: x => x.VirtualCardId == card.Id,
                pageNumber: pageNumber,
                pageSize: pageSize,
                orderBy: x => x.CreatedAt,
                ascending: false,
                tracking: false,
                cancellationToken: ct);

            var items = payments.Select(payment =>
            {
                var dto = payment.Adapt<PaymentDto>();
                dto.Last4Digits = card.Last4Digits;

                return dto;
            }).ToList();

            return new PaginatedResult<PaymentDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = Math.Max(1, pageNumber),
                PageSize = Math.Clamp(pageSize, 1, 100)
            };
        }
        private async Task<LedgerAccount> GetLedgerAccountAsync(
            string accountCode,
            CurrencyCode currency,
            CancellationToken ct
            )
        {
            var account = await _ledgerAccounts.GetAsync(
                x => x.AccountCode == accountCode
                && x.Currency == currency
                && x.IsActive,
                tracking: false,
                cancellationToken: ct
                );

            return account ??
                throw new BadRequestException($"Ledger account '{accountCode}' for {currency} was not found.");
        }
    }
}
