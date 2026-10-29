using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.ProfileDtos;
using FinCore.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MapsterMapper;

namespace FinCore.Application.Services
{
    public class ProfileService : IProfileService
    {
        private const long MaxPhotoBytes = 2 * 1024 * 1024;
        private const int AvatarSize = 256;
        private const string AvatarFolder = "fincore/avatars";
        private static readonly Regex PhoneRegex = new Regex(
            @"^\+995\d{9}$",
            RegexOptions.Compiled);

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICloudinaryImageService _images;
        

        public ProfileService(UserManager<ApplicationUser> userManager, ICloudinaryImageService images
            )
        {
            _userManager = userManager;
            _images = images;
           
        }

        public async Task<string> SetPhotoAsync(string userId, IFormFile file, CancellationToken ct = default)
        {
            await ValidateImageAsync(file, ct);

            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new NotFoundException("User not found.");

            var isReplace = !string.IsNullOrEmpty(user.ProfilePhotoPublicId);

            var uploaded = isReplace
                ? await _images.UpdateAsync(user.ProfilePhotoPublicId!, AvatarSize, AvatarSize, file, true, ct)
                : await _images.UploadAsync(file, AvatarSize, AvatarSize, AvatarFolder, ct);

            user.ProfilePhotoUrl = uploaded.Url;
            user.ProfilePhotoPublicId = uploaded.publicId;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                
                if (!isReplace)
                    await _images.DeleteAsync(uploaded.publicId, true, CancellationToken.None);

                throw new BadRequestException(result.Errors.First().Description);
            }

            return uploaded.Url;
        }
        public async Task<ProfileDto> GetProfileAsync(string userId, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new NotFoundException("User not found.");

            var roles = await _userManager.GetRolesAsync(user);

            return new ProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email!,
                PhoneNumber = user.PhoneNumber,
                MaskedPersonalNumber = MaskPersonalNumber(user.PersonalNumber),
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                Roles = roles.ToList(),
                CreatedAt = user.CreatedAt
            };
        }


        public async Task DeletePhotoAsync(string userId, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new NotFoundException("User not found.");

            if (string.IsNullOrEmpty(user.ProfilePhotoPublicId))
                return;

            await _images.DeleteAsync(user.ProfilePhotoPublicId, true, ct);

            user.ProfilePhotoUrl = null;
            user.ProfilePhotoPublicId = null;
            await _userManager.UpdateAsync(user);
        }

        private static async Task ValidateImageAsync(IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                throw new BadRequestException("File is required.");
            if (file.Length > MaxPhotoBytes)
                throw new BadRequestException("Photo must be up to 2 MB.");

            
            var header = new byte[12];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

            if (read < header.Length || !LooksLikeImage(header))
                throw new BadRequestException("Only JPEG, PNG or WebP images are allowed.");
        }
        private static bool LooksLikeImage(byte[] b)
        {
            var jpeg = b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
            var png = b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
            var webp = b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
                    && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';
            return jpeg || png || webp;
        }

        private static string MaskPersonalNumber(string personalNumber)
        {
            const int visible = 3;

            if (string.IsNullOrWhiteSpace(personalNumber) || personalNumber.Length <= visible)
                return new string('*', personalNumber?.Length ?? 0);

            return new string('*', personalNumber.Length - visible) + personalNumber[^visible..];
        }


        public async Task<ProfileDto> UpdateProfileAsync(
            string userId,
            UpdateProfileDto model,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException("User ID is required");

            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new NotFoundException("User not found");

            var roles = await _userManager.GetRolesAsync(user);

            
            if (model.FirstName is null &&
                model.LastName is null &&
                model.PhoneNumber is null)
            {
                throw new BadRequestException(
                    "At least one field must be provided");
            }

           
            if (model.FirstName is not null)
            {
                if (string.IsNullOrWhiteSpace(model.FirstName))
                    throw new BadRequestException(
                        "First name cannot be empty");

                if (model.FirstName.Length > 100)
                    throw new BadRequestException(
                        "First name cannot exceed 100 characters");

                user.FirstName = model.FirstName.Trim();
            }

            
            if (model.LastName is not null)
            {
                if (string.IsNullOrWhiteSpace(model.LastName))
                    throw new BadRequestException(
                        "Last name cannot be empty");

                if (model.LastName.Length > 100)
                    throw new BadRequestException(
                        "Last name cannot exceed 100 characters");

                user.LastName = model.LastName.Trim();
            }

           
            if (model.PhoneNumber is not null)
            {
                if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                    throw new BadRequestException(
                        "Phone number cannot be empty");

                var phoneNumber = model.PhoneNumber.Trim();

                if (!PhoneRegex.IsMatch(phoneNumber))
                    throw new BadRequestException(
                        "Phone number must be +995XXXXXXXXX format");

                var phoneExists = await _userManager.Users
                    .AnyAsync(
                        u => u.PhoneNumber == phoneNumber &&
                             u.Id != userId,
                        ct);

                if (phoneExists)
                    throw new BadRequestException(
                        "This phone number is already in use");

                user.PhoneNumber = phoneNumber;
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                throw new BadRequestException(
                    result.Errors.First().Description);
            }


            return new ProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email!,
                PhoneNumber = user.PhoneNumber,
                MaskedPersonalNumber = MaskPersonalNumber(user.PersonalNumber),
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                Roles = roles.ToList(),
                CreatedAt = user.CreatedAt
            };
        }

    }
}
