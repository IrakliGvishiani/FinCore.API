using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Common;
using FinCore.Application.Models.ManagerDtos;
using FinCore.Application.Models.SuspiciousActivity;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class ManagerService : IManagerService
    {

        private readonly IManagerRepository _managerRepo;
        private readonly IUnitOfWork _unitOfWork;


        public ManagerService(IManagerRepository managerRepository, IUnitOfWork unitOfWork)
        {
            _managerRepo = managerRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<AccountDetailsDto?> GetAccountByIdAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0)
                throw new BadRequestException("Invalid account ID.");

            var account = await _managerRepo.GetAccountByIdAsync(id, ct);

            if (account == null)
                throw new NotFoundException("Account not found.");

            return account;
        }

        public async Task<PaginatedResult<AccountMonitoringDto>> GetAccountsAsync(AccountFilterDto filter, CancellationToken ct = default)
            => await _managerRepo.GetAccountsAsync(filter, ct);

        public async Task<PaginatedResult<AccountTransactionHistoryDto>>
         GetAccountTransactionsAsync(
            int accountId,
            AccountTransactionFilterDto filter,
            CancellationToken ct = default)
        {
            if (accountId <= 0)
                throw new BadRequestException("Invalid account ID.");

            var accountExists = await _managerRepo
                .AccountExistsAsync(accountId, ct);

            if (!accountExists)
                throw new NotFoundException("Account not found.");

            return await _managerRepo
                .GetAccountTransactionsAsync(accountId, filter, ct);
        }

        public async Task<CurrencyConversionDetailsDto> GetCurrencyConversionByIdAsync(int id,
            CancellationToken ct = default)
        {
            if (id <= 0)
                throw new BadRequestException("Invalid conversion ID.");

            var conversion = await _managerRepo
                .GetCurrencyConversionByIdAsync(id, ct);

            if (conversion == null)
                throw new NotFoundException("Currency conversion not found.");

            return conversion;
        }

        public async Task<PaginatedResult<CurrencyConversionMonitoringDto>> GetCurrencyConversionsAsync(
            CurrencyConversionFilterDto filter, CancellationToken ct = default)
            => await _managerRepo.GetCurrencyConversionsAsync(filter, ct);

        public async Task<ManagerDashboardDto> GetDashboardAsync(CancellationToken ct = default)
            => await _managerRepo.GetDashboardAsync(ct);

        public  async Task<PaginatedResult<SuspiciousActivityMonitoringDto>> GetSuspiciousActivitiesAsync(
            SuspiciousActivityFilterDto filter,
            CancellationToken ct = default)
        {
            if (filter.PageNumber < 1)
                throw new BadRequestException("Page number must be greater than zero.");

            if (filter.PageSize < 1 || filter.PageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");

            if (filter.FromDate.HasValue &&
                filter.ToDate.HasValue &&
                filter.FromDate > filter.ToDate)
            {
                throw new BadRequestException("Invalid date range.");
            }

            return await _managerRepo.GetSuspiciousActivitiesAsync(filter, ct);
        }

        public async Task<SuspiciousActivityDetailsDto?> GetSuspiciousActivityByIdAsync(int id,
            CancellationToken ct = default)
        {
            if (id <= 0)
                throw new BadRequestException("Invalid suspicious activity ID.");

            var activity = await _managerRepo
                .GetSuspiciousActivityByIdAsync(id, ct);

            if (activity is null)
                throw new NotFoundException("Suspicious activity not found.");

            return activity;
        }

        public async Task<PaginatedResult<TransferMonitoringDto>> GetTransfersAsync(TransferFilterDto filter,
            CancellationToken ct = default)
            => await _managerRepo.GetTransfersAsync(filter, ct);


        public async Task<TransferDetailsDto?> GetTransfersByIdAsync(int id, CancellationToken ct = default)
            => await _managerRepo.GetTransfersById(id, ct);

        public async Task ResolveSuspiciousActivityAsync(int id, CancellationToken ct = default)
        {
            var activity = await _managerRepo 
              .GetSuspiciousActivityForUpdateAsync(id, ct);

            if (activity == null)
                throw new NotFoundException("Suspicious activity not found.");

            activity.Resolve();

            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task ReviewSuspiciousActivityAsync(int id, string UserId, CancellationToken ct = default)
        {
            var activity = await _managerRepo.GetSuspiciousActivityForUpdateAsync(id, ct);

            if (activity is null)
                throw new NotFoundException("Suspicious activity not found.");

            activity.StartReview(UserId);

            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
    