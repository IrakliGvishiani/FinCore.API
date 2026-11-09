using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Models.Common;
using FinCore.Application.Models.ManagerDtos;
using FinCore.Application.Models.SuspiciousActivity;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Persistence
{
    public class ManagerRepository : IManagerRepository
    {
        private readonly ApplicationDbContext _context;

        public ManagerRepository(ApplicationDbContext context)
        {
            _context = context;
        }



        public async Task<ManagerDashboardDto> GetDashboardAsync(CancellationToken ct = default)
        {
            var todayStartUtc = DateTime.UtcNow.Date;
            var tommorowStartUtc = todayStartUtc.AddDays(1);


            var totalUsers = await _context.Users.CountAsync(ct);

            var totalAccounts = await _context.BankAccounts.CountAsync(ct);

            var activeAccounts = await _context.BankAccounts.CountAsync(
                x => x.Status == AccountStatus.Active, ct
                );

            var frozenAccounts = await _context.BankAccounts.CountAsync(
                x => x.Status == AccountStatus.Frozen, ct
                );

            var closedAccounts = await _context.BankAccounts.CountAsync(
                x => x.Status == AccountStatus.Closed, ct
                );

            var totalTransfers = await _context.Transfers.CountAsync(ct);

            var completedTransfers = await _context.Transfers.CountAsync(
                x => x.Status == TransferStatus.Completed, ct
                );

            var pendingTransfers = await _context.Transfers.CountAsync(
                x => x.Status == TransferStatus.Pending, ct
                );

            var failedTransfers = await _context.Transfers.CountAsync(
                x => x.Status == TransferStatus.Failed, ct
                );

            var totalConversions = await _context.CurrencyConversions.CountAsync(ct);

            var todayTransfers = await _context.Transfers.CountAsync(
                x => x.CreatedAt >= todayStartUtc &&
                x.CreatedAt < tommorowStartUtc, ct
                );

            var todayConversions = await _context.CurrencyConversions.CountAsync(
                x => x.CreatedAt >= todayStartUtc &&
                x.CreatedAt < tommorowStartUtc, ct
                );

            return new ManagerDashboardDto
            {
                TotalUsers = totalUsers,
                TotalAccounts = totalAccounts,
                ActiveAccounts = activeAccounts,
                FrozenAccounts = frozenAccounts,
                ClosedAccounts = closedAccounts,
                TotalTransfers = totalTransfers,
                CompletedTransfers = completedTransfers,
                PendingTransfers = pendingTransfers,
                FailedTransfers = failedTransfers,
                TotalConversions = totalConversions,
                TodayTransfers = todayTransfers,
                TodayConversions = todayConversions
            };
        }


        /// TRANSFER MONITORING
        public async Task<PaginatedResult<TransferMonitoringDto>> GetTransfersAsync(TransferFilterDto filter,
            CancellationToken ct = default)
        {
            var query = _context.Transfers
                .AsNoTracking()
                .AsQueryable();

            if (filter.Status.HasValue)
            {
                query = query.Where(x => x.Status == filter.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.AccountNumber))
            {
                var accountNumber = filter.AccountNumber.Trim();

                query = query.Where(x =>
                    x.FromAccount.AccountNumber.Contains(accountNumber) ||
                    x.ToAccount.AccountNumber.Contains(accountNumber));
            }

            if (filter.FromDate.HasValue)
            {
                var fromDate = filter.FromDate.Value.Date;
                query = query.Where(x => x.CreatedAt >= fromDate);
            }

            if (filter.ToDate.HasValue)
            {
                var toDateExclusive = filter.ToDate.Value.Date.AddDays(1);
                query = query.Where(x => x.CreatedAt < toDateExclusive);
            }

            var totalCount = await query.CountAsync(ct);

            var pageNumber = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var transfers = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TransferMonitoringDto
            {
                Id = x.Id,
                FromAccountNumber = x.FromAccount.AccountNumber,
                ToAccountNumber = x.ToAccount.AccountNumber,
                Amount = x.Amount,
                Currency = x.Currency,
                Status = x.Status,
                Description = x.Description,
                InitiatedByUserId = x.InitiatedByUserId,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(ct);

            return new PaginatedResult<TransferMonitoringDto>
            {
                Items = transfers,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<TransferDetailsDto?> GetTransfersById(int id, CancellationToken ct = default)
        {
                    var transfer = await _context.Transfers
             .AsNoTracking()
             .Where(x => x.Id == id)
             .Select(x => new TransferDetailsDto
             {
                 Id = x.Id,
                 FromAccountNumber = x.FromAccount.AccountNumber,
                 ToAccountNumber = x.ToAccount.AccountNumber,
                 FromUserId = x.FromAccount.UserId,
                 ToUserId = x.ToAccount.UserId,
                 Amount = x.Amount,
                 Currency = x.Currency,
                 Status = x.Status,
                 Description = x.Description,
                 InitiatedByUserId = x.InitiatedByUserId,
                 FailureReason = x.FailureReason,
                 CreatedAt = x.CreatedAt,
                 CompletedAt = x.CompletedAt
             })
             .FirstOrDefaultAsync(ct);

            if (transfer == null)
                return null;

            transfer.LedgerEntries = await _context.LedgerEntries
                .AsNoTracking()
                .Where(e => _context.LedgerTransactions
                    .Any(t => t.Id == e.LedgerTransactionId &&
                              t.TransferId == id))
                .Select(e => new TransferLedgerEntryDto
                {
                    Id = e.Id,
                    BankAccountId = e.BankAccountId,
                    LedgerAccountId = e.LedgerAccountId,
                    Type = e.Type,
                    Amount = e.Amount
                })
                .ToListAsync(ct);

            return transfer;
        }


        /// ACCOUNT MONITORING

        public async Task<AccountDetailsDto?> GetAccountByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.BankAccounts
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AccountDetailsDto
                {
                    Id = x.Id,
                    AccountNumber = x.AccountNumber,
                    Name = x.Name,
                    UserId = x.UserId,
                    UserFullName = x.User.FirstName + " " + x.User.LastName,
                    UserEmail = x.User.Email,
                    Currency = x.Currency,
                    Balance = x.Balance,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<PaginatedResult<AccountMonitoringDto>> GetAccountsAsync(AccountFilterDto filter,
            CancellationToken ct = default)
        {
            var query = _context.BankAccounts.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.AccountNumber))
            {
                var accountNumber = filter.AccountNumber.Trim();

                query = query.Where(a => a.AccountNumber.Contains(accountNumber));
            }

            if (!string.IsNullOrWhiteSpace(filter.UserId))
            {

                query = query.Where(a => a.UserId == filter.UserId);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(a => a.Status == filter.Status);
            }

            if (filter.Currency.HasValue)
            {
                query = query.Where(x => x.Currency == filter.Currency);
            }

            var pageNumber = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var totalCount = await query.CountAsync(ct);

            var accounts = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountMonitoringDto
                {
                    Id = x.Id,
                    AccountNumber = x.AccountNumber,
                    Name = x.Name,
                    UserId = x.UserId,
                    UserFullName = x.User.FirstName + " " + x.User.LastName,
                    Currency = x.Currency,
                    Balance = x.Balance,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);

            return new PaginatedResult<AccountMonitoringDto>
            {
                Items = accounts,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PaginatedResult<AccountTransactionHistoryDto>> GetAccountTransactionsAsync(int accountId,
            AccountTransactionFilterDto filter, CancellationToken ct = default)
        {
            var query = _context.LedgerEntries
                .AsNoTracking()
                .Where(e => e.BankAccountId == accountId)
                .Join(
                _context.LedgerTransactions.AsNoTracking(),
                entry => entry.LedgerTransactionId,
                transaction => transaction.Id,
                (entry,transaction) => new 
                { 
                    Entry = entry,
                    Transaction = transaction 
                }

                );

            if (filter.Type.HasValue)
                query = query.Where(x => x.Entry.Type == filter.Type.Value);

            if (filter.FromDate.HasValue)
                query = query.Where(x =>
                    x.Transaction.CreatedAt >= filter.FromDate.Value.Date);

            if (filter.ToDate.HasValue)
                query = query.Where(x =>
                    x.Transaction.CreatedAt < filter.ToDate.Value.Date.AddDays(1));

            var totalCount = await query.CountAsync(ct);

            var pageNumber = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var items = await query
                .OrderByDescending(x => x.Transaction.CreatedAt)
                .ThenByDescending(x => x.Transaction.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AccountTransactionHistoryDto
                 {
                     Id = x.Transaction.Id,
                     Reference = x.Transaction.Reference,
                     Type = x.Entry.Type,
                     Amount = x.Entry.Amount,
                     Currency = x.Transaction.Currency,
                     CreatedAt = x.Transaction.CreatedAt
                 })
                .ToListAsync(ct);

            return new PaginatedResult<AccountTransactionHistoryDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

        }


        public async Task<bool> AccountExistsAsync(
        int accountId,
        CancellationToken ct = default)
        {
            return await _context.BankAccounts
                .AnyAsync(x => x.Id == accountId, ct);
        }

        /// CURRENCY CONVERSION MONITORING
        public async Task<PaginatedResult<CurrencyConversionMonitoringDto>> GetCurrencyConversionsAsync(
        CurrencyConversionFilterDto filter,
        CancellationToken ct = default)
        {
            var query = _context.CurrencyConversions
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.UserId))
            {
                query = query.Where(x =>
                    x.InitiatedByUserId == filter.UserId);
            }

            if (!string.IsNullOrWhiteSpace(filter.AccountNumber))
            {
                var accountNumber = filter.AccountNumber.Trim();

                query = query.Where(x =>
                    x.FromAccount.AccountNumber.Contains(accountNumber) ||
                    x.ToAccount.AccountNumber.Contains(accountNumber));
            }

            if (filter.FromCurrency.HasValue)
            {
                query = query.Where(x =>
                    x.FromCurrency == filter.FromCurrency.Value);
            }

            if (filter.ToCurrency.HasValue)
            {
                query = query.Where(x =>
                    x.ToCurrency == filter.ToCurrency.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt >= filter.FromDate.Value.Date);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt < filter.ToDate.Value.Date.AddDays(1));
            }

            var totalCount = await query.CountAsync(ct);

            var pageNumber = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new CurrencyConversionMonitoringDto
                {
                    Id = x.Id,
                    Reference = x.Reference,
                    InitiatedByUserId = x.InitiatedByUserId,

                    FromAccountNumber = x.FromAccount.AccountNumber,
                    ToAccountNumber = x.ToAccount.AccountNumber,

                    FromCurrency = x.FromCurrency,
                    ToCurrency = x.ToCurrency,

                    FromAmount = x.FromAmount,
                    ToAmount = x.ToAmount,

                    FeeRate = x.FeeRate,
                    FeeAmount = x.FeeAmount,
                    NetAmount = x.NetAmount,

                    Rate = x.Rate,
                    RateDate = x.RateDate,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);

            return new PaginatedResult<CurrencyConversionMonitoringDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<CurrencyConversionDetailsDto?> GetCurrencyConversionByIdAsync(
        int id,
        CancellationToken ct = default)
        {
            return await _context.CurrencyConversions
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new CurrencyConversionDetailsDto
                {
                    Id = x.Id,
                    Reference = x.Reference,
                    InitiatedByUserId = x.InitiatedByUserId,

                    FromAccountNumber = x.FromAccount.AccountNumber,
                    ToAccountNumber = x.ToAccount.AccountNumber,

                    FromCurrency = x.FromCurrency,
                    ToCurrency = x.ToCurrency,

                    FromAmount = x.FromAmount,
                    ToAmount = x.ToAmount,

                    FeeRate = x.FeeRate,
                    FeeAmount = x.FeeAmount,
                    NetAmount = x.NetAmount,

                    Rate = x.Rate,
                    RateDate = x.RateDate,
                    CreatedAt = x.CreatedAt,

                    IdempotencyKey = x.IdempotencyKey
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<PaginatedResult<SuspiciousActivityMonitoringDto>> GetSuspiciousActivitiesAsync(
            SuspiciousActivityFilterDto filter,
            CancellationToken ct = default)
        {
            
            var query = _context.SuspiciousActivities
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.UserId))
            {
                query = query.Where(x =>
                    x.UserId == filter.UserId);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt >= filter.FromDate.Value.Date);
            }

            if(filter.Status.HasValue)
            {
                query = query.Where(x => x.Status == filter.Status.Value);
            }

            if(filter.RiskLevel.HasValue)
            {
                query = query.Where(x => x.RiskLevel == filter.RiskLevel.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt < filter.ToDate.Value.Date.AddDays(1));
            }

            var totalCount = await query.CountAsync(ct);

            var pageNumber = Math.Max(1, filter.PageNumber);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new SuspiciousActivityMonitoringDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    TransferId = x.TransferId,
                    Reason = x.Reason,
                    Status = x.Status,
                    RiskScore = x.RiskScore,
                    RiskLevel = x.RiskLevel,
                    CreatedAt = x.CreatedAt,
                    ReviewedByUserId = x.ReviewedByUserId,
                    ReviewedAt = x.ReviewedAt
                })
                .ToListAsync(ct);

            return new PaginatedResult<SuspiciousActivityMonitoringDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<SuspiciousActivityDetailsDto?> GetSuspiciousActivityByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.SuspiciousActivities
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new SuspiciousActivityDetailsDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    TransferId = x.TransferId,
                    Reason = x.Reason,
                    Status = x.Status,
                    RiskScore = x.RiskScore,
                    RiskLevel = x.RiskLevel,
                    CreatedAt = x.CreatedAt,
                    ReviewedByUserId = x.ReviewedByUserId,
                    ReviewedAt = x.ReviewedAt,

                    TransferReference = _context.LedgerTransactions
                    .Where(t => t.TransferId == x.TransferId)
                    .Select(t => t.Reference)
                    .FirstOrDefault() ?? string.Empty,

                    TransferAmount = x.Transfer.Amount,
                    Currency = x.Transfer.Currency,
                    FromAccountNumber = x.Transfer.FromAccount.AccountNumber,
                    ToAccountNumber = x.Transfer.ToAccount.AccountNumber,
                    InitiatedByUserId = x.Transfer.InitiatedByUserId
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<SuspiciousActivity?> GetSuspiciousActivityForUpdateAsync(
            int id,
            CancellationToken ct = default)
        {
            return await _context.SuspiciousActivities
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }
    }
}
