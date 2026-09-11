using Journeys.API.Models;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly ILoyaltyAccountService _accountService;
        private readonly IEventService _eventService;
        private readonly ILogger<AccountController> _logger;
        private readonly IAdminAuditService _adminAuditService;

        public AccountController(ILoyaltyAccountService accountService, IEventService eventService, IAdminAuditService auditService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _eventService = eventService;
            _logger = logger;
            _adminAuditService = auditService ?? throw new ArgumentNullException(nameof(auditService), "AdminAuditService cannot be null.");
        }

        [HttpGet("{tenantId}/{id}")]
        public async Task<ActionResult<LoyaltyAccountDto>> GetLoyaltyAccount(string tenantId, string id)
        {
            var account = await _accountService.GetLoyaltyAccountAsync(tenantId, id, true);
            if (account == null)
            {
                return NotFound();
            }
            if (account.ResettleASAP)
            {
                var res = await _eventService.ResettleAccountAsync(tenantId, account);
                return Ok(res);
            }
            return Ok(account);
        }

        [HttpGet("{tenantId}/ext/{id}")]
        public async Task<ActionResult<LoyaltyAccountDto>> GetLoyaltyAccountByExternalId(string tenantId, string id)
        {
            try
            {
                var account = await _accountService.GetLoyaltyAccountByExtIdAsync(tenantId, id, false, true);
                if (account == null)
                {
                    return NotFound();
                }
                if (true) //account.ResettleASAP)
                {
                    var res = await _eventService.ResettleAccountAsync(tenantId, account);
                    return Ok(res);
                }

                return Ok(account);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{tenantId}/points/balances/{id}")]
        public async Task<ActionResult<List<AccountPointBalance>>> GetLoyaltyAccountPointBalances(string tenantId, string id)
        {
            var ledgers = await _accountService.GetLoyaltyAccountPointsAsync(tenantId, id, true);
            if (ledgers == null)
            {
                return NotFound();
            }
            List<AccountPointBalance> balances = new List<AccountPointBalance>();
            ledgers.ForEach(l => balances.Add(new AccountPointBalance
            {
                AccountId = l.AccountId,
                PointAccountTypeId = l.PointAccountTypeId,
                CurrentBalance = l.CurrentBalance,
                LifetimeTotal = l.LifetimeTotal
            }));
            return Ok(balances);
        }

        [HttpGet("{tenantId}/points/{id}")]
        public async Task<ActionResult<List<PointLedgerDto>>> GetLoyaltyAccountPoints(string tenantId, string id)
        {
            //HACK: because dealer portal blows up if the redemption event type is not "Redemption"
            //Capitalize the first letter of the event type for each entry
            bool capilatizecapitalizeType = true;

            var ledgers = await _accountService.GetLoyaltyAccountPointsAsync(tenantId, id, true, capilatizecapitalizeType);
            if (ledgers == null)
            {
                return NotFound();
            }
            if (ledgers.Any(x => x.ResettleASAP))
            {
                var res = await _eventService.ResettleAccountAsync(tenantId, id);
                return Ok(res.PointLedgers ?? ledgers);
            }
            return Ok(ledgers);
        }

        [HttpGet("{tenantId}/tags/{id}")]
        public async Task<ActionResult<PointLedgerDto>> GetLoyaltyAccountTags(string tenantId, string id, string type)
        {
            var tags = await _accountService.GetLoyaltyAccountTagsAsync(tenantId, id, type, 100); //TODO: add this as a parameter? a exref should not have more than 100 tags..?
            if (tags == null)
            {
                return NotFound();
            }
            return Ok(tags);
        }

        [HttpPost("{tenantId}/admin/save")]
        public async Task<ActionResult<LoyaltyAccountDto>> SaveLoyaltyAccount(string tenantId, [FromBody] LoyaltyAccountDto account)
        {
            if (account == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID.");
            }
            if (string.IsNullOrEmpty(account.TenantId))
            {
                account.TenantId = tenantId;
            }

            try
            {
                var savedAccount = await _accountService.UpsertLoyaltyAccountAsync(tenantId, account);
                return Ok(savedAccount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{tenantId}/alias")]
        public async Task<ActionResult<ExternalReference>> AliasLoyaltyAccount(string tenantId, [FromBody] AliasAccountRequest request)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID.");
            }
            if (string.IsNullOrEmpty(request.AliasId))
            {
                return BadRequest("Invalid alias ID.");
            }
            try
            {
                var exref = await _accountService.AliasLoyaltyAccountAsync(tenantId, request);
                if (exref == null)
                {
                    return NotFound();
                }
                return Ok(exref);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return BadRequest(ex.Message);
            }
        }


        [HttpPost("{tenantId}/points/deposit")]
        public async Task<ActionResult<PointLedgerDto>> DepositPoints(string tenantId, [FromBody] PointDespositRequest request)
        {
            AdminAuditDto? auditEntry = null;
            try
            {
                if (request == null || string.IsNullOrEmpty(tenantId))
                {
                    return BadRequest("Invalid tenant ID or ledgers point ledgers data.");
                }

                var pointAccountType = await PointAccountTypeCache.Instance.GetPointAccountTypeAsync(tenantId, request.PointAccountTypeId);
                if(pointAccountType == null)
                {
                    return BadRequest($"Point Account Type with ID {request.PointAccountTypeId} not found.");
                }
                var actionPrettyPrint = $"Deposit {request.Amount} to {pointAccountType.Name} for {request.LoyaltyAccountId}";

                var savedLedgerTask = _accountService.DepositPointsAsync(tenantId, request);
                var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, request, actionPrettyPrint, true);
                await Task.WhenAll(savedLedgerTask, auditTask.ContinueWith(async (dto) => auditEntry = await dto));
                var savedLedger = savedLedgerTask.Result;
                return Ok(savedLedger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                if (auditEntry != null)
                {
                    await _adminAuditService.RollbackAudit(auditEntry);
                }
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{tenantId}/points/withdrawal")]
        public async Task<ActionResult<PointLedgerDto>> WithdrawPoints(string tenantId, [FromBody] PointWithdrawlRequest request)
        {
            AdminAuditDto? auditEntry = null;
            try
            {
                if (request == null || string.IsNullOrEmpty(tenantId))
                {
                    return BadRequest("Invalid tenant ID or ledgers data.");
                }

                var pointAccountType = await PointAccountTypeCache.Instance.GetPointAccountTypeAsync(tenantId, request.PointAccountTypeId);
                if (pointAccountType == null)
                {
                    return BadRequest($"Point Account Type with ID {request.PointAccountTypeId} not found.");
                }
                var actionPrettyPrint = $"Withdraw {request.Amount} from {pointAccountType.Name} for {request.LoyaltyAccountId}";

                var savedLedgerTask = _accountService.WithdrawPointsAsync(tenantId, request);
                var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, request, actionPrettyPrint, true);
                await Task.WhenAll(savedLedgerTask, auditTask.ContinueWith(async (dto) => auditEntry = await dto));
                var savedLedger = savedLedgerTask.Result;
                return Ok(savedLedger);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex);
                if (auditEntry != null)
                {
                    await _adminAuditService.RollbackAudit(auditEntry);
                }
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{tenantId}/points/bulk/withdrawal")]
        public async Task<ActionResult<PointLedgerDto>> BulkWithdrawPoints(string tenantId, [FromBody] BulkPointWithdrawalRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }

            var actionPrettyPrint = $"Bulk Withdraw for {request.LoyaltyAccountId}";

            var savedLedgerTask = _accountService.BulkWithdrawPointsAsync(tenantId, request);
            var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, request, actionPrettyPrint, false);
            await Task.WhenAll(savedLedgerTask, auditTask);
            var savedLedger = savedLedgerTask.Result;

            return Ok(savedLedger);
        }


        [HttpPost("{tenantId}/points/admin")]
        public async Task<ActionResult<PointLedgerDto>> UpsertLedger(string tenantId, [FromBody] PointLedgerDto request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }

            var pointAccountType = await PointAccountTypeCache.Instance.GetPointAccountTypeAsync(tenantId, request.PointAccountTypeId);
            if (pointAccountType == null)
            {
                return BadRequest($"Point Account Type with ID {request.PointAccountTypeId} not found.");
            }
            var actionPrettyPrint = $"Upsert Ledger for {pointAccountType.Name} for {request.AccountId}";

            var savedLedgerTask = _accountService.AdminUpsertLoyaltyAccountPointsAsync(tenantId, request);
            var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, request, actionPrettyPrint, false);
            await Task.WhenAll(savedLedgerTask, auditTask);
            var savedLedger = savedLedgerTask.Result;

            return Ok(savedLedger);
        }



        [HttpPost("{tenantId}/points/details")]
        public async Task<ActionResult<PointLedgerDto>> UpsertPointsDetails(string tenantId, [FromBody] UpsertPointsDetailsRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }

            var savedDetails = await _accountService.UpsertPointsDetails(tenantId, request);
            return Ok(savedDetails);
        }

        [HttpPost("{tenantId}/points/bulk/details")]
        public async Task<ActionResult<PointLedgerDto>> BulkUpsertPointsDetails(string tenantId, [FromBody] BulkUpsertPointsDetailsRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }

            var savedDetails = await _accountService.BulkUpsertPointsDetails(tenantId, request);
            return Ok(savedDetails);
        }

        [HttpGet("{tenantId}/points/details/{accountId}")]
        public async Task<ActionResult<PointLedgerDto>> GetLoyaltyAccountPointsDetails(string tenantId, string accountId, string eventType, string eventId)
        {
            var details = await _accountService.GetPointsDetails(tenantId, accountId, eventType, eventId);
            if (details == null)
            {
                return NotFound();
            }
            return Ok(details);
        }

        [HttpPost("{tenantId}/points/details/many")]
        public async Task<ActionResult<PointLedgerDto>> GetManyLoyaltyAccountPointsDetails(string tenantId, [FromBody] GetManyPointsDetailsRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }

            //HACK: because dealer portal blows up if the redemption event type is not "Redemption"
            //Capitalize the first letter of the event type for each entry
            bool capilatizecapitalizeType = true;

            var details = await _accountService.GetManyPointsDetails(tenantId, request, capilatizecapitalizeType);
            if (details == null)
            {
                return NotFound();
            }
            return Ok(details);
        }

        [HttpPost("{tenantId}/points/details/all")]
        public async Task<ActionResult<PagedResultSetResponse<PointsDetails>>> GetAllLoyaltyAccountPointsDetails(string tenantId, [FromBody] GetAllByAccountRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }
            var details = await _accountService.GetAllPointsDetails(tenantId, request.LoyaltyAccountId, request.PageSize, request.ContinuationToken);
            if (details == null)
            {
                return NotFound();
            }
            return Ok(details);
        }


        [HttpGet("{tenantId}/tag/{accountId}")]
        public async Task<ActionResult<List<TagDto>>> GetAccountTags(string tenantId, string accountId, string type, int pageSize, string? continuationToken = null)
        {
            var tags = await _accountService.GetLoyaltyAccountTagsAsync(tenantId, accountId, type, pageSize, continuationToken);
            if (tags == null)
            {
                return NotFound();
            }
            return Ok(tags);
        }

        [HttpPost("{tenantId}/tag")]
        public async Task<ActionResult<TagDto>> TagLoyaltyAccount(string tenantId, [FromBody] TagDto tag)
        {
            if (tag == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }

            var savedTag = await _accountService.TagLoyaltyAccountAsync(tenantId, tag);
            return Ok(savedTag);
        }

        [HttpDelete("{tenantId}/remove")]
        public async Task<IActionResult> RemoveLoyaltyAccount(string tenantId, [FromBody] LoyaltyAccountDto account)
        {
            if (account == null || string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID or ledgers data.");
            }

            await _accountService.DeleteLoyaltyAccountAsync(tenantId, account);
            return NoContent();
        }

        [HttpGet("{tenantId}/report/{id}")]
        public async Task<IActionResult> GetFullAccountReport(string tenantId, string id)
        {
            try
            {
                var uri = await _accountService.GetFullAccountReportAsync(tenantId, id);
                return StatusCode(200, uri);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (APIErrorsException ex)
            {
                return BadRequest(new { ex.Errors });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An unexpected error occurred.");
            }
        }

        [HttpDelete("{tenantId}/remove/{id}")]
        public async Task<IActionResult> RemoveLoyaltyAccount(string tenantId, string id)
        {
            try
            {
                await _accountService.DeleteLoyaltyAccountAsync(tenantId, id);
                return NoContent();
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (APIErrorsException ex)
            {
                return BadRequest(new { ex.Errors });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An unexpected error occurred.");
            }
        }

        //[HttpGet("{tenantId}/{id}/stats")]
        //public async Task<ActionResult<UserStats>> GetUserStats(string tenantId, string id)
        //{
        //    var stats = await _accountService.GetUserStatsAsync(tenantId, id);
        //    if (stats == null)
        //    {
        //        return NotFound();
        //    }
        //    return Ok(stats);
        //}
    }
}
