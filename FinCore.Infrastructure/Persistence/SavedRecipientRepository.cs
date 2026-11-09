using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Models.SavedRecipientDtos;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Infrastructure.Persistence
{
    public class SavedRecipientRepository : ISavedRecipientRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public SavedRecipientRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<SavedRecipientDto>> GetAllWithUserInfoAsync(string userId, CancellationToken ct = default)
        {
            return await (
                from recipient in _dbContext.SavedRecipients.AsNoTracking()

                join account in _dbContext.BankAccounts.AsNoTracking()
                on recipient.AccountNumber equals account.AccountNumber
                into accounts

                from account in accounts.DefaultIfEmpty()

                where recipient.UserId == userId

                select new SavedRecipientDto
                {
                 Id = recipient.Id,
                Name = recipient.Name,
                AccountNumber = recipient.AccountNumber,
                CreatedAt = recipient.CreatedAt,

                FirstName = account != null
                    ? account.User.FirstName
                    : null,

                LastName = account != null
                    ? account.User.LastName
                    : null,

                ProfilePhotoUrl = account != null
                    ? account.User.ProfilePhotoUrl
                    : null
                }
                ).OrderBy(x => x.Name)
                .ToListAsync(ct);

        }
    }
}
