using CloudinaryDotNet.Actions;
using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.Xml;
using System.Text;

namespace FinCore.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IRepositoryBase<BankAccount> _accounts;
        private readonly IRepositoryBase<LedgerTransaction> _ledgerTransactions;
        private readonly IRepositoryBase<LedgerAccount> _ledgerAccounts;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;
        private readonly IMapper _mapper;
        private const decimal MaxDepositAmount = 10_100m;

        public AccountService(
            IRepositoryBase<BankAccount> account,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IRepositoryBase<LedgerTransaction> ledgerTransactions,
            IRepositoryBase<LedgerAccount> ledgerAccounts,
            IAuditService auditService
            )
        {
            _accounts = account;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _ledgerTransactions = ledgerTransactions;
            _ledgerAccounts = ledgerAccounts;
            _audit = auditService;
            
        }

        public async Task CloseAccountAsync(string userId, string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.UserId == userId &&
                x.AccountNumber == accountNumber,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account not found!");

            account.Close(); //Rules are one domain

            await _audit.RecordAsync(userId,AuditActions.AccountClosed,AuditEntities.BankAccount,
                account.AccountNumber,
                null,
                ct);

            await _unitOfWork.SaveChangesAsync();
        }



        public async Task FreezeAccountAsync(string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.AccountNumber == accountNumber,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account Not Found");

            account.Freeze();

            await _audit.RecordAsync(null,AuditActions.AccountFrozen,AuditEntities.BankAccount,
                account.AccountNumber,
                null,
                ct);

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

        public async Task<OpenAccountResponseDto> OpenAccountAsync(string userId,
            CurrencyCode currency,
            string name,
            CancellationToken ct = default)
        {
            const int maxAttempts = 5;

            for (var attempt = 1;attempt < maxAttempts ; attempt++)
            {
                var account = BankAccount.Create(
                    AccountNumberGenerator.Generate(),
                    userId,
                    currency,
                    name
                    );

                await _accounts.AddAsync(account,ct);

                try
                {
                    await _audit.RecordAsync(userId,AuditActions.AccountOpened,AuditEntities.BankAccount,
                        account.AccountNumber,
                        null,
                        ct);

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

            await _audit.RecordAsync(null,AuditActions.AccountUnfrozen,AuditEntities.BankAccount,
                account.AccountNumber,
                null,
                ct);

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<AccountForGettingDto> TryDepositAsync(string userId,
    string accountNumber,
    decimal amount, string key, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    throw new BadRequestException("Idempotency key is required.");

                if (amount <= 0 || decimal.Round(amount, 2) != amount)
                    throw new BadRequestException(
                        "Amount must be positive with at most 2 decimals.");

                await _unitOfWork.BeginTransactionAsync(ct);

                var account = await _accounts.GetAsync(
                    x => x.AccountNumber == accountNumber &&
                    x.UserId == userId,
                    cancellationToken: ct
                    ) ?? throw new NotFoundException("Account Not Found");

                var reference = $"DEP:{userId}:{key.Trim()}";

                var existing = await _ledgerTransactions.GetAsync(
                    x => x.Reference == reference,
                    includes: q => q.Include(x => x.Entries),
                    tracking: false,
                    cancellationToken: ct
                    );

                if(existing is not null)
                {
                    var customerEntry = existing.Entries.SingleOrDefault(
                        x => x.BankAccountId == account.Id  
                        &&
                        x.Type == EntryType.Debit
                        );

                    var cashEntry = existing.Entries.SingleOrDefault(
                        x => x.BankAccountId == account.Id
                        &&
                        x.Type == EntryType.Debit
                        );

                    var sameRequest =
                        existing.Currency == account.Currency &&
                        customerEntry is not null &&
                        cashEntry is not null &&
                        customerEntry.Amount == amount &&
                        cashEntry.Amount == amount;

                    if (!sameRequest)
                        throw new ConflictException("IdempotencyKey has already been used for a different deposit");

                    await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);

                    return _mapper.Map<AccountForGettingDto>(account);

                  
                }

                var cash = await _ledgerAccounts.GetAsync(
                    x => x.AccountCode == LedgerAccountCodes.Cash(account.Currency),
                    tracking: false,
                    cancellationToken: ct
                    ) ?? throw new InvalidOperationException("Bank cash ledger account is not configured.");

                account.Credit(amount);

                var tx = LedgerTransaction.CreateDeposit(reference,account,cash,amount);

                await _ledgerTransactions.AddAsync(tx,ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await _audit.RecordAsync(userId,AuditActions.Deposit,AuditEntities.BankAccount
                    ,account.AccountNumber,
                    $"amount={amount} {account.Currency}",ct);

                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitTransactionAsync(ct);

                return _mapper.Map<AccountForGettingDto>(account);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
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

        public async Task<AccountForGettingDto> RenameAccountAsync(string userId,
            string accountNumber, 
            string name, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                x => x.AccountNumber == accountNumber && x.UserId == userId,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account not found");

            account.Rename(name);
            await _unitOfWork.SaveChangesAsync(ct);

            return _mapper.Map<AccountForGettingDto>(account);
        }
    }
}
