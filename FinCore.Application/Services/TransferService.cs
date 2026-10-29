using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.TransferDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace FinCore.Application.Services
{
    public class TransferService : ITransferService
    {

        private readonly IRepositoryBase<BankAccount> _accountRepository;
        private readonly IRepositoryBase<Transfer> _transferRepository;
        private readonly IRepositoryBase<LedgerTransaction> _ledgerRepository;
        private readonly IRepositoryBase<ApplicationUser> _userRepository;
        private readonly IRedisService _redisService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;

        public TransferService(IRepositoryBase<BankAccount> accountRepository,
            IRepositoryBase<Transfer> transferRepository,
            IRepositoryBase<LedgerTransaction> ledgerRepository,
            IUnitOfWork unitOfWork,
            IRepositoryBase<ApplicationUser> userRepository,
            IRedisService redisService,
            IAuditService auditService)
        {
            _accountRepository = accountRepository;
            _transferRepository = transferRepository;
            _ledgerRepository = ledgerRepository;
            _unitOfWork = unitOfWork;
            _userRepository = userRepository;
            _redisService = redisService;
            _audit = auditService;
        }

        private static TransferDto ToDto(Transfer t, string fromNumber, string toNumber) => new()
        {
            Id = t.Id,
            FromAccountNumber = fromNumber,
            ToAccountNumber = toNumber,
            Amount = t.Amount,
            Currency = t.Currency,
            Status = t.Status,
            Description = t.Description,
            CreatedAt = t.CreatedAt,
            CompletedAt = t.CompletedAt,
            FailureReason = t.FailureReason
        };

        private static MyTransferDto ToMyDto(Transfer t, HashSet<int> myAccountIds)
        {
            var outgoing = myAccountIds.Contains(t.FromAccountId);

            return new MyTransferDto
            {
                Id = t.Id,
                Direction = outgoing ? TransferDirection.Outgoing : TransferDirection.Incoming,
                MyAccountNumber = outgoing ? t.FromAccount.AccountNumber : t.ToAccount.AccountNumber,
                CounterPartyAccountNumber = outgoing ? t.ToAccount.AccountNumber : t.FromAccount.AccountNumber,
                Amount = t.Amount,
                Currency = t.Currency,
                Status = t.Status,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            };
        }
        private async Task<TransferDto> ExecuteWithRetryAsync(
    string userId, string fromAccountNumber, int toAccountId,
    decimal amount, string idempotencyKey, string? description, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
                throw new BadRequestException("Idempotency key is required (max 100 chars).");

            description = NormalizeDescription(description);

            const int maxAttempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await TryExecuteAsync(
                        userId, fromAccountNumber, toAccountId, amount, idempotencyKey, description, ct);
                }
                catch (Exception ex) when (
                    (ex is ConcurrencyConflictException or DuplicateKeyException) && attempt < maxAttempts)
                {
                    await _unitOfWork.ClearTracking();
                    await Task.Delay(Random.Shared.Next(20, 80) * attempt, ct);
                }
            }
        }
        public Task<TransferDto> ExecuteTransferByRecipientAccountIdAsync(
        string userId, string fromAccountNumber, int toAccountId,
        decimal amount, string idempotencyKey, string? description = null,
        CancellationToken ct = default)
          => ExecuteWithRetryAsync(
        userId, fromAccountNumber, toAccountId, amount, idempotencyKey, description, ct);

        public async Task<TransferDto> ExecuteTransferByAccountNumberAsync(
            string userId, string fromAccountNumber, string toAccountNumber,
            decimal amount, string idempotencyKey, string? description = null,
            CancellationToken ct = default)
        {
            var to = await _accountRepository.GetAsync(
                x => x.AccountNumber == toAccountNumber, tracking: false, cancellationToken: ct)
                ?? throw new NotFoundException("Destination account not found.");

            return await ExecuteWithRetryAsync(
                userId, fromAccountNumber, to.Id, amount, idempotencyKey, description, ct);
        }
        private static string? NormalizeDescription(string? d)
         => string.IsNullOrWhiteSpace(d) ? null : d.Trim();



        public async Task<(IEnumerable<MyTransferDto> Items,int TotalCount)> GetMyTransfersAsync(
            string userId,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken ct = default
            )
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException("user Id is required");

            if (pageNumber < 1)
                throw new BadRequestException(
                    "Page number must be greater than zero.");

            if (pageSize < 1 || pageSize > 100)
                throw new BadRequestException(
                    "Page size must be between 1 and 100.");

            var accounts = await _accountRepository.GetAllAsync(
                filter: x => x.UserId == userId,
                tracking: false,
                cancellationToken: ct
                );

            var accountIds = accounts.Items
                .Select(x => x.Id)
                .ToList();

            if (accountIds.Count == 0)
                return (Enumerable.Empty<MyTransferDto>(), 0);

            var myIds = accountIds.ToHashSet();

            var result = await _transferRepository.GetAllAsync(
                filter: x =>
                accountIds.Contains(x.FromAccountId) ||
                accountIds.Contains(x.ToAccountId),
                pageNumber: pageNumber,
                pageSize: pageSize,
                orderBy: x => x.CreatedAt,
                ascending: false,
                tracking: false,
                cancellationToken: ct,
                includes: query => query
                .Include(x => x.FromAccount)
                .Include(x => x.ToAccount)
                );

            return (result.Items.Select(t => ToMyDto(t,myIds)).ToList(),result.TotalCount);
        }

        public async Task<TransferDto> GetTransfersByIdAsync(
            string userId,
            int transferId,
            CancellationToken ct = default
            )
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException("User ID is required.");

            if (transferId <= 0)
                throw new BadRequestException(
                    "Transfer ID must be positive.");

            var transfer = await _transferRepository.GetAsync(
                x => x.Id == transferId &&
                (x.FromAccount.UserId == userId ||
                 x.ToAccount.UserId == userId),
                tracking: false,
                cancellationToken: ct,
                includes: query => query
                .Include(x => x.FromAccount)
                .Include(x => x.ToAccount)
                );

            if (transfer is null) throw new NotFoundException("Transfer not found!");

            return ToDto(transfer,
                transfer.FromAccount.AccountNumber,
                transfer.ToAccount.AccountNumber);
        }


        private async Task<TransferDto> TryExecuteAsync(
    string userId,
    string fromAccountNumber,
    int toAccountId,
    decimal amount,
    string idempotencyKey,
    string? description = null,
    CancellationToken cancellationToken = default)
        {


            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                var from = await _accountRepository.GetAsync(
                    x => x.AccountNumber == fromAccountNumber
                         && x.UserId == userId,
                    cancellationToken: cancellationToken);

                if (from is null)
                    throw new NotFoundException("Source account not found.");

                var to = await _accountRepository.GetAsync(
                    x => x.Id == toAccountId,
                    cancellationToken: cancellationToken);

                if (to is null)
                    throw new NotFoundException("Destination account not found.");

                if (from.Currency != to.Currency)
                    throw new BadRequestException(
                        "Source and destination accounts must use the same currency.");

                //if (from.Id == to.Id)
                //    throw new BadRequestException(
                //        "Source and destination accounts cannot be the same."); //Domain mainc amowmebs

                var existing = await _transferRepository.GetAsync(
                    x => x.InitiatedByUserId == userId && x.IdempotencyKey == idempotencyKey,
                    tracking: false, cancellationToken: cancellationToken);

                if (existing is not null)
                {
                    if (existing.FromAccountId == from.Id
                        && existing.ToAccountId == to.Id
                        && existing.Amount == amount
                        && existing.Description == description?.Trim())
                    {
                        await _unitOfWork.RollbackTransactionAsync(
                            CancellationToken.None);

                        return ToDto(existing,from.AccountNumber,to.AccountNumber);
                    }

                    throw new ConflictException(
                        "Idempotency key already used for a different transfer.");
                }

                var transfer = Transfer.Create(
                    userId,
                    from,
                    to,
                    amount,
                    idempotencyKey,
                    description
                    );

                await _transferRepository.AddAsync(
                    transfer, cancellationToken);


                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var ledgerTransaction = transfer.Execute(from, to);

                await _ledgerRepository.AddAsync(
                    ledgerTransaction, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _audit.RecordAsync(userId, AuditActions.TransferCompleted, AuditEntities.Transfer,
                transfer.Id.ToString(),
                $"amount={amount} {from.Currency}; from={from.AccountNumber}; to={to.AccountNumber}", cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return ToDto(transfer,from.AccountNumber,to.AccountNumber);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(
                    CancellationToken.None);

                throw;
            }
        }

        public async Task<IReadOnlyList<RecipientAccountDto>> GetRecipientAccountAsync(string userId,
            string personalNumber,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(personalNumber))
                throw new BadRequestException("Personal number is required.");

            var allowed = await _redisService.TryAcquireRateLimitAsync(
                keyPrefix: "recipient-lookup",
                userId: userId,
                maxRequests: 20,
                cooldown: TimeSpan.FromSeconds(1),
                window: TimeSpan.FromMinutes(10));

            if (!allowed)
                throw new BadRequestException("Too many lookups. Please try again later");

            personalNumber = personalNumber.Trim();

            var recipient = await _userRepository.GetAsync(
                x => x.PersonalNumber == personalNumber,
                tracking: false,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Recipient not found");

            

            var (accounts, _) = await _accountRepository.GetAllAsync(
            filter: a => a.UserId == recipient.Id && a.Status == AccountStatus.Active,
            orderBy: a => a.Currency,
            tracking: false,
            cancellationToken: ct);
            if (!accounts.Any())
                throw new NotFoundException("Recipient not found.");

            return accounts.Select(a => new RecipientAccountDto
            {
                AccountId = a.Id,
                MaskedAccountNumber = MaskAccountNumber(a.AccountNumber),
                Currency = a.Currency
            }).ToList();

        }

        private static string MaskAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return string.Empty;

            const int visibleDigits = 4;

            if (accountNumber.Length <= visibleDigits)
                return new string('*', accountNumber.Length);

            return new string('*', accountNumber.Length - visibleDigits)
                + accountNumber[^visibleDigits..];
        }


    }
}

