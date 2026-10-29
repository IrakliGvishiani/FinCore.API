using FinCore.Application.Models.Cloudinary;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface ICloudinaryImageService
    {
        Task<ImageUploadResultDto> UploadAsync(
    IFormFile file,
    int width,
    int height,
    string folder = null,
    CancellationToken ct = default
    );

        Task<ImageUploadResultDto> UpdateAsync(
            string publicId,
            int width,
            int height,
            IFormFile newFile,
            bool invalidateCdn = true,
            CancellationToken ct = default
            );

        Task<bool> DeleteAsync(
            string publicId,
            bool invalidateCdn = true,
            CancellationToken ct = default
            );
    }
}
