using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCore.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly CommonResponse _commonResponse;
        private readonly IAccountService _accountService;
        private readonly IAuditService _audit;
        private readonly IUserManagementService _userManagementService;

        public AdminController(
            CommonResponse commonResponse,
            IAccountService accountService,
            IAuditService auditService,
            IUserManagementService userManagementService
            )
        {
            _commonResponse = commonResponse;
            _accountService = accountService;
            _audit = auditService;
            _userManagementService = userManagementService;
        }

        [HttpGet("actions")]
        public IActionResult GetActions()
        => this.ToActionResult(_commonResponse.Success(AuditActions.All));

        [HttpGet("audit-logs")]
        public async Task<IActionResult> Get(
            [FromQuery] AuditFilter filter,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default
            )
        {
            var (items, totalCount) = await _audit.GetLogsAsync(filter,pageNumber,pageSize,ct);

            return this.ToActionResult(_commonResponse.Success(new { items, totalCount }));
        }
        
        [HttpPatch("{accountNumber}/freeze")]

        public async Task<IActionResult> FreezeAccount(string accountNumber, CancellationToken ct)
        {

            await _accountService.FreezeAccountAsync(accountNumber, ct);

            return this.ToActionResult(_commonResponse.NoContent());
        }

        
        [HttpPatch("{accountNumber}/unfreeze")]

        public async Task<IActionResult> UnfreezeAccount(string accountNumber, CancellationToken ct)
        {

            await _accountService.UnfreezeAccountAsync(accountNumber, ct);

            return this.ToActionResult(_commonResponse.NoContent());
        }

        [HttpGet("users")]

        public async Task<IActionResult> GetUsers(
            [FromQuery] string? search,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default
            )
        {
            var (items, totalCount) = await _userManagementService.GetUsersAsync(
                search,pageNumber,pageSize,ct
                );

            return this.ToActionResult(_commonResponse.Success(
                new
                {
                    items,
                    totalCount,
                    pageNumber,
                    pageSize
                }
                ));
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserById(
            string userId,
            CancellationToken ct = default
            )
        {
            var result = await _userManagementService.GetUserByIdAsync(userId,ct);

            return this.ToActionResult(_commonResponse.Success(result));
        }
    }
}
