using FinCore.Application.Models.AuthModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IAuthService
    {
        Task<string> RegisterUserAsync(UserRegistrationDto model);

        Task<string> RegisterAdminAsync(UserRegistrationDto model);

        Task<LoginResponseDto> LoginAsync(LoginRequestDto model);

        Task ConfirmEmailAsync(ConfirmEmailDto model);

        Task<LoginResponseDto> RefreshTokenAsync(string token);

        Task RevokeToken(string refreshToken);

        Task ResendConfirmationCodeAsync(string email);

        Task ForgotPasswordAsync(string email);

        Task ResetPasswordAsync(ResetPasswordDto model);
    }
}
