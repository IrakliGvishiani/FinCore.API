using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using MapsterMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IRepositoryBase<BankAccount> _accounts;
        private readonly IRepositoryBase<LedgerTransaction> _ledgerTransactions;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private const decimal MaxDepositAmount = 10_100m;

        public AccountService(
            IRepositoryBase<BankAccount> account,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IRepositoryBase<LedgerTransaction> ledgerTransactions
            )
        {
            _accounts = account;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _ledgerTransactions = ledgerTransactions;
            
        }

        public async Task CloseAccountAsync(string userId, string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.UserId == userId &&
                x.AccountNumber == accountNumber,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account not found!");

            account.Close(); //Rules are one domain

            await _unitOfWork.SaveChangesAsync();
        }



        public async Task FreezeAccountAsync(string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.AccountNumber == accountNumber,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account Not Found");

            account.Freeze();

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<AccountForGettingDto> GetAccountAsync(string userId, string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.UserId == userId &&
                x.AccountNumber == accountNumber,
                tracking: false,
                cancellationToken: ct

                ) ?? throw new NotFoundException("Account not found");

            return _mapper.Map<AccountForGettingDto>(account);
        }

        public async Task<IReadOnlyList<AccountForGettingDto>> GetMyAccountsAsync(string userId, CancellationToken ct = default)
        {
            var (items, _) = await _accounts.GetAllAsync(
                filter: x => x.UserId == userId,
                orderBy: x => x.CreatedAt,
                tracking: false,
                cancellationToken: ct
                );

            return _mapper.Map<List<AccountForGettingDto>>(items);
        }

        public async Task<OpenAccountResponseDto> OpenAccountAsync(string userId, CurrencyCode currency, CancellationToken ct = default)
        {
            const int maxAttempts = 5;

            for (var attempt = 1;attempt < maxAttempts ; attempt++)
            {
                var account = BankAccount.Create(
                    AccountNumberGenerator.Generate(),
                    userId,
                    currency
                    );

                await _accounts.AddAsync(account,ct);

                try
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return _mapper.Map<OpenAccountResponseDto>(account);
                }
                catch(DuplicateKeyException ex) when (attempt < maxAttempts)
                {
                    await _unitOfWork.ClearTracking();
                }
            }

            throw new ConcurrencyConflictException("Could not generate a unique account number. Please try again.");
        }

        public async Task UnfreezeAccountAsync(string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.AccountNumber == accountNumber,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account Not Found");

            account.Unfreeze();

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<AccountForGettingDto> TryDepositAsync(string userId,
    string accountNumber,
    decimal amount, string key, CancellationToken ct)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(ct);

                var account = await _accounts.GetAsync(
                    x => x.AccountNumber == accountNumber &&
                    x.UserId == userId,
                    cancellationToken: ct
                    ) ?? throw new NotFoundException("Account Not Found");

                var reference = $"DEP:{userId}:{key}";

                if (await _ledgerTransactions.ExistsAsync(x => x.Reference == reference, ct))
                {
                    await _unitOfWork.RollbackTransactionAsync(ct);
                    return _mapper.Map<AccountForGettingDto>(account);
                }

                account.Credit(amount);

                var tx = LedgerTransaction.CreateDeposit(
                    reference,
                    account.Currency,
                    account.Id,
                    amount
                    );

                await _ledgerTransactions.AddAsync(tx,ct);

                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitTransactionAsync(ct);

                return _mapper.Map<AccountForGettingDto>(account);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                throw;
            }
        }

        public async Task<AccountForGettingDto> DepositAsync(string userId,
            string accountNumber, 
            decimal amount,
            string idempotencyKey, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 50)
                throw new BadRequestException("Idempotency key is required (max 50 chars).");
            if (amount > MaxDepositAmount)
                throw new BadRequestException($"Deposit limit is {MaxDepositAmount:N0}.");

            const int maxAttempts = 3;

            for (var attempts = 1;;attempts++)
            {
                try
                {
                    return await TryDepositAsync(
                        userId,
                        accountNumber,
                        amount,
                        idempotencyKey,
                        ct
                        );
                }
                catch(Exception ex) when ((ex is ConcurrencyConflictException or DuplicateKeyException) && attempts < maxAttempts)
                {
                    await _unitOfWork.ClearTracking();
                    await Task.Delay(Random.Shared.Next(20, 80) * attempts, ct);
                }
            }
        }
    }
}
