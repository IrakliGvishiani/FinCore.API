using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Cloudinary;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class CloudinaryImageService(Cloudinary cloudinary) : ICloudinaryImageService
    {
        #region Delete
        public async Task<bool> DeleteAsync(string publicId, bool invalidateCdn = true, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return false;

            var deletionParams = new DeletionParams(publicId)
            {
                Invalidate = invalidateCdn
            };

            var result = await cloudinary.DestroyAsync(deletionParams);

            return string.Equals(result.Result, "ok", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Update
        public async Task<ImageUploadResultDto> UpdateAsync(string publicId, int width, int height, IFormFile newFile, bool invalidateCdn = true, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                throw new BadRequestException("Public Id is required while uploading a file on cloudinary");

            return await UploadInternalAsync(
                newFile,
                width,
                height,
                folder: null,
                overwrite: true,
                invalidateCdn: invalidateCdn,
                publicId: publicId,
                ct
                );
        }

        #endregion


        #region Upload
        public async Task<ImageUploadResultDto> UploadAsync(IFormFile file, int width, int height, string folder = null, CancellationToken ct = default)
        {
            return await UploadInternalAsync(
                file,
                width,
                height,
                folder,
                overwrite: false,
                invalidateCdn: false,
                publicId: null,
                ct
                );
        }

        #endregion

        #region UploadInternal(private)
        private async Task<ImageUploadResultDto> UploadInternalAsync(
            IFormFile file,
            int width,
            int height,
            string folder,
            bool overwrite,
            bool invalidateCdn,
            string publicId,
            CancellationToken ct
            )

        {
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("UploadInternalAsync, File is required to upload on Cloudinary!");
            }

            await using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = overwrite,
                Invalidate = invalidateCdn,
                PublicId = publicId,
                Transformation = new Transformation().Width(width).Height(height).Crop("fill").Gravity("face").Quality("auto")

            };

            var uploadResult = await cloudinary.UploadAsync(uploadParams, ct);

            if (uploadResult.Error != null)
            {
                throw new InternalServerException($"{uploadResult.Error.Message}");
            }

            return MapUploadResult(uploadResult);
        }

        #endregion


        #region Map and Upload Result(private)
        private ImageUploadResultDto MapUploadResult(ImageUploadResult uploadResult)
        {
            return new()
            {
                publicId = uploadResult.PublicId,
                Url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString() ?? string.Empty,
                Width = uploadResult.Width,
                Height = uploadResult.Height,
                Bytes = uploadResult.Bytes
            };
        }

        #endregion
    }
}
