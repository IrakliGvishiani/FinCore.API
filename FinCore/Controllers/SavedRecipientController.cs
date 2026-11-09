using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.SavedRecipientDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinCore.Controllers
{
    [Route("api/recipient")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class SavedRecipientController : ControllerBase
    {
       private readonly ISavedRecipientService _savedRecipientService;
        private readonly CommonResponse _commonResponse;
        public SavedRecipientController(ISavedRecipientService savedRecipientService, CommonResponse commonResponse)
        {
            _savedRecipientService = savedRecipientService;
            _commonResponse = commonResponse;
        }
        [HttpPost]
        public async Task<IActionResult> CreateSavedRecipient([FromBody] CreateSavedRecipientDto model,
            CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var savedRecipient = await _savedRecipientService.CreateAsync(userId, model, ct);
            return this.ToActionResult(_commonResponse.Created(savedRecipient));
        }

        [HttpGet]
        public async Task<IActionResult> GetSavedRecipients(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var savedRecipients = await _savedRecipientService.GetAllAsync(userId, ct);
            return this.ToActionResult(_commonResponse.Success(savedRecipients));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateSavedRecipient(int id, [FromBody] UpdateSavedRecipientDto model, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _savedRecipientService.UpdateAsync(id,userId,model,ct);
            return this.ToActionResult(_commonResponse.Success("updated successfully"));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSavedRecipient(int id, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _savedRecipientService.DeleteAsync(id,userId,ct);
            return this.ToActionResult(_commonResponse.Success("deleted successfully"));
        }
    }
}
