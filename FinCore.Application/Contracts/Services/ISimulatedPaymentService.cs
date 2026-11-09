using FinCore.Application.Models.Common;
using FinCore.Application.Models.VirtualCardDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface ISimulatedPaymentService
    {
        Task<PaymentDto> PayAsync(
            string userId,
            CreatePaymentDto model,
            CancellationToken ct = default
            );

        Task<PaymentDto> GetByIdAsync(
            string userId,
            int paymentId,
            CancellationToken ct = default
            );

        Task<PaginatedResult<PaymentDto>> GetAllAsync(
            string userId,
            int pageNumber = 1,
            int pageSize = 15,
            CancellationToken ct = default
            );

        Task<PaginatedResult<PaymentDto>> GetByCardAsync(
            string userId,
            int cardId,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default
            );
    }
}
