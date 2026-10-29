using FinCore.Application.Models.BankAccountDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IAccountStatementService
    {
        Task<AccountStatementDto> GetStatementAsync(
            string userId,
            string accountNumber,
            DateTime? from,
            DateTime? to,
            int pageNumber = 1,
            int pageSize = 15,
            CancellationToken ct =default
            );
    }
}
