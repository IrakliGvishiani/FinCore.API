using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.ExchangeDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinCore.Controllers
{
    [Route("api/conversions")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class ConversionController : Controller
    {
        private readonly IConversionService _conversionService;
        private readonly CommonResponse _commonResponse;

        public ConversionController(
            IConversionService conversionService,
            CommonResponse commonResponse
            )
        {
            _commonResponse = commonResponse;
            _conversionService = conversionService;
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
    }
}
