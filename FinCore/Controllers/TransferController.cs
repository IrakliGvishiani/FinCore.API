using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FinCore.Controllers
{

    public record CreateTransferRequest(
         [Required] string FromAccountNumber,
    [Required] string ToAccountNumber,
    [Range(0.01, 1_000_000)] decimal Amount,
    [StringLength(500)] string? Description);

    [Route("api/transfers")]
    [ApiController]
    [Authorize(Roles = "User")]
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

        [HttpPost]

        public async Task<IActionResult> Execute(
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromBody] CreateTransferRequest request,
            CancellationToken ct
            )
        {
            var result = await _transferService.ExecuteTransferAsync(
                GetUserId(),request.FromAccountNumber,
                request.ToAccountNumber,
                request.Amount,
                idempotencyKey,
                request.Description,
                ct
                );

            return this.ToActionResult(_commonResponse.Success(result));
        }

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
    }
}
