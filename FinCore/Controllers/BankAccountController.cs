using CloudinaryDotNet.Actions;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
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
        private readonly IAccountStatementService _accountStatementService;


        public BankAccountController(
            CommonResponse commonResponse,
            IAccountService accountService,
            IAccountStatementService accountStatementService
            )
        {
            _commonResponse = commonResponse;
            _accountService = accountService;
            _accountStatementService = accountStatementService;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User ID not found.");
        }

        //GET ACCOUNTS
        [HttpGet]
        [Authorize(Roles = "Customer")]

        public async Task<IActionResult> GetMyAccounts(CancellationToken ct)
        {
            var userId = GetUserId();

            var accounts = await _accountService.GetMyAccountsAsync(userId,ct);

            return this.ToActionResult(_commonResponse.Success(accounts));
        }

        [HttpGet("{accountNumber}/statement")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetAccountStatement(
            string accountNumber,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 15,
            CancellationToken ct = default
            )
        {
            var statement = await _accountStatementService.GetStatementAsync(
                GetUserId(),
                accountNumber,
                from,
                to,
                pageNumber,
                pageSize,
                ct
                );

            return this.ToActionResult(_commonResponse.Success(statement));
        }


        //GET ACCOUNT BY ACCNUMBER
        [HttpGet("{accountNumber}")]
        [Authorize(Roles = "Customer")]

        public async Task<IActionResult> GetAccount(string accountNumber,CancellationToken ct)
        {
            var userId = GetUserId();

            var account = await _accountService.GetAccountAsync(userId,accountNumber,ct);

            return this.ToActionResult(_commonResponse.Success(account));
        }


        //CREATE ACCOUNT
        [HttpPost]
        [Authorize(Roles = "Customer")]

        public async Task<IActionResult> OpenAccount([FromQuery] OpenAccountRequest model,CancellationToken ct)
        {
            var userId = GetUserId();

            var acc = await _accountService.OpenAccountAsync(userId,model.Currency,model.Name,ct);

            return this.ToActionResult(_commonResponse.Success(acc));
        }

        //CLOSE ACCOUNT
        [HttpPost("{accountNumber}/close")]
        [Authorize(Roles = "Customer")]

        public async Task<IActionResult> CloseAccount(string accountNumber,CancellationToken ct)
        {
            var userId = GetUserId();

             await _accountService.CloseAccountAsync(userId,accountNumber,ct);

            return this.ToActionResult(_commonResponse.NoContent());
        }



        // DEPOSIT 
        [HttpPost("{accountNumber}/deposit")]
        [Authorize(Roles = "Customer")]
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

        [HttpPatch("{accountNumber}/name")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Rename(
        string accountNumber, [FromBody] RenameAccountRequest request, CancellationToken ct)
        {
            var result = await _accountService.RenameAccountAsync(
                GetUserId(), accountNumber, request.Name, ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }

    }
}
