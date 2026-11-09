using FinCore.Application.Models.Common;
using FinCore.Application.Models.ManagerDtos;
using FinCore.Application.Models.SuspiciousActivity;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IManagerService
    {
        Task<ManagerDashboardDto> GetDashboardAsync(CancellationToken ct = default);

        Task<PaginatedResult<TransferMonitoringDto>> GetTransfersAsync(TransferFilterDto filter,
            CancellationToken ct = default);

        Task<TransferDetailsDto?> GetTransfersByIdAsync(int id,CancellationToken ct = default);

        Task<PaginatedResult<AccountMonitoringDto>> GetAccountsAsync(
            AccountFilterDto filter,
            CancellationToken ct = default);

        Task<AccountDetailsDto?> GetAccountByIdAsync(int id,
            CancellationToken ct = default);

        Task<PaginatedResult<AccountTransactionHistoryDto>> GetAccountTransactionsAsync(
            int accountId,
            AccountTransactionFilterDto filter,
            CancellationToken ct = default);


        Task<PaginatedResult<CurrencyConversionMonitoringDto>> GetCurrencyConversionsAsync(
        CurrencyConversionFilterDto filter,
        CancellationToken ct = default);

        Task<CurrencyConversionDetailsDto> GetCurrencyConversionByIdAsync(
          int id,
          CancellationToken ct = default);

        Task<PaginatedResult<SuspiciousActivityMonitoringDto>> GetSuspiciousActivitiesAsync(
            SuspiciousActivityFilterDto filter,
            CancellationToken ct = default
            );

        Task<SuspiciousActivityDetailsDto?> GetSuspiciousActivityByIdAsync(
            int id,
            CancellationToken ct = default);

        Task ReviewSuspiciousActivityAsync(
            int id,
            string UserId,
            CancellationToken ct = default);

        Task ResolveSuspiciousActivityAsync(
            int id,
            CancellationToken ct = default);
    }
}
