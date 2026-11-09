using FinCore.Application.Models.SavedRecipientDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Persistence
{
    public interface ISavedRecipientRepository
    {
        Task<IReadOnlyList<SavedRecipientDto>> GetAllWithUserInfoAsync(string userId, CancellationToken ct = default);
    }
}
