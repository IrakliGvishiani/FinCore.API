using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class AccountStatementService : IAccountStatementService
    {

        private readonly IRepositoryBase<BankAccount> _accountRepository;
        private readonly IRepositoryBase<LedgerEntry> _ledgerEntryRepository;


        public AccountStatementService(
            IRepositoryBase<BankAccount> accountRepo,
            IRepositoryBase<LedgerEntry> ledgerEntryRepo
            )
        {
            _accountRepository = accountRepo;
            _ledgerEntryRepository = ledgerEntryRepo;
        }
        public async Task<AccountStatementDto> GetStatementAsync(string userId,
            string accountNumber,
            DateTime? from, DateTime? to, int pageNumber = 1, int pageSize = 15,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException("User ID is required.");

            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new BadRequestException("Account number is required.");

            if (pageNumber < 1)
                throw new BadRequestException(
                    "Page number must be greater than zero.");

            if (pageSize < 1 || pageSize > 100)
                throw new BadRequestException(
                    "Page size must be between 1 and 100.");

            if (from.HasValue && to.HasValue && from.Value > to.Value)
                throw new BadRequestException(
                    "'From' date cannot be later than 'To' date.");

            var account = await _accountRepository.GetAsync(
                x => x.AccountNumber == accountNumber &&
                x.UserId == userId,
                tracking: false,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account not found");

            var (entries, totalcount) = await _ledgerEntryRepository.GetAllAsync(
                filter: x =>
                x.BankAccountId == account.Id &&
                (!from.HasValue || x.CreatedAt >= from.Value) &&
                (!to.HasValue || x.CreatedAt < to.Value.AddDays(1)),
                pageNumber: pageNumber,
                pageSize: pageSize,
                orderBy: x => x.CreatedAt,
                ascending: false,
                cancellationToken: ct,
                tracking: false,
                includes: q => q.Include(x => x.LedgerTransaction)
                );

            var items = entries.Select(x => new AccountStatementEntryDto
            {
                LedgerEntryId = x.Id,
                Reference = x.LedgerTransaction.Reference,
                Type = x.Type,
                Amount = x.Amount,
                Currency = x.Currency,
                CreatedAt = x.CreatedAt
            }).ToList();

            return new AccountStatementDto
            {
                Items = items,
                TotalCount = totalcount
            };
        }
    }
}
