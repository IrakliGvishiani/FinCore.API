using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.ProfileDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinCore.Controllers
{
    [Route("api/profile")]
    [ApiController]
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IProfileService _profileService;
        private readonly CommonResponse _commonResponse;

        public ProfileController(
            IProfileService profileService,
            CommonResponse commonResponse
            )
        {
            _profileService = profileService;
            _commonResponse = commonResponse;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User ID not found.");
        }
        [HttpPut("photo")]
        [RequestSizeLimit(3_000_000)] 
        public async Task<IActionResult> SetPhoto(IFormFile file, CancellationToken ct)
        {
            var url = await _profileService.SetPhotoAsync(GetUserId(), file, ct);
            return this.ToActionResult(_commonResponse.Success(new { photoUrl = url }));
        }
        [HttpDelete("photo")]
        public async Task<IActionResult> DeletePhoto(CancellationToken ct)
        {
            await _profileService.DeletePhotoAsync(GetUserId(), ct);
            return this.ToActionResult(_commonResponse.NoContent());
        }

        [HttpGet]
        public async Task<IActionResult> GetProfile(CancellationToken ct)
        {
            var profile = await _profileService.GetProfileAsync(GetUserId(), ct);
            return this.ToActionResult(_commonResponse.Success(profile));
        }

        [HttpPatch]
        public async Task<IActionResult> UpdateProfile(
            [FromQuery] UpdateProfileDto request,
            CancellationToken ct
            )
        {
            var updatedProfile = await _profileService.UpdateProfileAsync(GetUserId(),request,ct);

            return this.ToActionResult(_commonResponse.Success(updatedProfile));
        }


    }
}
