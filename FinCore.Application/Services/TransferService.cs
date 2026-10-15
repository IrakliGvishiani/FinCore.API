using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Domain.Entities;
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
            CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
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

                var transfer = Transfer.Create(
                    from,
                    to,
                    amount,
                    idempotencyKey,
                    description);

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

