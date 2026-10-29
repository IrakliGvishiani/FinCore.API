using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.TransferDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FinCore.Controllers
{



    [Route("api/transfers")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class TransferController : ControllerBase
    {
        private readonly ITransferService _transferService;
        private readonly CommonResponse _commonResponse;

        public TransferController (
            ITransferService transferService,
            CommonResponse commonResponse
            )
        {
            _transferService = transferService;
            _commonResponse = commonResponse;
             
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new NotFoundException("User ID not found");

        [HttpGet]
        public async Task<IActionResult> GetMyTransfers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default
            )
        {
            var (items, totalCount) = await _transferService.GetMyTransfersAsync(
                GetUserId(),pageNumber,pageSize,ct
                );

            return this.ToActionResult(_commonResponse.Success(new {items,totalCount}));
        }

        [HttpGet("{id:int}")]

        public async Task<IActionResult> GetTransferById(int id,CancellationToken ct)
        {
            var transfer = await _transferService.GetTransfersByIdAsync(GetUserId(),id,ct);

            return this.ToActionResult(_commonResponse.Success(transfer));
        }

        //[HttpGet("recipients/{personalNumber}/accounts")]
        //public async Task<IActionResult> GetRecipientAccounts(
        //    string personalNumber,
        //    CancellationToken ct)
        //{
        //    var accounts = await _transferService.GetRecipientAccountAsync(
        //        GetUserId(),
        //        personalNumber, ct);

        //    return this.ToActionResult(_commonResponse.Success(accounts));
        //}

        [HttpPost("recipients/lookup")]
        public async Task<IActionResult> LookupRecipient(
        [FromBody] RecipientLookupRequest request, CancellationToken ct)
        {
            var accounts = await _transferService.GetRecipientAccountAsync(
                GetUserId(), request.PersonalNumber, ct);

            return this.ToActionResult(_commonResponse.Success(accounts));
        }
        [HttpPost("by-account-number")]
        public async Task<IActionResult> ByAccountNumber(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] TransferByAccountNumberRequest request,
        CancellationToken ct)
        {
            var result = await _transferService.ExecuteTransferByAccountNumberAsync(
                GetUserId(), request.FromAccountNumber, request.ToAccountNumber,
                request.Amount, idempotencyKey, request.Description, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpPost("by-recipient")]
        public async Task<IActionResult> ByRecipient(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] TransferByRecipientRequest request,
        CancellationToken ct)
        {
            var result = await _transferService.ExecuteTransferByRecipientAccountIdAsync(
                GetUserId(), request.FromAccountNumber, request.ToAccountId,
                request.Amount, idempotencyKey, request.Description, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

    }
}
