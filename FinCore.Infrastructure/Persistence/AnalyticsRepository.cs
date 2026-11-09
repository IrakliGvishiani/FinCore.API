using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Models.Analytics;
using FinCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace FinCore.Infrastructure.Persistence
{
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly ApplicationDbContext _context;

        public AnalyticsRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<List<CurrencyAnalyticsDto>> GetCurrencyAnalyticsAsync(string userId,
            DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            // 1. Current balances grouped by currency
            var balances = await _context.BankAccounts
                .AsNoTracking()
                .Where(x => x.UserId == userId &&
                            x.Status != AccountStatus.Closed)
                .GroupBy(x => x.Currency)
                .Select(g => new
                {
                    Currency = g.Key,
                    Balance = g.Sum(x => x.Balance)
                })
                .ToListAsync(ct);


            var analytics = await _context.LedgerEntries
                .AsNoTracking()
                .Where(x =>
                x.BankAccountId != null &&
                x.BankAccount.UserId == userId &&
                x.CreatedAt >= fromDate &&
                x.CreatedAt < toDate
                )
                .GroupBy(x => x.Currency)
                .Select(g => new
                {
                    Currency = g.Key,

                    Income = g.Sum(x =>
                    x.LedgerTransaction.TransactionType == LedgerTransactionType.Deposit &&
                    x.Type == EntryType.Credit
                    ? x.Amount
                    : x.LedgerTransaction.TransactionType == LedgerTransactionType.Transfer &&
                    x.Type == EntryType.Credit &&
                    x.LedgerTransaction.Transfer != null &&
                    x.LedgerTransaction.Transfer.FromAccount.UserId !=
                    x.LedgerTransaction.Transfer.ToAccount.UserId ?
                    x.Amount : 0m),


                    Expenses = g.Sum(x =>
                    x.LedgerTransaction.TransactionType == LedgerTransactionType.CardPayment &&
                    x.Type == EntryType.Debit
                    ? x.Amount
                    : x.LedgerTransaction.TransactionType == LedgerTransactionType.Transfer &&
                    x.Type == EntryType.Debit &&
                    x.LedgerTransaction.Transfer != null &&
                    x.LedgerTransaction.Transfer.FromAccount.UserId !=
                    x.LedgerTransaction.Transfer.ToAccount.UserId
                    ? x.Amount : 0m
                    )
                }

                    ).ToListAsync(ct);

            var result = balances.Select(
                x => new CurrencyAnalyticsDto
                {
                    Currency = x.Currency,
                    Balance = x.Balance
                }
                ).ToDictionary(x => x.Currency);

            foreach (var item in analytics)
            {
                if (!result.TryGetValue(item.Currency, out var currency))
                {
                    currency = new CurrencyAnalyticsDto
                    {
                        Currency = item.Currency
                    };

                    result.Add(item.Currency,currency);
                }

                currency.Income = item.Income;
                currency.Expenses = item.Expenses;
                    
            }

            return result.Values.ToList();
        }
                
        

        public async Task<List<MonthlyAnalyticsDto>> GetMonthlyAnalyticsAsync(string userId,
            DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            return await _context.LedgerEntries
                .AsNoTracking()
                .Where(x =>
                x.BankAccountId != null &&
                x.BankAccount.UserId == userId &&
                x.CreatedAt >= fromDate &&
                x.CreatedAt < toDate)
                .GroupBy(x => new
                {
                    x.CreatedAt.Year,
                    x.CreatedAt.Month,
                    x.Currency
                })
                .Select(g => new MonthlyAnalyticsDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Currency = g.Key.Currency,

                    Income = g.Sum(x =>
                    x.LedgerTransaction.TransactionType == LedgerTransactionType.Deposit &&
                    x.Type == EntryType.Credit
                    ?
                    x.Amount
                    : x.LedgerTransaction.TransactionType == LedgerTransactionType.Transfer &&
                    x.Type == EntryType.Credit &&
                    x.LedgerTransaction.Transfer != null &&
                    x.LedgerTransaction.Transfer.FromAccount.UserId !=
                    x.LedgerTransaction.Transfer.ToAccount.UserId
                    ? x.Amount
                    : 0m),

                    Expenses = g.Sum(x =>
                    x.LedgerTransaction.TransactionType == LedgerTransactionType.CardPayment &&
                    x.Type == EntryType.Debit
                    ?
                    x.Amount
                    : x.LedgerTransaction.TransactionType == LedgerTransactionType.Transfer &&
                    x.Type == EntryType.Debit &&
                    x.LedgerTransaction.Transfer != null &&
                    x.LedgerTransaction.Transfer.FromAccount.UserId !=
                    x.LedgerTransaction.Transfer.ToAccount.UserId
                    ? x.Amount
                    : 0m)


                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Currency)
                .ToListAsync(ct);
        }
    }
}
