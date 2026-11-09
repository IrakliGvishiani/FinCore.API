using FinCore.Application.Models.Common;
using FinCore.Application.Models.VirtualCardDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IVirtualCardService
    {
        Task<VirtualCardCreatedDto> CreateAsync(
            string userId,
            CreateVirtualCardDto model,
            CancellationToken ct = default
            );

        Task SetLimitAsync(
            int cardId,
            string userId,
            SetVirtualCardLimitsDto model,
            CancellationToken ct = default
            );

        Task<VirtualCardDto> GetByIdAsync(
            int cardId,
            string userId,
            CancellationToken ct = default
            );
        Task<PaginatedResult<VirtualCardDto>> GetAllAsync(
            string userId,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default);

        Task FreezeAsync(int cardId, string userId, CancellationToken cancellationToken = default);
        Task UnfreezeAsync(int cardId, string userId, CancellationToken cancellationToken = default);
        Task CloseAsync(int cardId, string userId, CancellationToken cancellationToken = default);


    }
}
