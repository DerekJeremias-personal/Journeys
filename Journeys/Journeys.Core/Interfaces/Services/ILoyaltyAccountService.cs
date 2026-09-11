using Journeys.Core.Models;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface ILoyaltyAccountService
    {
        Task<LoyaltyAccountDto> GetLoyaltyAccountAsync(string tenantId, string id, bool resettleIfNeeded = false);
        Task<LoyaltyAccountDto> GetLoyaltyAccountByExtIdAsync(string tenantId, string extId, bool throwExceptionOnDeleted = false, bool resettleIfNeeded = false);
        Task<List<LoyaltyAccountDto>> GetLoyaltyAccountsAsync(string tenantId, List<string> filters);

        Task<LoyaltyAccountDto> UpsertLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account);

        Task<ExternalReferenceDto> AliasLoyaltyAccountAsync(string tenantId, AliasAccountRequest request);
        Task<ExternalReferenceDto> SetExternalReferenceAsync(string tenantId, ExternalReferenceDto extRef);

        Task<LoyaltyAccount> LoadLoyaltyAccountDetailsAsync(string tenantId, LoyaltyAccount acct);
        Task<bool> EnsureAccountInValidStateAsync(string tenantId, LoyaltyAccount acct, CancellationToken token = default(CancellationToken));

        Task<LoyaltyAccount> TryLockAccount(string tenantId, LoyaltyAccount account, string lockKey, int lockLeaseExpirationMS);
        Task<List<PointLedgerDto>> BringLoyaltyAccountPointsCurrentAsync(string tenantId, LoyaltyAccount loyaltyAccount, bool capilatizecapitalizeType = false);
        Task<List<PointLedger>> BringLoyaltyAccountPointsCurrentInternalAsync(string tenantId, LoyaltyAccount loyaltyAccount);

        Task<List<PointLedgerDto>> GetLoyaltyAccountPointsAsync(string tenantId, string accountId, bool resettleIfNeeded = false, bool capilatizecapitalizeType = false);

        Task<PointLedgerDto> BulkDepositPointsAsync(string tenantId, BulkPointDepositRequest request);
        Task<PointLedgerDto> DepositPointsAsync(string tenantId, PointDespositRequest request);
        Task<PointLedgerDto> WithdrawPointsAsync(string tenantId, PointWithdrawlRequest request);
        Task<PointLedgerDto> BulkWithdrawPointsAsync(string tenantId, BulkPointWithdrawalRequest request);
        Task<PointLedgerDto> UpsertLoyaltyAccountPointsAsync(string tenantId, PointLedgerDto ledger, LoyaltyAccount account = null);
        Task<List<PointLedgerDto>> SetDefaultPointLedgersAsync(string tenantId, LoyaltyAccount account, List<PointAccountType> types);

        Task<PointLedgerDto> AdminUpsertLoyaltyAccountPointsAsync(string tenantId, PointLedgerDto ledger);

        Task<PagedResultSet<TagDto>> GetLoyaltyAccountTagsAsync(string tenantId, string accountId, string type, int pageSize, string? continuationToken = null);
        Task<TagDto> TagLoyaltyAccountAsync(string tenantId, TagDto tag);

        Task<PointsDetailsResponse> UpsertPointsDetails(string tenantId, UpsertPointsDetailsRequest req);
        Task<List<PointsDetailsResponse>> BulkUpsertPointsDetails(string tenantId, BulkUpsertPointsDetailsRequest request);
        Task<PointsDetailsResponse> GetPointsDetails(string tenantId, string loyaltyAccountId, string pointEventId, string pointEventType);
        Task<List<PointsDetailsResponse>> GetManyPointsDetails(string tenantId, GetManyPointsDetailsRequest request, bool capitalizeType = false);
        Task<PagedResultSetResponse<PointsDetailsDto>> GetAllPointsDetails(string tenantId, string loyaltyAccountId, int pageSize, string continuationToken = null);

        Task<List<PointLedger>> ExpireLoyaltyAccountPointsByEarnDate(string tenantId, string loyaltyAccountId, List<string> ledgerIds, DateTimeOffset date, decimal amountPercent);
        Task<List<PointLedger>> ExpireLoyaltyAccountPointsByExpirationDate(string tenantId, string loayaltyAccountId, List<string> ledgerIds, DateTimeOffset expDate, decimal amountPercent);
        Task<List<PointLedger>> ExpireLoyaltyAccountPointsByEventId(string tenantId, string loyaltyAccountId, List<string> ledgerIds, string eventId, decimal amount, bool isPercent);
        Task<List<PointLedger>> ExpireLoyaltyAccountPointsByAmount(string tenantId, string loyaltyAccountId, List<string> ledgerIds, decimal amount, bool isPercent);

        Task<string?> GetFullAccountReportAsync(string tenantId, string accountId);
        Task<string?> GetFullAccountReportAsync(string tenantId, LoyaltyAccountDto account);

        Task DeleteLoyaltyAccountAsync(string tenantId, string accountId);
        Task DeleteLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account);

        Task SoftDeleteLoyaltyAccountAsync(string tenantId, string loyaltyAccountId);
        Task SoftDeleteLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account);

        //Task<UserStatsDto> GetUserStatsAsync(string tenantId, string accountId);


    }
}
