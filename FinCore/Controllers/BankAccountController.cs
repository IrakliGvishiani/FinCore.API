using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FinCore.Controllers
{
    public record DepositRequest([Range(0.01,10_100)] decimal Amount);


    [Route("api/account")]
    [ApiController]
    [Authorize]
    public class BankAccountController : Controller
    {
        private readonly CommonResponse _commonResponse;
        private readonly IAccountService _accountService;


        public BankAccountController(
            CommonResponse commonResponse,
            IAccountService accountService
            )
        {
            _commonResponse = commonResponse;
            _accountService = accountService;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User ID not found.");
        }

        //GET ACCOUNTS
        [HttpGet]
        [Authorize(Roles = "User")]

        public async Task<IActionResult> GetMyAccounts(CancellationToken ct)
        {
            var userId = GetUserId();

            var accounts = await _accountService.GetMyAccountsAsync(userId,ct);

            return this.ToActionResult(_commonResponse.Success(accounts));
        }

        //GET ACCOUNT BY ACCNUMBER
        [HttpGet("{accountNumber}")]
        [Authorize(Roles = "User")]

        public async Task<IActionResult> GetAccount(string accountNumber,CancellationToken ct)
        {
            var userId = GetUserId();

            var account = await _accountService.GetAccountAsync(userId,accountNumber,ct);

            return this.ToActionResult(_commonResponse.Success(account));
        }


        //CREATE ACCOUNT
        [HttpPost]
        [Authorize(Roles = "User")]

        public async Task<IActionResult> OpenAccount([FromQuery] CurrencyCode currency,CancellationToken ct)
        {
            var userId = GetUserId();

            var acc = await _accountService.OpenAccountAsync(userId,currency,ct);

            return this.ToActionResult(_commonResponse.Success(acc));
        }

        //CLOSE ACCOUNT
        [HttpPost("{accountNumber}/close")]
        [Authorize(Roles = "User")]

        public async Task<IActionResult> CloseAccount(string accountNumber,CancellationToken ct)
        {
            var userId = GetUserId();

             await _accountService.CloseAccountAsync(userId,accountNumber,ct);

            return this.ToActionResult(_commonResponse.NoContent());
        }

        // FREEZE ACCOUNT
        [HttpPatch("{accountNumber}/freeze")]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> FreezeAccount(string accountNumber,CancellationToken ct)
        {

            await _accountService.FreezeAccountAsync(accountNumber,ct);

            return this.ToActionResult(_commonResponse.NoContent());
        }

        // UNFREEZE ACCOUNT
        [HttpPatch("{accountNumber}/unfreeze")]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> UnfreezeAccount(string accountNumber, CancellationToken ct)
        {

            await _accountService.UnfreezeAccountAsync(accountNumber, ct);

            return this.ToActionResult(_commonResponse.NoContent());
        }

        // DEPOSIT 
        [HttpPost("{accountNumber}/deposit")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Deposit(
    string accountNumber,
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
    [FromBody] DepositRequest request,
    CancellationToken ct)
        {
            var result = await _accountService.DepositAsync(
                GetUserId(), accountNumber, request.Amount, idempotencyKey, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

    }
}
