using FinCore.Application.Models.TransferDtos;
using FinCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface ITransferService
    {
        Task<TransferDto> ExecuteTransferAsync(
            string userId,
            string fromAccountNumber,
            string toAccountNumber,
            decimal amount,
            string idempotencyKey,
            string? description = null,
            CancellationToken ct = default
            );

        Task<(IEnumerable<MyTransferDto> Items, int TotalCount)>
            GetMyTransfersAsync(
            string userId,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default
            );

        Task<TransferDto> GetTransfersByIdAsync(
            string userId,
            int transferId,
            CancellationToken ct = default
            );
    }
}
