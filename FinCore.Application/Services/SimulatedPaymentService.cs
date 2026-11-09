using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.Common;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Mapster;
using Org.BouncyCastle.Asn1.Cms;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class SimulatedPaymentService : ISimulatedPaymentService
    {
        private readonly IRepositoryBase<SimulatedPayment> _payments;
        private readonly IRepositoryBase<VirtualCard> _cards;
        private readonly IRepositoryBase<BankAccount> _accounts;
        private readonly IRepositoryBase<LedgerAccount> _ledgerAccounts;
        private readonly IRepositoryBase<LedgerTransaction> _ledgerTransactions;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;


        public SimulatedPaymentService(
            IRepositoryBase<SimulatedPayment> payments,
            IRepositoryBase<VirtualCard> cards,
            IRepositoryBase<BankAccount> accounts,
            IRepositoryBase<LedgerAccount> ledgerAccounts,
            IRepositoryBase<LedgerTransaction> ledgerTransactions,
            IUnitOfWork unitOfWork,
            IAuditService audit)
        {
            _payments = payments;
            _cards = cards;
            _accounts = accounts;
            _ledgerAccounts = ledgerAccounts;
            _ledgerTransactions = ledgerTransactions;
            _unitOfWork = unitOfWork;
            _audit = audit;
        }

        public async Task<PaymentDto> PayAsync(string userId, CreatePaymentDto model,
            CancellationToken ct = default)
        {
            
            try
            {
                await _unitOfWork.BeginTransactionAsync(ct: ct);

                var card = await _cards.GetAsync(
                    x => x.Id == model.VirtualCardId
                    && x.UserId == userId,
                    cancellationToken: ct
                    ) ?? throw new NotFoundException("Virtual card not found");

                // idempotency key check
                var existing = await _payments.GetAsync(
                    x => x.VirtualCardId == model.VirtualCardId
                    && x.IdempotencyKey == model.IdempotencyKey.Trim(),
                    tracking: false,
                    cancellationToken: ct
                    );

                if(existing is not null)
                {
                    if(existing.Amount != model.Amount ||
                        existing.Merchant != model.Merchant.Trim() ||
                        existing.Description != (string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim())
                        )
                    {
                        throw new ConflictException("Idempotency Key already used for a differenc payment");
                    }

                    await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);

                    var dto = existing.Adapt<PaymentDto>();
                    dto.Last4Digits = card.Last4Digits;

                    return dto;
                }

                var account = await _accounts.GetAsync(
                    x => x.Id == card.BankAccountId,
                    cancellationToken: ct
                    ) ?? throw new NotFoundException("Bank account not found");

                var now = DateTime.UtcNow;
                var dayStart = now.Date;

                var (todayPayments, _) = await _payments.GetAllAsync(
                    filter: x => x.VirtualCardId == card.Id
                    && x.Status == SimulatedPaymentStatus.Completed
                    && x.CreatedAt >= dayStart,
                    tracking: false,
                    cancellationToken: ct
                    );

                var spentToday = todayPayments.Sum(x => x.Amount);


                var declineReason = card.Authorize(
                    account,
                    model.Amount,
                    account.Currency,
                    spentToday,
                    now
                    );

                var payment = SimulatedPayment.Create(
                    card,
                    account.Currency,
                    model.Amount,
                    model.Merchant,
                    model.Description,
                    model.IdempotencyKey,
                    now
                    );

                if(declineReason is null)
                {
                    account.Debit(model.Amount);

                    var settlementAccount = await GetLedgerAccountAsync(
                        LedgerAccountCodes.CardSettlement(account.Currency),
                        account.Currency,
                        ct
                        );

                    var transaction = LedgerTransaction.Create(
                        payment.Reference,
                        account.Currency,

                        new[]
                        {
                            LedgerLine.ForCustomer(account.Id,EntryType.Debit,model.Amount),

                            LedgerLine.ForSystem(settlementAccount.Id,EntryType.Credit,model.Amount)
                        },
                        LedgerTransactionType.CardPayment


                        );

                    payment.Complete();

                    await _ledgerTransactions.AddAsync(transaction,ct);
                }
                else
                {
                    payment.Fail(declineReason.Value);
                }

                await _payments.AddAsync(payment,ct);

                await _audit.RecordAsync(
                    userId,
                    declineReason is null ?
                    AuditActions.CardPaymentCompleted :
                    AuditActions.CardPaymentDeclined,
                    AuditEntities.VirtualCard,
                    card.Id.ToString(),
                    $"amount={model.Amount} {account.Currency}; " +
                    $"merchant={payment.Merchant}; reason={declineReason}",
                    ct
                    );

                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitTransactionAsync(ct);

                var paymentDto = payment.Adapt<PaymentDto>();
                paymentDto.Last4Digits = card.Last4Digits;

                return paymentDto;

            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                throw;
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
