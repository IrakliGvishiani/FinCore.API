using FinCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface ITransferService
    {
        Task<Transfer> ExecuteTransferAsync(
            string userId,
            string fromAccountNumber,
            string toAccountNumber,
            decimal amount,
            string idempotencyKey,
            string? description = null,
            CancellationToken cancellationToken = default
            );
    }
}
