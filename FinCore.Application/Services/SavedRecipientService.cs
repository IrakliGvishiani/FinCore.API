using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.SavedRecipientDtos;
using FinCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class SavedRecipientService : ISavedRecipientService
    {
        private readonly IRepositoryBase<SavedRecipient> _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISavedRecipientRepository _savedRecipientRepository;


        public SavedRecipientService(IRepositoryBase<SavedRecipient> repository, IUnitOfWork unitOfWork,
            ISavedRecipientRepository savedRecipientRepository)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _savedRecipientRepository = savedRecipientRepository;
        }

        public async Task<SavedRecipientDto> CreateAsync(string userId, 
            CreateSavedRecipientDto model, CancellationToken ct = default)
        {
            var accNumber = model.AccountNumber.Trim();

            var alreadyExists = await _repository.ExistsAsync(
                r => r.UserId == userId && r.AccountNumber == accNumber, ct);

            if (alreadyExists)
                throw new BadRequestException("This Recipient is already saved.");

            var recipient = SavedRecipient.Create(userId, model.Name, accNumber);

            await _repository.AddAsync(recipient, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return MapToDto(recipient);
        }

        public async Task DeleteAsync(int id, string userId, CancellationToken ct = default)
        {
            var recipient = await _repository.GetAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken: ct
                );

            if (recipient == null)
                throw new NotFoundException("Recipient not found.");

            _repository.Remove(recipient);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<SavedRecipientDto>> GetAllAsync(string userId, CancellationToken ct = default)
        {
            return await _savedRecipientRepository.GetAllWithUserInfoAsync(userId, ct);
        }

        public async Task UpdateAsync(int id, string userId, UpdateSavedRecipientDto model, CancellationToken ct = default)
        {
            var recipient = await _repository.GetAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken: ct
                );

            if (recipient == null)
                throw new NotFoundException("Recipient not found.");

            recipient.UpdateName(model.Name);
            await _unitOfWork.SaveChangesAsync(ct);
        }


        private static SavedRecipientDto MapToDto(SavedRecipient recipient)
        {
            return new SavedRecipientDto
            {
                Id = recipient.Id,
                Name = recipient.Name,
                AccountNumber = recipient.AccountNumber,
                CreatedAt = recipient.CreatedAt
            };
        }
    }
}
