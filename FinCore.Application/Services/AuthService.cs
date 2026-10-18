using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.AuthModels;
using FinCore.Domain.Entities;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FinCore.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IRedisService _redisService;
        private readonly IMapper _mapper;

        private const string _adminRole = "Admin";
        private const string _guestRole = "User";

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled);

        private static readonly Regex PhoneRegex = new Regex(
            @"^\+995\d{9}$",
            RegexOptions.Compiled);

        private static readonly Regex PersonalNumberRegex = new Regex(
            @"^\d{11}$",
            RegexOptions.Compiled);

        public AuthService(IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtTokenGenerator jwtTokenGenerator,
            IMapper mapper,
            IEmailService emailService,
            IRedisService redisService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _mapper = mapper;
            _emailService = emailService;
            _redisService = redisService;
            _configuration = configuration;
        }


        public async Task<string> RegisterAdminAsync(UserRegistrationDto model)
        {
            var user = await RegisterAsync(model, _adminRole);
            await SendConfirmationCodeAsync(user);
            return user.Id;
        }

        public async Task<string> RegisterUserAsync(UserRegistrationDto model)
        {
            var user = await RegisterAsync(model, _guestRole);

            await SendConfirmationCodeAsync(user);

            return user.Id;
        }

        

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null)
                throw new NotFoundException("User not found.");

            if (!user.EmailConfirmed)
                throw new BadRequestException("Email is not confirmed.");

            if (await _userManager.IsLockedOutAsync(user))
                throw new BadRequestException("Your account is locked.");

            bool isValid = await _userManager.CheckPasswordAsync(user, model.Password);

            if (!isValid)
            {
                await _userManager.AccessFailedAsync(user);
                throw new BadRequestException("Username or Password is Incorrect!");
            }
            else
            {
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            var roles = await _userManager.GetRolesAsync(user);

            return await GenerateTokenPairAsync(user, roles);



        }

        private async Task<LoginResponseDto> GenerateTokenPairAsync(ApplicationUser user, IList<string> roles)
        {
            var accessToken = _jwtTokenGenerator.GenerateToken(user, roles);
            var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

            await _redisService.SetAsync(
               $"refresh-token:{refreshToken}",
               user.Id,
               TimeSpan.FromDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"])));

            return new LoginResponseDto
          {
              AccessToken = accessToken,
              RefreshToken = refreshToken
          };
        }

        

        public async Task ConfirmEmailAsync(ConfirmEmailDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);


            if (user == null)
                throw new NotFoundException("User not found.");

            if (user.EmailConfirmed)
                throw new BadRequestException("Email is already confirmed.");

            var key = $"email-confirm:{user.Id}";

            var savedCode = await _redisService.GetAsync(key);

            if (savedCode == null)
                throw new BadRequestException("Verification code has expired.");

            if (savedCode != model.Code)
                throw new BadRequestException("Invalid verification code.");

            user.EmailConfirmed = true;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                throw new Exception("Failed to confirm email.");

            await _redisService.RemoveAsync(key);
        }

        private async Task SendConfirmationCodeAsync(ApplicationUser user)
        {
            var code = RandomNumberGenerator
            .GetInt32(100000, 1_000_000)
            .ToString();

            await _redisService.SetAsync(
                $"email-confirm:{user.Id}",
                code,
                TimeSpan.FromMinutes(5));

            //var baseUrl = _configuration["AppSettings:BaseUrl"];

            //var confirmationLink =
            // $"{baseUrl}/confirm-email?email={Uri.EscapeDataString(user.Email)}";

            await _emailService.Send(
                user.Email,
                "Email Confirmation",
                $"""
        <h3>Your confirmation code is: <strong>{code}</strong></h3>

        <p>This code will expire in 5 minutes.</p>

        <p>Enter the code on the confirmation page:</p>

        <a href=""
           style="
               background-color:#2563eb;
               color:white;
               padding:10px 20px;
               text-decoration:none;
               border-radius:5px;
               display:inline-block;">
            Confirm Email
        </a>

        <p style="margin-top:20px;">
            Didn't receive a code? You can request a new one from the confirmation page.
        </p>
        """);
        }

        private async Task<ApplicationUser> RegisterAsync(UserRegistrationDto model, string role)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync();
                if (string.IsNullOrEmpty(model.PersonalNumber))
                    throw new BadRequestException("Personal number is required");

                if (model.PersonalNumber.Length != 11)
                    throw new BadRequestException("Personal number must be 11 characters long");

                if (!PersonalNumberRegex.IsMatch(model.PersonalNumber))
                    throw new BadRequestException("Personal number must contain only digits");

                if (string.IsNullOrEmpty(model.Email))
                    throw new BadRequestException("Email is required");

                if (!EmailRegex.IsMatch(model.Email))
                    throw new BadRequestException("Invalid email format");

                if (string.IsNullOrEmpty(model.PhoneNumber))
                    throw new BadRequestException("Phone number is required");

                if (!PhoneRegex.IsMatch(model.PhoneNumber))
                    throw new BadRequestException("Phone number must be +995XXXXXXXXX format");

                if (await _userManager.Users.AnyAsync(u => u.PersonalNumber == model.PersonalNumber))
                    throw new BadRequestException("Choose different personal number");

                if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber))
                    throw new BadRequestException("Choose different phone number");

                if (await _userManager.Users.AnyAsync(u => u.Email == model.Email))
                    throw new BadRequestException("Choose different email");

                var user = _mapper.Map<ApplicationUser>(model);

                var result = await _userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded)
                    throw new BadRequestException(
                      result.Errors.First().Description);

               var roleResult = await _userManager.AddToRoleAsync(user, role);

                if (!roleResult.Succeeded)
                {
                    throw new BadRequestException(
                        roleResult.Errors.First().Description);
                }

                await _unitOfWork.CommitTransactionAsync();

                return user;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<LoginResponseDto> RefreshTokenAsync(string token)
        {
            var userId = await _redisService.GetAsync(
       $"refresh-token:{token}");

            if (string.IsNullOrEmpty(userId))
                throw new BadRequestException("Invalid or expired refresh token.");

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                throw new NotFoundException("User not found.");


            await _redisService.RemoveAsync(
                $"refresh-token:{token}");

            var roles = await _userManager.GetRolesAsync(user);

            return await GenerateTokenPairAsync(user, roles);
        }

        public async Task RevokeToken(string refreshToken)
        {
            var storedToken = await _redisService.GetAsync(
                 $"refresh-token:{refreshToken}");

            if (string.IsNullOrEmpty(storedToken))
                throw new BadRequestException("Invalid or expired refresh token.");

            await _redisService.RemoveAsync(
                $"refresh-token:{refreshToken}");
        }

        public async Task ResendConfirmationCodeAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new BadRequestException("Email is required.");

            var normalizedEmail = email.Trim();

            var user = await _userManager.FindByEmailAsync(normalizedEmail);

            if (user == null)
                throw new NotFoundException("User not found.");

            if (user.EmailConfirmed)
                throw new BadRequestException("Email is already confirmed.");

            var allowed = await _redisService.TryAcquireRateLimitAsync(
                "confirmation-resend",
                user.Id,
                5,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromHours(1));

            if (!allowed)
            {
                throw new BadRequestException(
                    "Please wait before requesting another confirmation code. " +
                    "The limit is one request every 60 seconds and five requests per hour.");
            }

            await SendConfirmationCodeAsync(user);
        }

        public async Task ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new BadRequestException("Email is required.");

            var user = await _userManager.FindByEmailAsync(email.Trim());

            if (user == null)
                throw new NotFoundException("User not found.");

            var allowed = await _redisService.TryAcquireRateLimitAsync(
                keyPrefix: "password-reset",
                userId: user.Id,
                maxRequests: 5,
                cooldown: TimeSpan.FromSeconds(60),
                window: TimeSpan.FromHours(1));

            if (!allowed)
            {
                throw new BadRequestException(
                    "Please wait before requesting another password reset email. " +
                    "The limit is one request every 60 seconds and five requests per hour.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));

            //var baseUrl = _configuration["AppSettings:BaseUrl"];

            //if (string.IsNullOrWhiteSpace(baseUrl))
            //    throw new InvalidOperationException(
            //        "AppSettings:BaseUrl is not configured.");

            //var resetLink =
            //    $"{baseUrl.TrimEnd('/')}/reset-password" +
            //    $"?email={Uri.EscapeDataString(user.Email!)}" +
            //    $"&token={Uri.EscapeDataString(encodedToken)}";

            var emailBody = $"""
        <html>
        <body style="font-family: Arial, sans-serif; color: #1f2937;">
            <h2>Reset Your Password</h2>
            <p>We received a request to reset your password.</p>
            <p>{encodedToken}<p>
            <p>
                <a href=""
                   style="display:inline-block;padding:12px 24px;
                          background-color:#2563eb;color:#ffffff;
                          text-decoration:none;border-radius:6px;">
                    Reset Password
                </a>
            </p>
            <p>If you did not request a password reset, ignore this email.</p>
        </body>
        </html>
        """;

            await _emailService.Send(
                user.Email!,
                "Reset Password",
                emailBody);
        }

        public async Task ResetPasswordAsync(ResetPasswordDto model)
        {
                        var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                throw new NotFoundException("User not found.");

            string token;

            try
            {
                token = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(model.Token));
            }
            catch
            {
                throw new BadRequestException("Invalid reset token.");
            }

            var result = await _userManager.ResetPasswordAsync(
                user,
                token,
                model.NewPassword);

            if (!result.Succeeded)
            {
                throw new BadRequestException(
                    result.Errors.First().Description);
            }
        }
    }
}
