using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.AuthModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Filters;
using static FinCore.Examples;

namespace FinCore.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly CommonResponse _commonResponse;

        public AuthController(IAuthService authService, CommonResponse commonResponse)
        {
            _authService = authService;
            _commonResponse = commonResponse;
        }

        [HttpPost("register")]
        [SwaggerRequestExample(typeof(UserRegistrationDto),typeof(UserRegitrationDtoExample))]
        public async Task<IActionResult> RegisterUser([FromBody] UserRegistrationDto request)
        {
            var user = await _authService.RegisterUserAsync(request);
            return this.ToActionResult(_commonResponse.Created(user));
        }

        [HttpPost("register-admin")]
        public async Task<IActionResult> RegisterAdmin([FromBody] UserRegistrationDto request)
        {
            var admin = await _authService.RegisterAdminAsync(request);
            return this.ToActionResult(_commonResponse.Created(admin));
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("register-manager")]
        public async Task<IActionResult> RegisterManager([FromBody] UserRegistrationDto request)
        {
            var manager = await _authService.RegisterManagerAsync(request);
            return this.ToActionResult(_commonResponse.Created(manager));
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailDto model)
        {
            await _authService.ConfirmEmailAsync(model);

            return this.ToActionResult(_commonResponse.Success(null));
        }

        [HttpGet("resend-confirmation-code")]
        public async Task<IActionResult> ResendConfirmationCode(string email)
        {
            await _authService.ResendConfirmationCodeAsync(email);

            return this.ToActionResult(_commonResponse.Success(null));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
        {
            var response = await _authService.LoginAsync(model);
            return this.ToActionResult(_commonResponse.Success(response));
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] string token)
        {
            var response = await _authService.RefreshTokenAsync(token);
            return this.ToActionResult(_commonResponse.Success(response));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
    [FromBody] string email)
        {
            await _authService.ForgotPasswordAsync(email);

            return this.ToActionResult(_commonResponse.Success(null));
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
    [FromBody] ResetPasswordDto model)
        {
            await _authService.ResetPasswordAsync(model);

            return this.ToActionResult(_commonResponse.Success(null));
        }

        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] string refreshToken)
        {
            await _authService.RevokeToken(refreshToken);

            return this.ToActionResult(_commonResponse.Success(null));
        }
    }
}
