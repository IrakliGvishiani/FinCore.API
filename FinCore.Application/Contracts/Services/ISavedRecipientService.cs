using FinCore.Application.Models.SavedRecipientDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface ISavedRecipientService
    {

        Task<SavedRecipientDto> CreateAsync(string userId,
            CreateSavedRecipientDto model,
            CancellationToken ct = default);

        Task<IReadOnlyList<SavedRecipientDto>> GetAllAsync(
            string userId,
            CancellationToken ct = default
            );

        Task UpdateAsync(
            int id,
            string userId,
            UpdateSavedRecipientDto model,
            CancellationToken ct = default
            );

        Task DeleteAsync(
            int id,
            string userId,
            CancellationToken ct = default
            );
    }
}
