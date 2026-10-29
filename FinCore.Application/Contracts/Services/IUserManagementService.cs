using FinCore.Application.Models.UserDtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IUserManagementService
    {
        Task<(IEnumerable<UserListItemDto> Items, int TotalCount)> GetUsersAsync(
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default
            );

        Task<UserDetailsDto> GetUserByIdAsync(
            string userId,
            CancellationToken ct = default
            );
    }
}
