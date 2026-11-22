using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinCore.Controllers
{
    [Route("api/card")]
    [ApiController]
    [Authorize(Roles = Roles.Customer)]
    public class VirtualCardController : ControllerBase
    {
        private readonly IVirtualCardService _cardService;
        private readonly CommonResponse _commmon;


        public VirtualCardController(
            IVirtualCardService virtualCardService,
            CommonResponse commonResponse
            )
        {
            _cardService = virtualCardService;
            _commmon = commonResponse;
        }

        private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new NotFoundException("User ID not found");

        [HttpPost]
        public async Task<IActionResult> CreateVirtualCard(
            [FromBody] CreateVirtualCardDto model,
            CancellationToken ct = default
            )
        {
            var result = await _cardService.CreateAsync(
                GetUserId(),
                model,
                ct
                );

            return this.ToActionResult(_commmon.Success(result));
        }

        [HttpGet("{cardId:int}")]
        public async Task<IActionResult> GetById(int cardId,CancellationToken ct = default)
        {
            var result = await _cardService.GetByIdAsync(cardId,GetUserId(),ct);

            return this.ToActionResult(_commmon.Success(result));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            CancellationToken ct = default
            )
        {
            var result = await _cardService.GetAllAsync(GetUserId(),pageNumber,pageSize,ct);

            return this.ToActionResult(_commmon.Success(result));
        }

        [HttpPatch("{cardId:int}/limits")]
        public async Task<IActionResult> SetLimits(
            int cardId,
            [FromBody] SetVirtualCardLimitsDto model,
            CancellationToken ct = default
            )
        {
            await _cardService.SetLimitAsync(cardId,GetUserId(),model,ct);

            return this.ToActionResult(_commmon.NoContent());
        }

        [HttpPatch("{cardId:int}/freeze")]
        public async Task<IActionResult> Freeze(int cardId,CancellationToken ct = default)
        {
            await _cardService.FreezeAsync(cardId,GetUserId(),ct);

            return this.ToActionResult(_commmon.NoContent());
        }

        [HttpPatch("{cardId:int}/unfreeze")]

        public async Task<IActionResult> Unfreeze(int cardId,CancellationToken ct = default)
        {
            await _cardService.UnfreezeAsync(cardId,GetUserId(),ct);

            return this.ToActionResult(_commmon.NoContent());
        }

        [HttpPatch("{cardId:int}/auto-conversion")]
        public async Task<IActionResult> SetAutoConversion(
            int cardId,
            [FromBody] SetAutoConversionDto model,
            CancellationToken ct = default
            )
        {
            var result = await _cardService.SetAutoConversionAsync(cardId, GetUserId(), model, ct);

            return this.ToActionResult(_commmon.Success(result));
        }

        [HttpPatch("{cardId:int}/close")]

        public async Task<IActionResult> Close(int cardId,CancellationToken ct = default)
        {
            await _cardService.CloseAsync(cardId,GetUserId(),ct);

            return this.ToActionResult(_commmon.NoContent());
        }

    }
}

