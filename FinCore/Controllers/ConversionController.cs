using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinCore.Controllers
{
    [Route("api/conversions")]
    [ApiController]
    [Authorize(Roles = Roles.Customer)]
    public class ConversionController : Controller
    {
        private readonly IConversionService _conversionService;
        private readonly CommonResponse _commonResponse;
        private readonly IReceiptService _receipt;
        public ConversionController(
            IConversionService conversionService,
            CommonResponse commonResponse,
            IReceiptService receiptService
            )
        {
            _commonResponse = commonResponse;
            _conversionService = conversionService;
            _receipt = receiptService;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new NotFoundException("User ID not found");

        [HttpPost]

        public async Task<IActionResult> Convert(
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromBody]ConvertRequest request,
            CancellationToken ct
            )
        {
            var result = await _conversionService.ConvertAsync(
                GetUserId(),request.FromAccountNumber,
                request.ToAccountNumber,
                request.Amount,idempotencyKey,request.ExpectedRate,ct
                );

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpPost("quote")]

        public async Task<IActionResult> Quote(
            [FromBody] ConversionQuoteRequest request,
            CancellationToken ct
            )
        {
            var result = await _conversionService.QuoteAsync(
                GetUserId(),request.FromAccountNumber,request.ToAccountNumber,request.Amount,ct
                );

            return this.ToActionResult(_commonResponse.Success(result));
        }
        [HttpGet]
        public async Task<IActionResult> GetMyConversions(
            [FromQuery] ConversionFilter filter,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default
            )
        {
            var (items,totalcount) = await _conversionService.GetMyConversionsAsync(
                GetUserId(),
                filter,
                pageNumber,
                pageSize,
                ct
                );

            return this.ToActionResult(_commonResponse.Success(new { items, totalcount }));
        }

        [HttpGet("{conversionId:int}/receipt")]
        public async Task<IActionResult> DownloadReceipt(
            int conversionId,
            CancellationToken ct
            )
        {
            var pdfBytes = await _receipt.GenerateCurrencyConversionReceiptAsync(
                GetUserId(),
                conversionId,
                ct
                );

            var result = File(
                pdfBytes,
                "application/pdf",
                $"Fincore-Conversion-Receipt-{conversionId}.pdf"
                );

            return this.ToActionResult(_commonResponse.Success(result));
        }
    }
}
