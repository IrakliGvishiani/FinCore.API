using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinCore.Controllers
{
    [Route("api/payments")]
    [Authorize(Roles = Roles.Customer)]
    public class SimulatedPaymentController : ControllerBase
    {
        private readonly ISimulatedPaymentService _simulatedPaymentService;
        private readonly IReceiptService _receiptService;
        private readonly CommonResponse _commonResponse;

        public SimulatedPaymentController(
            ISimulatedPaymentService simulatedPaymentService,
            CommonResponse commonResponse,
            IReceiptService receiptService
            )
        {
            _simulatedPaymentService = simulatedPaymentService;
            _commonResponse = commonResponse;
            _receiptService = receiptService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Pay(
            [FromBody] CreatePaymentDto model,
            CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _simulatedPaymentService.PayAsync(
                userId,
                model,
                ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _simulatedPaymentService.GetAllAsync(
                userId, pageNumber, pageSize, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("{paymentId:int}")]
        public async Task<IActionResult> GetById(
            int paymentId,
            CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _simulatedPaymentService.GetByIdAsync(
                userId, paymentId, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("card/{cardId:int}")]
        public async Task<IActionResult> GetByCard(
            int cardId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await _simulatedPaymentService.GetByCardAsync(
                userId, cardId, pageNumber, pageSize, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("{paymentId:int}/receipt")]
        public async Task<IActionResult> DownloadReceipt(
            int paymentId,
            CancellationToken ct 
            )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var pdfBytes = await _receiptService.GeneratePaymentReceiptAsync(
                userId,
                paymentId,
                ct
                );

            var result = File(
                pdfBytes,
                "application/pdf",
                $"Fincore-Payment-Receipt-{paymentId}.pdf"
                );

            return this.ToActionResult(_commonResponse.Success(result));
        }
    }
}
