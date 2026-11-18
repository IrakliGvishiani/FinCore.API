using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.ManagerDtos;
using FinCore.Application.Models.SuspiciousActivity;
using FinCore.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinCore.Controllers
{
    [Route("api/manager")]
    [ApiController]
    [Authorize(Roles = Roles.ManagerOrAdmin)]
    public class ManagerController : Controller
    {
        private readonly IManagerService _managerService;
        private readonly CommonResponse _commonResponse;

        public ManagerController(
            IManagerService managerService,
            CommonResponse commonResponse
            )
        {
            _managerService = managerService;
            _commonResponse = commonResponse;

        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(
            CancellationToken ct
            )
        {
            var result = await _managerService.GetDashboardAsync(ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("transfers")]

        public async Task<IActionResult> GetTransfers(
            [FromQuery] TransferFilterDto filter,
            CancellationToken ct
            )
        {
            var result = await _managerService.GetTransfersAsync(filter,ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("transfers/{id:int}")]

        public async Task<IActionResult> GetTransfersById(int id,CancellationToken ct)
        {
            var result = await _managerService.GetTransfersByIdAsync(id,ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts(
            [FromQuery] AccountFilterDto filter,
            CancellationToken ct
            )
        {
            var result = await _managerService.GetAccountsAsync(filter, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("accounts/{id:int}")]
        public async Task<IActionResult> GetAccountsById(int id, CancellationToken ct)
        {
            var result = await _managerService.GetAccountByIdAsync(id, ct);
            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("accounts/{id:int}/transactions")]
        public async Task<IActionResult> GetAccountTransactions(int id,
            [FromQuery] AccountTransactionFilterDto filter,
            CancellationToken ct)
        {
            var result = await _managerService.GetAccountTransactionsAsync(id, filter, ct);
            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("currency-conversions")]
        public async Task<IActionResult> GetCurrencyConversions(
            [FromQuery] CurrencyConversionFilterDto filter,
            CancellationToken ct)
        {
            var result = await _managerService.GetCurrencyConversionsAsync(filter, ct);
            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("currency-conversions/{id:int}")]
        public async Task<IActionResult> GetCurrencyConversionById(int id, CancellationToken ct)
        {
            var result = await _managerService.GetCurrencyConversionByIdAsync(id, ct);
            return this.ToActionResult(_commonResponse.Success(result));
        }


        [HttpGet("suspicious-activities")]
        public async Task<IActionResult> GetSuspiciousActivities(
            [FromQuery] SuspiciousActivityFilterDto filter,
            CancellationToken ct)
        {
            var result = await _managerService.GetSuspiciousActivitiesAsync(filter, ct);
            return this.ToActionResult(_commonResponse.Success(result));
        }

        [HttpGet("suspicious-activities/{id:int}")]
        public async Task<IActionResult> GetSuspiciousActivityById(int id, CancellationToken ct)
        {
            var result = await _managerService.GetSuspiciousActivityByIdAsync(id, ct);
            return this.ToActionResult(_commonResponse.Success(result));
        }


        [HttpPatch("{id:int}/review")]

        public async Task<IActionResult> Review(int id,CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _managerService.ReviewSuspiciousActivityAsync(id, userId, ct);
            return this.ToActionResult(_commonResponse.NoContent());
        }

        [HttpPatch("{id:int}/resolve")]

        public async Task<IActionResult> Resolve(int id, CancellationToken ct)
        {
            await _managerService.ResolveSuspiciousActivityAsync(id, ct);
            return this.ToActionResult(_commonResponse.NoContent());
        }
    }
}