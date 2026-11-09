using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.Common;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using MapsterMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class VirtualCardService : IVirtualCardService
    {
        private readonly IRepositoryBase<VirtualCard> _repo;
        private readonly IRepositoryBase<BankAccount> _account;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRepositoryBase<SimulatedPayment> _payments;
        private readonly IMapper _mapper;

        public VirtualCardService(
            IRepositoryBase<VirtualCard> repositoryBase,
            IRepositoryBase<BankAccount> repositoryBase1,
            IUnitOfWork unitOfWork,
            IRepositoryBase<SimulatedPayment> payments,
            IMapper mapper
            )
        {
            _repo = repositoryBase;
            _account = repositoryBase1;
            _unitOfWork = unitOfWork;
            _payments = payments;
            _mapper = mapper;
        }



        public async Task<VirtualCardCreatedDto> CreateAsync(string userId, 
            CreateVirtualCardDto model, CancellationToken ct = default)
        {

            if (model.BankAccountId == null)
                throw new BadRequestException("Bank account Id is required");

    
            var account = await _account.GetAsync(
                x => x.Id == model.BankAccountId && x.UserId == userId,
                cancellationToken: ct
                );

            if (account is null)
                throw new NotFoundException("Bank account not found");

            var cardNumber = CardNumberGenerator.Generate();

            var last4 = cardNumber[^4..];

            var card = VirtualCard.Create(
                userId,
                account,
                model.Name,
                last4,
                model.DailyLimit,
                model.TransactionLimit,
                DateTime.UtcNow
                );

            await _repo.AddAsync(card,ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new VirtualCardCreatedDto
            {
                Id = card.Id,
                CardNumber = cardNumber,
                Last4Digits = last4,
                ExpiryMonth = card.ExpiryMonth,
                ExpiryYear = card.ExpiryYear,
                DailyLimit = card.DailyLimit,
                TransactionLimit = card.TransactionLimit
            };
        }

        public async Task FreezeAsync(int cardId, string userId, CancellationToken cancellationToken = default)
         => await UpdateCardStatusAsync(cardId,userId,card => card.Freeze(),cancellationToken);

        public async Task UnfreezeAsync(int cardId, string userId, CancellationToken cancellationToken = default)
         => await UpdateCardStatusAsync(cardId,userId,card => card.Unfreeze(),cancellationToken);

        public async Task CloseAsync(int cardId, string userId, CancellationToken cancellationToken = default)
         => await UpdateCardStatusAsync(cardId,userId,card => card.Close(),cancellationToken);

        private async Task UpdateCardStatusAsync(
            int cardId,
            string userId,
            Action<VirtualCard> action,
            CancellationToken ct
            )
        {
            var card = await _repo.GetAsync(
                x => x.Id == cardId && x.UserId == userId,
                cancellationToken: ct
                );

            if (card is null)
                throw new NotFoundException("Virtual card not found");

            action(card);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task SetLimitAsync(int cardId, string userId, SetVirtualCardLimitsDto model,
            CancellationToken ct = default)
        {
            var card = await _repo.GetAsync(
                x => x.Id == cardId && x.UserId == userId,
                cancellationToken: ct
                );

            if (card is null)
                throw new NotFoundException("Virtual card not found");

            card.SetLimits(model.DailyLimit,model.TransactionLimit);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<VirtualCardDto> GetByIdAsync(int cardId, string userId, CancellationToken ct = default)
        {
            var card = await _repo.GetAsync(
                x => x.Id == cardId && x.UserId == userId,
                tracking: false,
                cancellationToken: ct
                );

            if (card is null)
                throw new NotFoundException("Virtual card not found");

            return _mapper.Map<VirtualCardDto>(card);
        }

        public async Task<PaginatedResult<VirtualCardDto>> GetAllAsync(
            string userId,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var (cards, totalCount) = await _repo.GetAllAsync(
                filter: x => x.UserId == userId,
                pageNumber: pageNumber,
                pageSize: pageSize,
                orderBy: x => x.CreatedAt,
                ascending: false,
                tracking: false,
                cancellationToken: cancellationToken);

            var items = cards.Select(MapToDto).ToList();

            return new PaginatedResult<VirtualCardDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = Math.Max(pageNumber, 1),
                PageSize = Math.Clamp(pageSize, 1, 100)
            };
        }

        private static VirtualCardDto MapToDto(VirtualCard card)
        {
            return new VirtualCardDto
            {
                Id = card.Id,
                BankAccountId = card.BankAccountId,
                Name = card.Name,
                Last4Digits = card.Last4Digits,
                ExpiryMonth = card.ExpiryMonth,
                ExpiryYear = card.ExpiryYear,
                Status = card.Status,
                DailyLimit = card.DailyLimit,
                TransactionLimit = card.TransactionLimit,
                CreatedAt = card.CreatedAt
            };
        }
    }
}
