using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class TransferService : ITransferService
    {

        private readonly IRepositoryBase<BankAccount> _accountRepository;
        private readonly IRepositoryBase<Transfer> _transferRepository;
        private readonly IRepositoryBase<LedgerTransaction> _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;

        public TransferService(IRepositoryBase<BankAccount> accountRepository,
            IRepositoryBase<Transfer> transferRepository,
            IRepositoryBase<LedgerTransaction> ledgerRepository,
            IUnitOfWork unitOfWork)
        {
            _accountRepository = accountRepository;
            _transferRepository = transferRepository;
            _ledgerRepository = ledgerRepository;
            _unitOfWork = unitOfWork;
        }



        public async Task<Transfer> ExecuteTransferAsync(
            string userId,
            string fromAccountNumber,
            string toAccountNumber,
            decimal amount,
            string idempotencyKey,
            string? description = null,
            CancellationToken ct = default
            )
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
                throw new BadRequestException("idempotencyKey is required (max 100 chars)");

            description = NormalizeDescription(description);

            const int maxAttempts = 3;

                for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await TryExecuteAsync(
                        userId,
                        fromAccountNumber,
                        toAccountNumber,
                        amount,
                        idempotencyKey,
                        description,
                        ct
                        );
                }
                catch(Exception ex) when (
                (ex is ConcurrencyConflictException or DuplicateKeyException)
                && attempt < maxAttempts
                )
                {
                    await _unitOfWork.ClearTracking();
                    await Task.Delay(Random.Shared.Next(20,80) * attempt, ct);
                }
            }

        }
        private static string? NormalizeDescription(string? d)
         => string.IsNullOrWhiteSpace(d) ? null : d.Trim();



        public async Task<(IEnumerable<Transfer> Items,int TotalCount)> GetMyTransfersAsync(
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
                return (Enumerable.Empty<Transfer>(), 0);

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

            return result;
        }

        public async Task<Transfer> GetTransfersByIdAsync(
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

            return transfer;
        }


        private async Task<Transfer> TryExecuteAsync(
    string userId,
    string fromAccountNumber,
    string toAccountNumber,
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
                    x => x.AccountNumber == toAccountNumber,
                    cancellationToken: cancellationToken);

                if (to is null)
                    throw new NotFoundException("Destination account not found.");

                if (from.Id == to.Id)
                    throw new BadRequestException(
                        "Source and destination accounts cannot be the same.");

                var existing = await _transferRepository.GetAsync(
                    x => x.IdempotencyKey == idempotencyKey,
                    tracking: false,
                    cancellationToken: cancellationToken);

                if (existing is not null)
                {
                    if (existing.FromAccountId == from.Id
                        && existing.ToAccountId == to.Id
                        && existing.Amount == amount
                        && existing.Description == description?.Trim())
                    {
                        await _unitOfWork.RollbackTransactionAsync(
                            CancellationToken.None);

                        return existing;
                    }

                    throw new BadRequestException(
                        "Idempotency key already used for a different transfer.");
                }

                var fromAccount = await _accountRepository.GetAsync(
                    x => x.AccountNumber == fromAccountNumber,
                    tracking: false,
                    cancellationToken: cancellationToken
                    );

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

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return transfer;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(
                    CancellationToken.None);

                throw;
            }
        }

    }
}

