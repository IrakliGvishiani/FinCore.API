using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.UserDtos;
using FinCore.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserManagementService(
            UserManager<ApplicationUser> userManager
            )
        {
            _userManager = userManager;
        }

        public async Task<UserDetailsDto> GetUserByIdAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException("user ID is required");

            var user = await _userManager.Users
                .AsNoTracking()
                .Include(x => x.BankAccounts)
                .FirstOrDefaultAsync(x => x.Id == userId,ct);

            if (user is null)
                throw new NotFoundException("User not found");

            var roles = await _userManager.GetRolesAsync(user);


            return new UserDetailsDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                PersonalNumber = user.PersonalNumber,
                PhoneNumber = user.PhoneNumber,
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                CreatedAt = user.CreatedAt,
                Roles = roles,

                BankAccounts = user.BankAccounts
                .Select(account => new UserBankAccountDto
                {
                    AccountNumber = account.AccountNumber,
                    Currency = account.Currency.ToString(),
                    Balance = account.Balance,
                    Status = account.Status.ToString(),
                    CreatedAt = account.CreatedAt
                })
                .ToList()
            };
        }

        public async Task<(IEnumerable<UserListItemDto> Items, int TotalCount)> GetUsersAsync(string? search,
            int pageNumber, int pageSize, CancellationToken ct = default)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(pageNumber), "Page number must be at least 1.");

            if (pageSize < 1 || pageSize > 100)
                throw new ArgumentOutOfRangeException(
                    nameof(pageSize), "Page size must be between 1 and 100.");

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(u =>
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                (u.Email != null && u.Email.Contains(search)) ||
                u.PersonalNumber.Contains(search)
                );
            }

            var totalCount = await query.CountAsync(ct);

            var users = await query
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ThenBy(u => u.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = new List<UserListItemDto>(users.Count);

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                items.Add(new UserListItemDto
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email ?? string.Empty,
                    PersonalNumber = user.PersonalNumber,
                    PhoneNumber = user.PhoneNumber,
                    ProfilePhotoUrl = user.ProfilePhotoUrl,
                    CreatedAt = user.CreatedAt,
                    Roles = roles
                });
            }

            return (items, totalCount);
        }
    }
}
