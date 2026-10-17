using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IAccountService
    {
        Task<OpenAccountResponseDto> OpenAccountAsync(string userId,CurrencyCode currency,CancellationToken ct = default);

        Task<IReadOnlyList<AccountForGettingDto>> GetMyAccountsAsync(
            string userId,
            CancellationToken ct = default
            );

        Task<AccountForGettingDto> GetAccountAsync(string userId,string accountNumber,CancellationToken ct =default);

        Task CloseAccountAsync(string userId, string accountNumber,CancellationToken ct =default);
    }
}
