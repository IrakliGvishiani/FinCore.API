using FinCore.Application.Models.ProfileDtos;
using FinCore.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IProfileService
    {
        Task<string> SetPhotoAsync(string userId, IFormFile file, CancellationToken ct = default);
        Task DeletePhotoAsync(string userId, CancellationToken ct = default);

        Task<ProfileDto> GetProfileAsync(string userId, CancellationToken ct = default);

        Task<ProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto model, CancellationToken ct = default);

    }
}
