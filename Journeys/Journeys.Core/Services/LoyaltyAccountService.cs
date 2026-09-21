using Azure;
using Backend.Dto.Structures.Model;
using Journeys.Core.Caching;
using Journeys.Core.Exceptions;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.Utility;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using MassTransit;
using MassTransit.Contracts.JobService;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Serilog.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using ConcurrencyException = Journeys.Core.Exceptions.ConcurrencyException;

namespace Journeys.Core.Services
{
    public class LoyaltyAccountService : ILoyaltyAccountService
    {
        public static string EXT_ID_TYPE = "account";
        public static string EXT_ID_TYPE_ALIAS = "alias";

        private const string ACTIVE = "active";
        private const string DELETED = "deleted";

        private const string INIT = "init";

        // HACK: Pull from metadata
        private const string LOYALTY_DETAILS = "a79bcc89-07cd-4ae9-87e9-95c45aa48819";

        private readonly ILoyaltyAccountAdapter _loyaltyAccountAdapter;
        private readonly ITagAdapter _tagAdapter;
        private readonly ILoyaltyAccountPointLedgerAdapter _loyaltyAccountPointLedgerAdapter;
        //private readonly ILoyaltyAccountRuleStateAdapter _loyaltyAccountRuleStateAdapter;
        private readonly ILoyaltyAccountPointsDetailsAdapter _loyaltyAccountPointsDetailsAdapter;
        private readonly IPointAccountTypeCache _cache;
        private readonly ILogger<LoyaltyAccountService> _logger;
        private readonly IDynamicDataAdapter _dynamicDataAdapter;
        private readonly IDynamicExternalReferenceAdapter _dynamicExtRefAdapter;
        private readonly IDataLakeAdapter _dataLakeAdapter;


        private readonly SemaphoreSlim _lockObject = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _accountSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

        private static PathValueProvider DbStatusProvider = new PathValueProvider("db_status");

        public LoyaltyAccountService(ILoyaltyAccountAdapter loyaltyAccountAdapter, ILoyaltyAccountPointLedgerAdapter loyaltyAccountPointLedgerAdapter,
                            ITagAdapter tagAdapter, IPointAccountTypeCache cache, // ILoyaltyAccountRuleStateAdapter loyaltyAccountRuleStateAdapter,
                            ILoyaltyAccountPointsDetailsAdapter pointsDetailsAdapter,
                            ILogger<LoyaltyAccountService> logger, IDynamicDataAdapter dynamicDataAdapter,
                            IDynamicExternalReferenceAdapter dynamicAdapter, IDataLakeAdapter dataLakeAdapter)
        {
            _loyaltyAccountAdapter = loyaltyAccountAdapter;
            _loyaltyAccountPointLedgerAdapter = loyaltyAccountPointLedgerAdapter;
            _tagAdapter = tagAdapter;
            _cache = cache;
            //_loyaltyAccountRuleStateAdapter = loyaltyAccountRuleStateAdapter;
            _loyaltyAccountPointsDetailsAdapter = pointsDetailsAdapter;
            _logger = logger;
            _dynamicDataAdapter = dynamicDataAdapter;
            _dynamicExtRefAdapter = dynamicAdapter;
            _dataLakeAdapter = dataLakeAdapter;

        }

        #region Loyalty Account

        public async Task<LoyaltyAccountDto> GetLoyaltyAccountAsync(string tenantId, string accountId, bool resettleIfNeeded = false)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentNullException(nameof(accountId), "The AccountId must be provided.");

            LoyaltyAccountDto loyaltyAccount = null;
            try
            {
                var acct = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, accountId, ACTIVE);
                if (acct != null)
                {
                    if (resettleIfNeeded)
                    {
                        var res = await BringLoyaltyAccountPointsCurrentAsync(tenantId, acct);
                    }
                    loyaltyAccount = acct?.ToDto();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return loyaltyAccount;
        }

        public async Task<LoyaltyAccountDto> GetLoyaltyAccountByExtIdAsync(string tenantId, string extId, bool throwExceptionOnDeleted = false, bool resettleIfNeeded = false)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrWhiteSpace(extId))
                throw new ArgumentNullException(nameof(extId), "The External Account Id must be provided.");

            LoyaltyAccountDto loyaltyAccount = null;
            try
            {
                var extRef = await _loyaltyAccountAdapter.FetchAccountExternalReferenceAsync(tenantId, extId, EXT_ID_TYPE);
                if (extRef == null)
                {
                    //See if it's an alias
                    extRef = await _loyaltyAccountAdapter.FetchAccountExternalReferenceAsync(tenantId, extId, EXT_ID_TYPE_ALIAS);
                }
                if (extRef != null)
                {
                    var acct = await _loyaltyAccountAdapter.FetchLoyaltyAccountByExtIdAsync(tenantId, extRef, ACTIVE);
                    if (acct != null)
                    {
                        if (resettleIfNeeded)
                        {
                            var res = await BringLoyaltyAccountPointsCurrentAsync(tenantId, acct);
                        }
                        loyaltyAccount = acct?.ToDto();
                    }
                    else if (throwExceptionOnDeleted)
                    {
                        var deletedRecord = await _loyaltyAccountAdapter.FetchLoyaltyAccountByExtIdAsync(tenantId, extRef, DELETED);
                        if (deletedRecord != null) throw new Exception("The provided external id references a Deleted account.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return loyaltyAccount;
        }

        public async Task<List<LoyaltyAccountDto>> GetLoyaltyAccountsAsync(string tenantId, List<string> filters)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            List<LoyaltyAccountDto> loyaltyAccounts = default;
            try
            {
                var fils = filters ?? new List<string>();
                var res = await _loyaltyAccountAdapter.GetLoyaltyAccountsAsync(tenantId, fils);

                loyaltyAccounts = res.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return loyaltyAccounts;
        }

        public async Task<LoyaltyAccountDto> UpsertLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (account == null)
                throw new ArgumentNullException(nameof(account), "The Account must be provided.");

            try
            {
                //Need to preserve any existing journey state
                var inboundLoyaltyAccount = account.FromDto();
                inboundLoyaltyAccount.Status = ACTIVE;

                LoyaltyAccount storedLoyaltyAccount = null;

                if (!string.IsNullOrEmpty(account.Id))
                    storedLoyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, account.Id, ACTIVE);
                else if (!string.IsNullOrEmpty(account.ExtAccountId))
                    storedLoyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountByExtIdAsync(tenantId, account.ExtAccountId, EXT_ID_TYPE, ACTIVE);

                if (storedLoyaltyAccount != null && (storedLoyaltyAccount.Journeys?.Any() ?? false) &&
                    (inboundLoyaltyAccount.Journeys == null))
                {
                    inboundLoyaltyAccount.Journeys = storedLoyaltyAccount.Journeys;
                }

                var res = await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, inboundLoyaltyAccount);
                account = res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return account;
        }

        public async Task<ExternalReferenceDto> AliasLoyaltyAccountAsync(string tenantId, AliasAccountRequest request)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrEmpty(request?.LoyaltyAccountId) && string.IsNullOrEmpty(request.XId))
                throw new ArgumentNullException("The Account Id or External Id must be provided.");

            try
            {
                LoyaltyAccount storedLoyaltyAccount = null;

                //Find loyalty account to alias...either by account id or xid
                if (!string.IsNullOrEmpty(request.LoyaltyAccountId))
                    storedLoyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId, ACTIVE);
                else if (!string.IsNullOrEmpty(request.XId))
                    storedLoyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountByExtIdAsync(tenantId, request.XId.Trim().ToLowerInvariant(), EXT_ID_TYPE, ACTIVE);

                if (storedLoyaltyAccount == null)
                    return null;

                //We have an account, so alias it
                var res = await _loyaltyAccountAdapter.UpsertExternalReferenceAsync(tenantId,
                                    new ExternalReference("alias", "active", request.AliasId, storedLoyaltyAccount.Id, tenantId, null, null));

                return res.ToDto();          
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return null;
        }

        public async Task<ExternalReferenceDto> SetExternalReferenceAsync(string tenantId, ExternalReferenceDto extRef)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (string.IsNullOrWhiteSpace(extRef.Type))
                throw new ArgumentNullException(nameof(extRef.Type), "The reference Type must be provided.");

            if (string.IsNullOrEmpty(extRef?.MapFromId) && string.IsNullOrEmpty(extRef.MapToId))
                throw new ArgumentNullException("The MapFrom Id and MapTo Id must be provided.");

            try
            {
                extRef.Status ??= "active";
                var res = await _loyaltyAccountAdapter.UpsertExternalReferenceAsync(tenantId, extRef.FromDto());

                return res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return null;
        }

        public async Task<LoyaltyAccount> LoadLoyaltyAccountDetailsAsync(string tenantId, LoyaltyAccount acct)
        {
            var loyaltyDetailsRecord = await _dynamicDataAdapter.GetEntitiesByPKAsync<WrappedEventPayload>(tenantId, acct.Id, LOYALTY_DETAILS, 100);
            if (!string.IsNullOrEmpty(loyaltyDetailsRecord?.ContinuationToken)) throw new Exception($"LoyaltyAccountService.EnsureAccountInValidState: account - {acct.ExtAccountId} exceded max number of detail records.");

            var ents = loyaltyDetailsRecord?.Entities?.FirstOrDefault();
            acct.AccountDetails = ents?.Event;
            return acct;
        }

        public async Task<bool> EnsureAccountInValidStateAsync(string tenantId, LoyaltyAccount acct, CancellationToken token = default(CancellationToken))
        {
            try
            {
                //load current account details page
                var loyaltyDetails = acct.AccountDetails;
                if (loyaltyDetails == null)
                {
                    acct = await LoadLoyaltyAccountDetailsAsync(tenantId, acct);
                    loyaltyDetails = acct.AccountDetails;
                }

                if (loyaltyDetails == null) return false;

                //if (loyaltyDetails.Status?.Equals(DELETED) ?? false) return false;

                //Load Details (event payload) IsValid Rule(s)
                var rules = await GetAccountValidationRules();

                //var state = new RulesEngineState(_loyaltyAccountRuleStateAdapter, new List<Campaign>(), false, acct);
                var state = new RulesEngineState(new List<Campaign>(), false, acct);
                state.LoyaltyAccountId = acct.Id;
                state.TenantId = tenantId;
                state.ImportDynamicEvent(acct.AccountDetails);

                return await rules.Evaluate(state, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
            return false;
        }

        public async Task<LoyaltyAccount> TryLockAccount(string tenantId, LoyaltyAccount account, string lockKey, int lockLeaseExpirationMS)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (account == null)
                throw new ArgumentNullException(nameof(account), "The Account must be provided.");

            if (string.IsNullOrWhiteSpace(lockKey))
                throw new ArgumentNullException(nameof(lockKey), "The Lock Key must be provided.");
            if (lockLeaseExpirationMS == 0)
                throw new ArgumentNullException(nameof(lockLeaseExpirationMS), "The lockLeaseExpirationMS must be provided.");

            var numretries = 3;
            while (numretries > 0)
            {
                try
                {
                    await _lockObject.WaitAsync(); // Acquire the lock asynchronously
                    try
                    {
                        //Attempt to retrieve the loyalty account, confirm it is not already locked (and lock lease has not expired)
                        // and lock it
                        var storedAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, account.Id, ACTIVE);
                        if (storedAccount == null)
                            throw new Exception($"No loyaltyaccount found for the provided accountid: {account.Id}");

                        if ((!storedAccount.LockLeaseKey?.Equals(lockKey, StringComparison.InvariantCultureIgnoreCase) ?? false) &&
                            (storedAccount.LockLeaseExpiration ?? DateTimeOffset.MinValue) > DateTimeOffset.UtcNow)
                            throw new LockAcquisitionException("Concurrency check failure", account.Id, account.LockLeaseKey, storedAccount.LockLeaseKey);

                        storedAccount.LockLeaseKey = lockKey;
                        storedAccount.LockLeaseExpiration = DateTimeOffset.UtcNow.AddMilliseconds(lockLeaseExpirationMS);
                        storedAccount.Status = ACTIVE;
                        var returnAccount = await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, storedAccount);
                        returnAccount.AccountDetails = account.AccountDetails;
                        returnAccount.Journeys = account.Journeys;
                        returnAccount.Campaigns = account.Campaigns;
                        return returnAccount;
                    }
                    finally
                    {
                        _lockObject.Release(); // Always release the lock
                    }
                }
                catch (LockAcquisitionException laex)
                {
                    //TODO: Log this

                    await Task.Delay(100);
                    numretries--;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                    throw;
                }
            }
            return null;
        }

        /// <summary>
        /// Full report of LoyaltyAccount and all associated data
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="accountId"></param>
        /// <returns>Storage container SAS expiring uri string containing the report</returns>
        /// <exception cref="ArgumentNullException"></exception>

        public async Task<string?> GetFullAccountReportAsync(string tenantId, string accountId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentNullException(nameof(accountId), "The AccountId must be provided.");

            var acct = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, accountId, ACTIVE);
            if (acct == null) throw new ArgumentNullException(nameof(acct));
            return await GetFullAccountReportInternalAsync(tenantId, acct, true);
        }

        /// <summary>
        /// Full report of LoyaltyAccount and all associated data
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="account"></param>
        /// <returns>Storage container SAS expiring uri string containing the report</returns>
        /// <exception cref="ArgumentNullException"></exception>

        public async Task<string?> GetFullAccountReportAsync(string tenantId, LoyaltyAccountDto account)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (account == null)
                throw new ArgumentNullException(nameof(account), "The Account must be provided.");

            return await GetFullAccountReportInternalAsync(tenantId, account.FromDto(), true);
        }

        internal async Task<string?> GetFullAccountReportInternalAsync(string tenantId, LoyaltyAccount account, bool fetch)
        {
            var baseDir = $"{tenantId}/AccountData";
            var fileName = $"{account.Id}.json";
            var fileSystem = "reports";

            Dictionary<string, object> data = [];

            //if there is a prior report then delete it first
            await DeleteFullAccountReportAsync(tenantId, account);
            await _dataLakeAdapter.CreateDirectoryAsync(baseDir, fileSystem);
            var stream = await _dataLakeAdapter.GetFileWriteStreamAsync(baseDir, fileName);
            using var streamWriter = new StreamWriter(stream: stream, encoding: Encoding.UTF8);
            using var jsonWriter = new JsonTextWriter(streamWriter)
            {
                Formatting = Formatting.Indented
            };
            var serializer = new Newtonsoft.Json.JsonSerializer();

            try
            {
                jsonWriter.WriteStartObject();

                //Fetch all tags
                var bOk = await DeleteLoyaltyAccountTagsInternalAsync(tenantId, account, fetch);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account tags could not be deleted" } });
                jsonWriter.WritePropertyName("Tags");
                serializer.Serialize(jsonWriter, bOk.Data);

                //Fetch all related events
                bOk = await DeleteLoyaltyAccountEventsInternalAsync(tenantId, account, fetch);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account events could not be deleted" } });
                jsonWriter.WritePropertyName("Events");
                serializer.Serialize(jsonWriter, bOk.Data);

                //Fetch points details
                bOk = await DeleteLoyaltyAccountPointsDetailsInternalAsync(tenantId, account, fetch);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account points details could not be deleted" } });
                jsonWriter.WritePropertyName("PointDetails");
                serializer.Serialize(jsonWriter, bOk.Data);

                //Fetch point ledgers
                bOk = await DeleteLoyaltyAccountAllLedgersAsync(tenantId, account, fetch);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account ledgers could not be deleted" } });
                jsonWriter.WritePropertyName("Ledgers");
                serializer.Serialize(jsonWriter, bOk.Data);

                //Now fetch account and ext ref
                bOk = await DeleteLoyaltyAccountAndExtRefsInternalAsync(tenantId, account, fetch);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account or external reference could not be deleted" } });
                jsonWriter.WritePropertyName("Account");
                serializer.Serialize(jsonWriter, bOk.Data);

                jsonWriter.WriteEndObject();
                await jsonWriter.FlushAsync();
                stream.Close();

                return await _dataLakeAdapter.FileSasUriAsync(baseDir, fileName, fileSystem);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        /// <summary>
        /// Full delete of LoyaltyAccount and all associated data
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="accountId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task DeleteLoyaltyAccountAsync(string tenantId, string accountId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentNullException(nameof(accountId), "The AccountId must be provided.");

            var acct = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, accountId, ACTIVE);
            if (acct == null) throw new ArgumentNullException(nameof(acct));
            await DeleteLoyaltyAccountInternalAsync(tenantId, acct);
        }

        /// <summary>
        /// Full delete of LoyaltyAccount and all associated data
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="account"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task DeleteLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (account == null)
                throw new ArgumentNullException(nameof(account), "The Account must be provided.");

            await DeleteLoyaltyAccountInternalAsync(tenantId, account.FromDto());
        }


        internal async Task DeleteLoyaltyAccountInternalAsync(string tenantId, LoyaltyAccount account)
        {
            
            try
            {
                var bOk = await DeleteLoyaltyAccountTagsInternalAsync(tenantId, account);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account tags could not be deleted" } });

                //Delete all related events
                bOk = await DeleteLoyaltyAccountEventsInternalAsync(tenantId, account);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account events could not be deleted" } });

                //delete points details
                bOk = await DeleteLoyaltyAccountPointsDetailsInternalAsync(tenantId, account);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account points details could not be deleted" } });

                //delete point ledgers
                bOk = await DeleteLoyaltyAccountAllLedgersAsync(tenantId, account);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account ledgers could not be deleted" } });

                //now delete account and ext ref
                bOk = await DeleteLoyaltyAccountAndExtRefsInternalAsync(tenantId, account);
                if (!bOk.Success) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountInternalAsync: Loyalty Account or external reference could not be deleted" } });

                //if full account report was requested, delete it
                if (!await DeleteFullAccountReportAsync(tenantId, account)) throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteFullAccountReportAsync: Full account report could not be deleted" } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        internal async Task<bool> DeleteFullAccountReportAsync(string tenantId, LoyaltyAccount account)
        {
            try
            {
                var baseDir = $"{tenantId}/AccountData";
                var fileName = $"{account.Id}.json";
                var fileSystem = "reports";
                if (await _dataLakeAdapter.FileExistsAsync(baseDir, fileName, fileSystem))
                {
                    await _dataLakeAdapter.DeleteFileAsync(baseDir, fileName, fileSystem);
                }
                //if file does or doesn't exist return true, catch errors if unsuccessful
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        internal async Task<AccountData> DeleteLoyaltyAccountAllLedgersAsync(string tenantId, LoyaltyAccount account, bool fetch = false)
        {
            List<PointLedger> legders = [];
            try
            {
                var pagedLedgers = await _loyaltyAccountPointLedgerAdapter.FetchLoyaltyAccountLedgersAsync(tenantId, account.Id, 1000);
                if (pagedLedgers != null && pagedLedgers.Entities.Count > 0)
                {
                    var tasks = new List<Task>();
                    foreach (var ledger in pagedLedgers.Entities)
                    {
                        if (fetch)
                        {
                            legders.Add(ledger);
                        } 
                        else
                        {
                            var refTask = _loyaltyAccountPointLedgerAdapter.DeleteLoyaltyAccountLedgerAsync(tenantId, account.Id, ledger.Id);
                            tasks.Add(refTask);
                        }
                        
                    }

                    await Task.WhenAll(tasks);

                    if (tasks.Any(x => !x.IsCompletedSuccessfully))
                    {
                        throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountAllLedgersAsync: Loyalty Account ledger could not be deleted - Account id: {account.Id}" } });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return new AccountData() { Success = true, Data = legders };
        }

        internal async Task<AccountData> DeleteLoyaltyAccountPointsDetailsInternalAsync(string tenantId, LoyaltyAccount account, bool fetch = false)
        {
            List<PointsDetails> pointsDetails = [];
            try
            {
                int pageCount = 10;
                var currentResult = await _loyaltyAccountPointsDetailsAdapter.GetAllAccountPointsDetailsAsync(tenantId, account.Id, pageCount, null);

                // Process all results while there's a continuation token
                while (currentResult != null)
                {
                    if (currentResult.Entities != null)
                    {
                        //delete detail records
                        foreach (PointsDetails details in currentResult.Entities)
                        {
                            if (fetch)
                            {
                                pointsDetails.Add(details);
                            } 
                            else
                            {
                                await _loyaltyAccountPointsDetailsAdapter.DeletePointsDetailAsync(tenantId, account.Id, details.EventId, details.EventType);
                            }
                        }
                    }

                    // Get next page if there's a continuation token
                    if (!string.IsNullOrEmpty(currentResult.ContinuationToken))
                    {
                        currentResult = await _loyaltyAccountPointsDetailsAdapter.GetAllAccountPointsDetailsAsync(tenantId, account.Id, pageCount, currentResult.ContinuationToken);
                    }
                    else
                    {
                        break; // No more
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return new AccountData() { Success = true, Data = pointsDetails };
        }

        internal async Task<AccountData> DeleteLoyaltyAccountEventsInternalAsync(string tenantId, LoyaltyAccount account, bool fetch = false)
        {
            List<WrappedEventPayload> payloads = [];
            try
            {
                //Get event models (dynamic data)
                var taskModelPairs = new List<(Task<PagedResultSet<WrappedEventPayload>> Task, string ModelId)>();
                var dynamicEntityModels = await _loyaltyAccountAdapter.GetRelatedLoyaltyAccountModelsAsync(tenantId) ?? new List<ModelDto>();

                int pageCount = 10;
                Dictionary<string, ModelDto> dynModels = dynamicEntityModels?.ToDictionary(s => s.ID, s => s) ?? new Dictionary<string, ModelDto>();
                foreach (var model in dynamicEntityModels)
                {
                    var refTask = _dynamicDataAdapter.GetEntitiesByPKAsync<WrappedEventPayload>(tenantId, account.Id, model.ID, pageCount);
                    taskModelPairs.Add((refTask, model.ID));
                }

                if (taskModelPairs.Count == 0)
                {
                    //No inbound events configured for this tenant...?
                    _logger.LogInformation($"LoyaltyAccountService.DeleteLoyaltyAccountEventsInternalAsync: No inbound events configured for this tenant: {tenantId}");
                    return new AccountData() { Success = true, Data = null };
                }

                if (taskModelPairs.Count > 20)
                {
                    //TODO: make this handle more than 20 configured events
                    // ... by paging through taskModelPairs and aggregating all of the results
                    throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountEventsInternalAsync: Loyalty Account events could not be deleted, max of 20 inbound events currently supported." } });
                }

                await Task.WhenAll(taskModelPairs.Select(x => x.Task));

                if (taskModelPairs.Any(x => !x.Task.IsCompletedSuccessfully))
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountEventsInternalAsync: Loyalty Account events could not be deleted" } });
                }

                // Process all pages for each event model
                foreach (var (task, modelId) in taskModelPairs)
                {
                    var currentResult = task.Result;
                    var thisModel = (dynModels.ContainsKey(modelId)) ? dynModels[modelId] : null;
                    if (thisModel == null)
                    {
                        _logger.LogInformation($"LoyaltyAccountService.DeleteLoyaltyAccountEventsInternalAsync: No model found for id: {modelId}");
                        continue;
                    }

                    while (currentResult != null)
                    {
                        if (currentResult.Entities != null)
                        {
                            foreach (var evt in currentResult.Entities)
                            {
                                var naturalKey = evt.NaturalKey;

                                //TODO: FIx this! Need a reliable way to get the external reference type
                                var strs = thisModel.Name.Split("AndRuleStateModel");
                                var nameType = (strs.Length > 0) ? strs[0] : thisModel.Name;
                                var TypeProvider = new PathValueProvider("type");
                                string metaType = TypeProvider.GetValue<string>(evt.Event) ?? ((naturalKey.Split('|').Length > 1) ? naturalKey.Split('|')[0].ToLower() : nameType.ToLower());

                                if (fetch)
                                {
                                    payloads.Add(evt);
                                } 
                                else
                                {
                                    //Remove event
                                    var remTask = RemoveEntityWithRetryAsync(tenantId, account, evt, modelId);

                                    //fetch the ext ref for the event ...expect 1
                                    var exRefTask = _dynamicExtRefAdapter.FetchExtReferenceByKeyAsync(tenantId, metaType, naturalKey);

                                    await Task.WhenAll(remTask, exRefTask);

                                    if (!remTask.IsCompletedSuccessfully || !exRefTask.IsCompletedSuccessfully || !remTask.Result)
                                    {
                                        _logger.LogError("Failed to remove entity after retries. TenantId: {TenantId}, ModelId: {ModelId}, EntityId: {EntityId}",
                                                tenantId, modelId, evt?.Id);

                                        throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"LoyaltyAccountService.DeleteLoyaltyAccountEventsInternalAsync: Failed to remove entity after retries. TenantId: {tenantId}, ModelId: {modelId}, EntityId: {evt?.Id}" } });
                                    }

                                    //delete ext ref...log if there isn't one
                                    if (exRefTask.Result != null)
                                    {
                                        await _dynamicExtRefAdapter.DeleteExternalReferenceAsync(tenantId, exRefTask.Result.Id, exRefTask.Result.Type, exRefTask.Result.MapFromId);
                                    }
                                    else
                                    {
                                        //no ext ref is suspicious
                                        _logger.LogInformation($"LoyaltyAccountService.DeleteLoyaltyAccountEventsInternalAsync: Failed to remove entity after retries. TenantId: {tenantId}, ModelId: {modelId}, EntityId: {evt?.Id}",
                                                tenantId, modelId, evt?.Id);
                                    }
                                }
                           
                            }
                        }

                        if (!string.IsNullOrEmpty(currentResult.ContinuationToken))
                        {
                            currentResult = await _dynamicDataAdapter.GetEntitiesByPKAsync<WrappedEventPayload>(tenantId, account.Id, modelId, pageCount, continuationToken: currentResult.ContinuationToken);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return new AccountData() { Success = true, Data = payloads };
        }

        internal async Task<AccountData> DeleteLoyaltyAccountTagsInternalAsync(string tenantId, LoyaltyAccount account, bool fetch = false)
        {
            List<Tag> tags = [];
            try
            {
                //delete ext references to the loyalty account
                var currentResult = await _tagAdapter.FetchEntityTagsAsync(tenantId, account.Id, "type", 1000);

                while (currentResult != null)
                {
                    var lst = currentResult.Entities ?? new List<Tag>();
                    foreach (var tag in lst)
                    {
                        //delete tag
                        if (fetch)
                        {
                            tags.Add(tag);
                        }
                        else
                        {
                            await _tagAdapter.DeleteTagAsync(tenantId, tag);
                        }
                    }

                    if (!string.IsNullOrEmpty(currentResult.ContinuationToken))
                    {
                        currentResult = await _tagAdapter.FetchEntityTagsAsync(tenantId, account.Id, "type", 1000, continuationToken: currentResult.ContinuationToken);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return new AccountData() { Success = true, Data = tags };
        }

        internal async Task<AccountData> DeleteLoyaltyAccountAndExtRefsInternalAsync(string tenantId, LoyaltyAccount account, bool fetch = false)
        {
            List<object> data = [];
            try
            {
                //delete ext references to the loyalty account
                var lst = account.KnownExternalIds ?? new List<string>();
                foreach(var exref in lst)
                {
                    var exrefResult = await _dynamicExtRefAdapter.FetchExtReferenceByKeyAsync(tenantId, "account", exref);
                    if (exrefResult != null)
                    {
                        if (fetch)
                        {
                            data.Add(exrefResult);
                        } 
                        else
                        {
                            await _dynamicExtRefAdapter.DeleteExternalReferenceAsync(tenantId, exrefResult.Id, exrefResult.Type, exrefResult.MapFromId);
                        }
                    }
                }

                if (fetch)
                {
                    data.Add(await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, account.Id, account.Status));
                } 
                else
                {
                    //now delete the loyalty account itself ...last thing to delete
                    await _loyaltyAccountAdapter.DeleteLoyaltyAccountAsync(tenantId, account.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return new AccountData() { Success = true, Data = data};
        }

        /// <summary>
        /// Soft delete by moving to Deleted partition
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="accountId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task SoftDeleteLoyaltyAccountAsync(string tenantId, string accountId)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) 
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrWhiteSpace(accountId)) 
                throw new ArgumentNullException(nameof(accountId), "The AccountId must be provided.");

            try
            {
                var resTask = _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, accountId, ACTIVE);
                var loyaltyDetailsTask = _dynamicDataAdapter.GetEntitiesByPKAsync<WrappedEventPayload>(tenantId, accountId, LOYALTY_DETAILS, 1);
                await Task.WhenAll(resTask, loyaltyDetailsTask);

                if (!resTask.IsCompletedSuccessfully || !loyaltyDetailsTask.IsCompletedSuccessfully)
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"Loyalty Account could not be fetched" } });
                }

                var res = resTask.Result;
                var loyaltyDetails = loyaltyDetailsTask.Result?.Entities?.FirstOrDefault();

                if (res != null && loyaltyDetails != null)
                {
                    loyaltyDetails.DbStatus = DELETED;
                    var updatedDetailsTask = _dynamicExtRefAdapter.UpsertEntityAsync(tenantId: tenantId, entity: loyaltyDetails, modelId: LOYALTY_DETAILS, xId: loyaltyDetails.NaturalKey, type: loyaltyDetails.MetaType);
                    var moveTask = _loyaltyAccountAdapter.MoveLoyaltyAccountAsync(tenantId, res, new Dictionary<string, string> 
                    { 
                        { "TenantId", tenantId },
                        { "status", DELETED }
                    });
                    await Task.WhenAll(moveTask, updatedDetailsTask);

                    if (!moveTask.IsCompletedSuccessfully || !updatedDetailsTask.IsCompletedSuccessfully)
                    {
                        _logger.LogCritical("Deleting Loyalty Account Failed: this error requires manual intervention");
                        throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"Loyalty Account could not be deleted" } });
                    }
                } else
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"The Loyalty Account has already been deleted" } });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        /// <summary>
        /// Soft delete by moving to Deleted partition
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="account"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task SoftDeleteLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) 
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (account == null)
                throw new ArgumentNullException(nameof(account), "The Account must be provided.");

            try
            {
                //await _loyaltyAccountAdapter.DeleteLoyaltyAccountAsync(tenantId, account.FromDto());
                await _loyaltyAccountAdapter.MoveLoyaltyAccountAsync(tenantId, account.FromDto(), new Dictionary<string, string>
                    {
                        { "TenantId", tenantId },
                        { "status", DELETED }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        #endregion

        #region Loyalty Account Points

        public async Task<List<PointLedgerDto>> BringLoyaltyAccountPointsCurrentAsync(string tenantId, LoyaltyAccount loyaltyAccount, bool capilatizecapitalizeType = false)
        {
            var results = await BringLoyaltyAccountPointsCurrentInternalAsync(tenantId, loyaltyAccount);
            return results?.Select(x =>
            {
                var val = x.ToDto(capilatizecapitalizeType);
                val.ResettleASAP = loyaltyAccount.ResettleASAP;
                return val;
            })?.ToList();
        }

        public async Task<List<PointLedger>> BringLoyaltyAccountPointsCurrentInternalAsync(string tenantId, LoyaltyAccount loyaltyAccount)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (loyaltyAccount == null)
                throw new ArgumentNullException(nameof(loyaltyAccount), "The LoyaltyAccount must be provided.");

            var res = loyaltyAccount.PointLedgers;
            if (res == null)
            {
                var lst = await _loyaltyAccountPointLedgerAdapter.FetchLoyaltyAccountLedgersAsync(tenantId, loyaltyAccount.Id, 100);
                if (!string.IsNullOrEmpty(lst?.ContinuationToken)) throw new Exception($"Maximum number of Point Ledgers has been exceeded for account: {loyaltyAccount.Id}");
                res = lst.Entities ?? new List<PointLedger>();
            }

            var pointAccountTypes = await GetPointAccountTypes(tenantId) ?? new List<PointAccountType>();
            if (pointAccountTypes?.Count > 0)
            {
                if (!res?.Any() ?? true)
                {
                    //Create them since they don't exist
                    // and just return as there can't be any entries to expire
                    var defs = await SetDefaultPointLedgersAsync(tenantId, loyaltyAccount, pointAccountTypes);
                    return defs?.Select(x => x.FromDto())?.ToList();
                }
                else if (res.Count < pointAccountTypes.Count)
                {
                    var pats = res.Select(x => x.PointAccountTypeId).ToList();
                    _ = await SetDefaultPointLedgersAsync(tenantId, loyaltyAccount, pointAccountTypes.Where(x => !pats.Contains(x.Id)).ToList());
                }

                var ptAccountTypes = res.Select(x => x.PointAccountTypeId).ToList();
                if (ptAccountTypes?.Any() ?? false)
                {
                    //Assumes this account has already been locked
                    var leds = await ExpirePoints(tenantId, loyaltyAccount, ptAccountTypes, 1.0m, true,
                                       x => x.ExpirationDate != null && x.ExpirationDate > DateTimeOffset.MinValue && x.ExpirationDate <= DateTimeOffset.UtcNow);
                    return leds;
                }
                return res;
            }
            return null;
        }

        public async Task<List<PointLedgerDto>> GetLoyaltyAccountPointsAsync(string tenantId, string loyaltyAccountId, bool resettleIfNeeded = false, bool capilatizecapitalizeType = false)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrWhiteSpace(loyaltyAccountId))
                throw new ArgumentNullException(nameof(loyaltyAccountId), "The LoyaltyAccountId must be provided.");

            try
            {
                var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, loyaltyAccountId, ACTIVE);
                if (loyaltyAccount == null)
                    return null;

                if (resettleIfNeeded)
                {
                    return await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount, capilatizecapitalizeType);
                }
                var res = loyaltyAccount.PointLedgers;
                if (res == null)
                {
                    var lst = await _loyaltyAccountPointLedgerAdapter.FetchLoyaltyAccountLedgersAsync(tenantId, loyaltyAccount.Id, 100);
                    if (!string.IsNullOrEmpty(lst?.ContinuationToken)) throw new Exception($"Maximum number of Point Ledgers has been exceeded for account: {loyaltyAccount.Id}");
                    res = lst.Entities ?? new List<PointLedger>();
                }

                return res?.Select(x => x.ToDto(capilatizecapitalizeType)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return null;
        }

        public async Task<PointLedgerDto> BulkDepositPointsAsync(string tenantId, BulkPointDepositRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(request.LoyaltyAccountId))
            {
                throw new Exception("Invalid request.");
            }

            if (!request.PointsDepositEntries?.Any() ?? true)
            {
                return null;
            }

            if (request.PointsDepositEntries.Any(x => x.DepositDate == null || x.DepositDate == DateTimeOffset.MinValue))
            {
                throw new Exception("deposit date is required");
            }

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {request.LoyaltyAccountId}");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {request.LoyaltyAccountId}.");

            try
            {
                await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                PointLedgerDto savedLedger = null;
                var groupedByPointAccountType = request.PointsDepositEntries
                                                    .GroupBy(x => x.PointAccountTypeId)
                                                    .ToDictionary(y => y.Key, y => y.Select(z => z)
                                                    .ToList()) ?? new Dictionary<string, List<PointDepositlDetails>>();

                foreach (var patDeposit in groupedByPointAccountType.Keys)
                {
                    var lst = groupedByPointAccountType[patDeposit]
                                .Select(x => new LedgerEntryDto
                                {
                                    EventId = x.EventId,
                                    EventType = x.EventType,
                                    PointsWithdrawn = x.Amount,
                                    EarnDate = x.DepositDate,
                                    Status = x.Status,
                                    UserId = x.UserId,
                                    Type = LedgerEntryType.Deposit
                                })
                                .ToList();

                    var ledger = new PointLedgerDto
                    {
                        AccountId = request.LoyaltyAccountId,
                        PointAccountTypeId = patDeposit,
                        TenantId = tenantId,
                        LedgerEntries = lst
                    };

                    savedLedger = await UpsertLoyaltyAccountPointsAsync(tenantId, ledger, loyaltyAccount);
                }
                return savedLedger;
            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
        }

        public async Task<PointLedgerDto> DepositPointsAsync(string tenantId, PointDespositRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(request.LoyaltyAccountId))
            {
                throw new Exception("Invalid request.");
            }

            if (request.DepositDate == null || request.DepositDate == DateTimeOffset.MinValue)
            {
                throw new Exception("Deposit date is required");
            }

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {request.LoyaltyAccountId}");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {request.LoyaltyAccountId}.");

            try
            {
                await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                var ledger = new PointLedgerDto
                {
                    AccountId = request.LoyaltyAccountId,
                    PointAccountTypeId = request.PointAccountTypeId,
                    TenantId = tenantId,
                    LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = request.EventId,
                        EventType = request.EventType,
                        EarnDate = request.DepositDate,
                        PointsDeposited = request.Amount,
                        SpendablePoints = request.Amount,
                        Status = request.Status,
                        UserId = request.UserId,
                        Type = LedgerEntryType.Deposit
                    }
                }
                };

                var savedLedger = await UpsertLoyaltyAccountPointsAsync(tenantId, ledger, loyaltyAccount);
                return savedLedger;
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
        }

        public async Task<PointLedgerDto> BulkWithdrawPointsAsync(string tenantId, BulkPointWithdrawalRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(request.LoyaltyAccountId))
            {
                throw new Exception("Invalid request.");
            }

            if (!request.PointsWithdrawalEntries?.Any() ?? true)
            {
                return null;
            }

            if (request.PointsWithdrawalEntries.Any(x => x.WithdrawalDate == null || x.WithdrawalDate == DateTimeOffset.MinValue))
            {
                throw new Exception("Withdrawl date is required");
            }

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {request.LoyaltyAccountId}");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {request.LoyaltyAccountId}.");

            var ledgersList = new List<PointLedgerDto>();
            try
            {
                await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                PointLedgerDto savedLedger = null;
                var groupedByPointAccountType = request.PointsWithdrawalEntries
                                                    .GroupBy(x => x.PointAccountTypeId)
                                                    .ToDictionary(y => y.Key, y => y.Select(z => z)
                                                    .ToList()) ?? new Dictionary<string, List<PointWithdrawalDetails>>();
                
                foreach (var patWithdrawl in groupedByPointAccountType.Keys)
                {
                    var pointAccountType = await _cache.GetPointAccountTypeAsync(tenantId, patWithdrawl);
                    if (pointAccountType == null)
                        throw new Exception("Unable to resolve the PointAccountType for this ledger");

                    int places = Convert.ToInt32(pointAccountType.RoundingDecimalPlaces);
                    var lst = groupedByPointAccountType[patWithdrawl]
                                .Select(x => new LedgerEntryDto
                                {
                                    EventId = x.EventId,
                                    EventType = x.EventType,
                                    PointsWithdrawn = Math.Round(x.Amount, places, pointAccountType.RoundingOption),
                                    BurnDate = x.WithdrawalDate,
                                    Status = x.Status,
                                    UserId = x.UserId,
                                    Type = LedgerEntryType.Withdrawl
                                })
                                .ToList();

                    var ledger = new PointLedgerDto
                    {
                        AccountId = request.LoyaltyAccountId,
                        PointAccountTypeId = patWithdrawl,
                        TenantId = tenantId,
                        LedgerEntries = lst
                    };

                    savedLedger = await UpsertLoyaltyAccountPointsAsync(tenantId, ledger, loyaltyAccount);
                    ledgersList.Add(savedLedger);
                }
                return savedLedger;
            }
            catch(Exception ex)
            {

            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
            return null;
        }

        public async Task<PointLedgerDto> WithdrawPointsAsync(string tenantId, PointWithdrawlRequest request)
        {
            if (request == null || string.IsNullOrEmpty(tenantId) || string .IsNullOrEmpty(request.LoyaltyAccountId))
            {
                throw new Exception("Invalid request.");
            }

            if (request.WithdrawalDate == null || request.WithdrawalDate == DateTimeOffset.MinValue)
            {
                throw new Exception("Withdrawl date is required");
            }

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {request.LoyaltyAccountId}");

            var pointAccountType = await _cache.GetPointAccountTypeAsync(tenantId, request.PointAccountTypeId);
            if (pointAccountType == null)
                throw new Exception("Unable to resolve the PointAccountType for this ledger");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {request.LoyaltyAccountId}.");

            PointLedgerDto returnLedger = new PointLedgerDto();
            try
            {
                await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                int places = Convert.ToInt32(pointAccountType.RoundingDecimalPlaces);
                var ledger = new PointLedgerDto
                {
                    AccountId = request.LoyaltyAccountId,
                    PointAccountTypeId = request.PointAccountTypeId,
                    TenantId = tenantId,
                    LedgerEntries = new List<LedgerEntryDto>
                    {
                        new LedgerEntryDto
                        {
                            EventId = request.EventId,
                            EventType = request.EventType,
                            PointsWithdrawn = Math.Round( request.Amount, places, pointAccountType.RoundingOption),
                            BurnDate = request.WithdrawalDate,
                            Status = request.Status,
                            UserId = request.UserId,
                            Type = LedgerEntryType.Withdrawl
                        }
                    }
                };

                returnLedger = await UpsertLoyaltyAccountPointsAsync(tenantId, ledger, loyaltyAccount);
                return returnLedger;
            }
            catch(Exception ex)
            {
                throw;
            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
        }

        public async Task<PointLedgerDto> AdminUpsertLoyaltyAccountPointsAsync(string tenantId, PointLedgerDto ledger)
        {
            if (ledger == null || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(ledger.AccountId))
            {
                throw new Exception("Invalid request.");
            }

            try
            {
                if (ledger != null)
                {
                    var res = await _loyaltyAccountPointLedgerAdapter.UpsertLoyaltyAccountLedgerAsync(tenantId, ledger.FromDto());
                    return res?.ToDto();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            return null;
        }

        public async Task<PointLedgerDto> UpsertLoyaltyAccountPointsAsync(string tenantId, PointLedgerDto ledger, LoyaltyAccount account)
        {
            if (ledger == null || (ledger.LedgerEntries?.Count ?? 0) == 0) return null;

            if (string.IsNullOrEmpty(tenantId))
                throw new Exception("The TenantId must be provided.");

            if (string.IsNullOrEmpty(ledger.PointAccountTypeId))
                throw new Exception("The PointAccountTypeId must be provided.");

            if (string.IsNullOrEmpty(ledger.AccountId))
                throw new Exception("The account id must be provided.");

            var pointAccountType = await _cache.GetPointAccountTypeAsync(tenantId, ledger.PointAccountTypeId);

            if (pointAccountType == null)
                throw new Exception("Unable to resolve the PointAccountType for this ledger");

            if (ledger.LedgerEntries.Any(e => string.IsNullOrEmpty(e.EventType)) || ledger.LedgerEntries.Any(e => string.IsNullOrEmpty(e.EventId)))
                throw new Exception("One or more ledger entries are missing an Event Type and/or Id");

            var bOk = await EnsureAccountInValidStateAsync(tenantId, account);
            if (!bOk && ledger.LedgerEntries.FirstOrDefault(x => x.EventType.Equals(INIT, StringComparison.InvariantCultureIgnoreCase)) == null) throw new Exception("The provided loyalty account is in an invalid state.");

            var inboundNetDep = ledger.LedgerEntries.Sum(x => x.PointsDeposited ?? 0) - ledger.LedgerEntries.Sum(x => x.PointsWithdrawn ?? 0);

            PointLedgerDto returnLedger = null;
            decimal? adjustedSpend = null;
            bool bFail = false;
            try
            {
                if (!string.IsNullOrEmpty(account.LockLeaseKey) && (account.LockLeaseExpiration ?? DateTimeOffset.MinValue) > DateTimeOffset.UtcNow)
                {
                    //Verify lock for concurrency check
                    var storedAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, ledger.AccountId, ACTIVE);
                    if (storedAccount == null)
                    {
                        throw new Exception($"No loyalty account found for the provided account id: {ledger.AccountId}");
                    }
                    if (!storedAccount.LockLeaseKey?.Equals(account.LockLeaseKey, StringComparison.InvariantCultureIgnoreCase) ?? false)
                    {
                        _logger.LogError($"Concurrency check failure, {account.Id}, {account.LockLeaseKey}, {storedAccount.LockLeaseKey}");
                        throw new Exceptions.ConcurrencyException("Concurrency check failure", account.Id, account.LockLeaseKey, storedAccount.LockLeaseKey);
                    }
                }

                //First, get account points
                var storedLedgers = await _loyaltyAccountPointLedgerAdapter.FetchLoyaltyAccountLedgersAsync(tenantId, ledger.AccountId, 100);
                if (!string.IsNullOrEmpty(storedLedgers?.ContinuationToken)) throw new Exception($"Maximum number of Point Ledgers has been exceeded for account: {ledger.AccountId}");

                //Match the stored ledger type to the inbound ledger type
                var storedLedger = storedLedgers?.Entities?.FirstOrDefault(x => !string.IsNullOrEmpty(ledger.Id) && x.Id == ledger.Id);
                storedLedger ??= storedLedgers?.Entities?.FirstOrDefault(x => !string.IsNullOrEmpty(x.PointAccountTypeId) && x.PointAccountTypeId.Equals(ledger.PointAccountTypeId));

                bool bIsNew = false;
                if (storedLedger == null)
                {
                    //This is a new (to this account) ledger (type)
                    storedLedger = new PointLedger(ledger.AccountId, ledger.PointAccountTypeId, 0, 0, 1, null, tenantId, ledger.Id);
                    bIsNew = true;
                }

                //Ensure initialized
                storedLedger.CurrentBalance ??= 0;
                storedLedger.LifetimeTotal ??= 0;
                storedLedger.Entries ??= new Dictionary<string, List<LedgerEntry>>();
                var inboundSpends = new List<LedgerEntry>();

                //Update account ledger with inbound entry(s)
                foreach (var entry in ledger.LedgerEntries)
                {
                    if (string.IsNullOrEmpty(entry.EventId) || string.IsNullOrEmpty(entry.EventId))
                        throw new Exception($"LoyaltyAccountService::UpsertLoyaltyAccountPointsAsync - Each ledger entry must have an associated Event Type and Id");

                    entry.EntryId ??= Guid.NewGuid().ToString();

                    var entryKey = EventKeyUtility.ToEventKey(entry.EventType, entry.EventId);
                    bool isDep = (entry.PointsWithdrawn == null || entry.PointsWithdrawn == 0); //then it's likely a deposit
                    isDep = isDep || (entry.PointsDeposited ?? 0) != 0;
                    isDep = isDep || !entry.EarnDate.IsNullOrMinDate();

                    //Add or update ledger entries...updating only works when provided and EventId
                    if (storedLedger.Entries.ContainsKey(entryKey)) 
                    {
                        var entrytoupdate = storedLedger.Entries[entryKey]
                                                .FirstOrDefault(e => e.EventId.Equals(entry.EventId, StringComparison.InvariantCultureIgnoreCase) &&
                                                e.EventType.Equals(entry.EventType, StringComparison.InvariantCultureIgnoreCase));

                        if (entrytoupdate == null)
                        {
                            //Should never get in here ...but
                            entrytoupdate = entry.FromDto().Clone();
                            storedLedger.Entries[entryKey] ??= new List<LedgerEntry>();
                            storedLedger.Entries[entryKey].Add(entrytoupdate);
                        }

                        if (entrytoupdate != null)
                        {
                            //update entry
                            var netPtsAdd = (entry.PointsDeposited ?? 0) - (entrytoupdate.PointsDeposited ?? 0);
                            var netPtsSub = (entry.PointsWithdrawn ?? 0) - (entrytoupdate.PointsWithdrawn ?? 0);
                            var totalAdjustment = (netPtsAdd) - (netPtsSub);

                            storedLedger.CurrentBalance += totalAdjustment;
                            //Since this in an update, we want to make sure Lifetime total is handled appropriately,
                            //even if it means reducing the value
                            storedLedger.LifetimeTotal += totalAdjustment;

                            //Ensure expiration is set
                            try
                            {
                                if (isDep && entry.EarnDate.IsNullOrMinDate())
                                    throw new Exception($"LoyaltyAccountService::UpsertLoyaltyAccountPointsAsync - Every (net) deposit must provide an Earn Date.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex);
                                throw;
                            }

                            //Update SpendReferences only if null
                            //....we may need to consolidate if some are being passed in and some exist
                            entrytoupdate.SpendReferences ??= entry.SpendReferences;
                            entrytoupdate.SpendablePoints += totalAdjustment;
                            entrytoupdate.PointsDeposited = entry.PointsDeposited;
                            entrytoupdate.PointsWithdrawn = entry.PointsWithdrawn;
                            entrytoupdate.EarnDate = entry.EarnDate;
                            entrytoupdate.BurnDate = entry.BurnDate;
                            entrytoupdate.Status = entry.Status;
                            entrytoupdate.UserId = entry.UserId;

                            if (entrytoupdate.ExpirationDate.IsNullOrMinDate())
                            {
                                entrytoupdate.ExpirationDate = ((!pointAccountType.PointsLifespanEndDate.IsNullOrMinDate()) ?
                                                                    pointAccountType.PointsLifespanEndDate :
                                                                    entrytoupdate.EarnDate.Value.AddDays((double)(pointAccountType.PointsLifespanDays ?? 0)));
                            }

                            if (isDep)
                            {
                                entrytoupdate.Type = entry.Type ?? "+";
                            }
                            else
                            {
                                entrytoupdate.Type = entry.Type ?? "-";

                                if (entry.PointsWithdrawn > 0 && totalAdjustment != 0)
                                    inboundSpends.Add(entrytoupdate);
                            }
                            
                            continue;
                        }
                        else
                        {
                            //This seems bad...
                            _logger.LogWarning($"LoyaltyAccountService::UpsertLoyaltyAccountPointsAsync - empty or no ledger entry found: {entryKey}");
                        }
                    }

                    var adjustment = (entry.PointsDeposited ?? 0) - (entry.PointsWithdrawn ?? 0);
                    if (isDep && entry.EarnDate.IsNullOrMinDate())
                        throw new Exception($"LoyaltyAccountService::UpsertLoyaltyAccountPointsAsync - Every (net) deposit must provide an Earn Date.");

                    storedLedger.CurrentBalance += adjustment;
                    storedLedger.LifetimeTotal += entry.PointsDeposited ?? 0;
                    
                    if (isDep)
                    {
                        entry.SpendablePoints ??= 0;
                        entry.SpendablePoints += adjustment;
                        entry.Type ??= "+";
                        if (entry.ExpirationDate.IsNullOrMinDate())
                        {
                            entry.ExpirationDate = ((!pointAccountType.PointsLifespanEndDate.IsNullOrMinDate()) ?
                                                                pointAccountType.PointsLifespanEndDate :
                                                                entry.EarnDate.Value.AddDays((double)(pointAccountType.PointsLifespanDays ?? 0)));
                        }
                    }
                    else
                    {
                        entry.Type ??= "-";
                    }

                    var entrymodel = entry.FromDto();

                    if (!isDep)
                        inboundSpends.Add(entrymodel);

                    storedLedger.Entries.Add(entryKey, new List<LedgerEntry> { entrymodel });
                }

                //TODO: Add config check for allowing balances to go negatives
                var bAllowNegativeBalance = true;
                if (storedLedger.CurrentBalance < 0 && !bAllowNegativeBalance)
                {
                    //TODO: THrow exception...?
                }

                //process inboundSpends
                if ((inboundSpends?.Count ?? 0) > 0)
                {
                    //Go through deposits, oldest first by ExpirationDate (so closest to expire)
                    // looking for SpendablePoints > 0,
                    // for those, reduce SpendablePoints until either
                    // the spend amount is satisfied or it hits zero,
                    // in which case, move to the next entry (next youngest)
                    // and reduce there...etc.
                    //For each deposit we reduce we add the spend reference to the deposit
                    var deps = storedLedger.Entries?
                                .SelectMany(d => d.Value)?
                                .Where(d => d.SpendablePoints > 0)?
                                .OrderBy(d => d.ExpirationDate)
                                .ToList();

                    if ((deps?.Count ?? 0) > 0)
                    {
                        var curSpend = 0;
                        var curDeposit = 0;
                        var remainingSpend = (inboundSpends[curSpend].PointsDeposited ?? 0) - (inboundSpends[curSpend].PointsWithdrawn ?? 0);
                        //as this is (should be) negative we need to invert it
                        if (remainingSpend > 0) throw new Exception("Something weird happened");
                        remainingSpend *= -1;
                        while (remainingSpend > 0 && curDeposit < deps.Count)
                        {
                            var eventKey = EventKeyUtility.ToEventKey(inboundSpends[curSpend].EventType, inboundSpends[curSpend].EventId);

                            if (deps[curDeposit].SpendablePoints > remainingSpend)
                            {
                                deps[curDeposit].SpendablePoints -= remainingSpend;
                                deps[curDeposit].SpendReferences ??= new List<string>();
                                if (!deps[curDeposit].SpendReferences.Contains(eventKey))
                                    deps[curDeposit].SpendReferences.Add(eventKey);
                                
                                curSpend++;
                                remainingSpend = curSpend < inboundSpends.Count ?
                                                    (inboundSpends[curSpend].PointsDeposited ?? 0) - (inboundSpends[curSpend].PointsWithdrawn ?? 0) :
                                                    0;
                                continue;
                            }
                            else
                            {
                                if (deps[curDeposit].SpendablePoints > 0)
                                {
                                    deps[curDeposit].SpendReferences ??= new List<string>();
                                    if (!deps[curDeposit].SpendReferences.Contains(eventKey))
                                        deps[curDeposit].SpendReferences.Add(eventKey);
                                }
                                remainingSpend -= deps[curDeposit].SpendablePoints ?? 0;
                                deps[curDeposit].SpendablePoints = 0;
                                curDeposit++;
                                continue;
                            }
                        }
                        if (!bAllowNegativeBalance && remainingSpend > 0)
                        {
                            //Throw an exception....?
                        }
                    }
                    else
                    {
                        //there are no deposits from which to pull points so throw exception
                        _logger.LogError($"LoyaltyAccountService::UpsertLoyaltyAccountPointsAsync - there are no deposits from which to pull points for account: {account.ExtAccountId} - {account.Id}");
                        throw new Exception($"LoyaltyAccountService::UpsertLoyaltyAccountPointsAsync - there are no deposits from which to pull points for account: {account.ExtAccountId} - {account.Id}");
                    }
                }

                //Save the (whole) updated PointLedger for the account
                var res = await _loyaltyAccountPointLedgerAdapter.UpsertLoyaltyAccountLedgerAsync(tenantId, storedLedger);
                returnLedger = res?.ToDto();
            }
            catch (Exception ex)
            {
                bFail = true;
                _logger.LogError(ex);
                throw;
            }
            finally
            {
                if (!bFail)
                {
                    //Publish notification job
                    //await _publishEndpoint.Publish<SubmitJob<PointLedgerDto>>(new
                    //{
                    //    JobId = NewId.NextGuid(),
                    //    Job = returnLedger
                    //});
                }
            }
            return returnLedger;
        }

        //public async Task<List<PointLedgerDto>> SetAllDefaultPointLedgers(string tenantId, LoyaltyAccount account)
        //{
        //    //Get all point account types
        //    var pointAccountTypes = await GetPointAccountTypes(tenantId) ?? new List<PointAccountType>();
        //    if (pointAccountTypes.Any())
        //    {
        //        return await SetDefaultPointLedgersAsync(tenantId, account, pointAccountTypes);
        //    }
        //    return null;
        //}

        public async Task<List<PointLedgerDto>> SetDefaultPointLedgersAsync(string tenantId, LoyaltyAccount account, List<PointAccountType> types)
        {
            var returnLedgers = new List<PointLedgerDto>();
            if (string.IsNullOrEmpty(tenantId) || account?.Id == null || !types.Any())
            {
                throw new Exception("Invalid request.");
            }

            try
            {
                foreach (var pat in types)
                {
                    var entries = new List<LedgerEntryDto>
                        {
                            new LedgerEntryDto
                            {
                                EarnDate = DateTime.UtcNow,
                                EntryId = Guid.NewGuid().ToString(),
                                EventId = "creation",
                                EventType = INIT,
                                ExpirationDate = DateTime.UtcNow.AddYears(4),
                                PointsDeposited = 0,
                                SpendablePoints = 0,
                                Type = LedgerEntryType.Deposit
                            }
                        };

                    var ledger = new PointLedgerDto
                    {
                        AccountId = account.Id,
                        PointAccountTypeId = pat.Id,
                        TenantId = tenantId,
                        CurrentBalance = 0,
                        LifetimeTotal = 0,
                        PageNumber = 1,
                        LedgerEntries = entries
                    };

                    var savedLedger = await UpsertLoyaltyAccountPointsAsync(tenantId, ledger, account);
                    if (savedLedger != null)
                    {
                        returnLedgers.Add(savedLedger);
                    }
                }
                return returnLedgers;
            }
            catch (Exception ex)
            {
                //TODO: log this
                throw;
            }
        }

        public async Task<List<PointLedger>> ExpireLoyaltyAccountPointsByEarnDate(string tenantId, string loyaltyAccountId, List<string> acctTypeIds, DateTimeOffset date, decimal amountPercent)
        {
            if (acctTypeIds?.Count == 0) return null;

            ValidateParametersDate(tenantId, loyaltyAccountId, date);
            ValidateParametersAmount(amountPercent, true);

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, loyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {loyaltyAccountId}");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {loyaltyAccountId}.");

            try
            {
                //await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                return await ExpirePoints(tenantId, loyaltyAccount, acctTypeIds, amountPercent, true,
                                            x => x.EarnDate != null && x.EarnDate > DateTime.MinValue && x.EarnDate <= date);
            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
        }

        public async Task<List<PointLedger>> ExpireLoyaltyAccountPointsByExpirationDate(string tenantId, string loyaltyAccountId, List<string> acctTypeIds, DateTimeOffset expDate, decimal amountPercent)
        {
            if (acctTypeIds?.Count == 0) return null;

            ValidateParametersDate(tenantId, loyaltyAccountId, expDate);
            ValidateParametersAmount(amountPercent, true);

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, loyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {loyaltyAccountId}");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {loyaltyAccountId}.");

            try
            {
                //await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                return await ExpirePoints(tenantId, loyaltyAccount, acctTypeIds, amountPercent, true,
                                            x => x.ExpirationDate != null && x.ExpirationDate > DateTime.MinValue && x.ExpirationDate <= expDate);
            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
        }

        public async Task<List<PointLedger>> ExpireLoyaltyAccountPointsByEventId(string tenantId, string loyaltyAccountId, List<string> acctTypeIds, string eventId, decimal amount, bool isPercent)
        {
            if (acctTypeIds?.Count == 0) return null;

            ValidateParametersDate(tenantId, loyaltyAccountId, null);
            ValidateParametersAmount(amount, isPercent);
            if (string.IsNullOrEmpty(eventId))
                throw new Exception("The Event Id must be provided");

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, loyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {loyaltyAccountId}");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {loyaltyAccountId}.");

            try
            {
                //await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                return await ExpirePoints(tenantId, loyaltyAccount, acctTypeIds, amount, isPercent,
                                            x => x.EventId.Equals(eventId, StringComparison.InvariantCultureIgnoreCase));
            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
        }

        public async Task<List<PointLedger>> ExpireLoyaltyAccountPointsByAmount(string tenantId, string loyaltyAccountId, List<string> acctTypeIds, decimal amount, bool isPercent)
        {
            if (acctTypeIds.Count == 0) return null;

            ValidateParametersDate(tenantId, loyaltyAccountId, null);
            ValidateParametersAmount(amount, isPercent);

            var loyaltyAccount = await _loyaltyAccountAdapter.FetchLoyaltyAccountAsync(tenantId, loyaltyAccountId, ACTIVE);
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount found for the provided accountid: {loyaltyAccountId}");

            loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 250);
            if (loyaltyAccount == null) throw new Exception($"Failed to acquire lock on account: {loyaltyAccountId}.");

            try
            {
                await BringLoyaltyAccountPointsCurrentAsync(tenantId, loyaltyAccount);

                //First, get account points
                var storedLedgersSet = await _loyaltyAccountPointLedgerAdapter.FetchLoyaltyAccountLedgersAsync(tenantId, loyaltyAccountId, 100);
                if (!string.IsNullOrEmpty(storedLedgersSet.ContinuationToken)) throw new Exception($"Maximum number of PointLedgers has been exceeded for account: {loyaltyAccountId}");
                if (storedLedgersSet == null) return null;

                var storedLedgers = storedLedgersSet.Entities;

                foreach (var atid in acctTypeIds)
                {
                    var ledger = storedLedgers.FirstOrDefault(x => x.PointAccountTypeId.Equals(atid));
                    if (ledger == null) continue;

                    //expire from oldest to newest
                    var deps = ledger.Entries?
                                    .SelectMany(d => d.Value)?
                                    .Where(d => d.SpendablePoints > 0)?
                                    .OrderBy(d => d.ExpirationDate)
                                    .ToList();

                    decimal pointsAffected = 0;
                    List<LedgerEntry> expEntries = new List<LedgerEntry>();
                    if ((deps?.Count ?? 0) > 0)
                    {
                        var curDeposit = 0;
                        var expPtsRemaining = !isPercent ? amount :
                                             amount * ((deps[curDeposit].PointsDeposited ?? 0) - (deps[curDeposit].PointsWithdrawn ?? 0));
                        while (expPtsRemaining > 0 && curDeposit < deps.Count)
                        {
                            if (deps[curDeposit].SpendablePoints > expPtsRemaining)
                            {
                                //this deposit entry's Spendable points is greater than the ex points
                                //Reduce Spendable by expiring point amount and zero out exp points remaining
                                var expEntry = deps[curDeposit].Clone();
                                expEntry.PointsDeposited = expPtsRemaining;
                                expEntry.ExpirationDate = DateTimeOffset.UtcNow;
                                expEntries.Add(expEntry);

                                expPtsRemaining = 0;
                                continue;
                            }
                            else
                            {
                                //this deposit entry's Spendable points is less than exp points
                                //So take it all and move to the next deposit entry
                                expPtsRemaining -= deps[curDeposit].SpendablePoints ?? 0;
                                expEntries.Add(deps[curDeposit]);
                                deps.Remove(deps[curDeposit]);

                                curDeposit++;
                                continue;
                            }
                        }

                        if (expEntries.Count > 0)
                        {
                            await SaveLedgerExpirations(storedLedgers, ledger, expEntries, amount, isPercent);
                            //ledger = (led != null) ? led : ledger;
                        }
                    }
                }
                return storedLedgers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            finally
            {
                loyaltyAccount.LockLeaseExpiration = null;
                loyaltyAccount.LockLeaseKey = null;
                await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
            }
        }


        public async Task<List<PointsDetailsResponse>> BulkUpsertPointsDetails(string tenantId, BulkUpsertPointsDetailsRequest request)
        {
            List<PointsDetailsResponse> results = new List<PointsDetailsResponse>();
            if (request == null || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(request.LoyaltyAccountId))
            {
                throw new Exception("Invalid request.");
            }

            if (!request.PointsDetails?.Any() ?? true)
            {
                return null;
            }

            if (request.PointsDetails.Any(x => string.IsNullOrEmpty(x.EventId) || string.IsNullOrEmpty(x.EventType)))
            {
                throw new Exception("Event Type and Id are required");
            }

            foreach (var req in request.PointsDetails)
            {

                var res = await _loyaltyAccountPointsDetailsAdapter.UpsertPointsDetailsAsync(tenantId,
                    new PointsDetails(request.LoyaltyAccountId, req.EventId.ToLower(), req.EventType.ToLower(), req.Name, req.Description, req.Quantity, tenantId, req.Id));

                if (res != null)
                {
                    results.Add(new PointsDetailsResponse
                    {
                        Id = res.Id,
                        LoyaltyAccountId = res.LoyaltyAccountId,
                        EventId = res.EventId.ToLower(),
                        EventType = res.EventType.ToLower(),
                        Name = res.Name,
                        Description = res.Description,
                        Quantity = res.Quantity
                    });
                }
            }
            return results;
        }

        public async Task<PointsDetailsResponse> UpsertPointsDetails(string tenantId, UpsertPointsDetailsRequest req)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            
            if (req == null)
                throw new ArgumentNullException(nameof(req), "The points details must be provided.");

            if (string.IsNullOrWhiteSpace(req.LoyaltyAccountId))
                throw new ArgumentNullException(nameof(req.LoyaltyAccountId), "The LoyaltyAccountId must be provided.");

            if (string.IsNullOrEmpty(req.EventId) || string.IsNullOrEmpty(req.EventType))
                throw new ArgumentNullException(nameof(req), "The Event Type and Id must be provided.");

            try
            {
                var res = await _loyaltyAccountPointsDetailsAdapter.UpsertPointsDetailsAsync(tenantId, 
                    new PointsDetails(req.LoyaltyAccountId, req.EventId.ToLower(), req.EventType.ToLower(), req.Name, req.Description, req.Quantity, tenantId, req.Id));

                if (res != null)
                {
                    return new PointsDetailsResponse
                    {
                        Id = res.Id,
                        LoyaltyAccountId = res.LoyaltyAccountId,
                        EventId = res.EventId.ToLower(),
                        EventType = res.EventType.ToLower(),
                        Name = res.Name,
                        Description = res.Description,
                        Quantity = res.Quantity
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return null;

        }

        public async Task<PointsDetailsResponse> GetPointsDetails(string tenantId, string loyaltyAccountId, string pointEventType, string pointEventId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            
            if (string.IsNullOrWhiteSpace(loyaltyAccountId))
                throw new ArgumentNullException(nameof(loyaltyAccountId), "The LoyaltyAccountId must be provided.");
            
            if (string.IsNullOrEmpty(pointEventId) || string.IsNullOrEmpty(pointEventType))
                throw new ArgumentNullException(nameof(pointEventType), "The Event Type and Id must be provided.");

            try
            {
                var res = await _loyaltyAccountPointsDetailsAdapter.FetchPointsDetailsAsync(tenantId, loyaltyAccountId, pointEventType.ToLower(), pointEventId.ToLower());
                if (res != null)
                {
                    return new PointsDetailsResponse
                    {
                        Id = res.Id,
                        LoyaltyAccountId = res.LoyaltyAccountId,
                        EventId = res.EventId.ToLower(),
                        EventType = res.EventType.ToLower(),
                        Name = res.Name,
                        Description = res.Description,
                        Quantity = res.Quantity
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return null;

        }

        public async Task<List<PointsDetailsResponse>> GetManyPointsDetails(string tenantId, GetManyPointsDetailsRequest request, bool capitalizeType = false)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (string.IsNullOrWhiteSpace(request.LoyaltyAccountId))
                throw new ArgumentNullException(nameof(request.LoyaltyAccountId), "The LoyaltyAccountId must be provided.");

            if (!request.Events?.Any() ?? true)
                throw new ArgumentNullException(nameof(request.Events), "The events must be provided.");

            try
            {
                var eventTuples = request.Events.Select(e => (e.EventType.ToLower(), e.EventId.ToLower())).ToList();
                var res = await _loyaltyAccountPointsDetailsAdapter.GetManyPointsDetailsAsync(tenantId, request.LoyaltyAccountId, eventTuples);
                if (res?.Any() ?? false)
                {
                    return res.Select(res => new PointsDetailsResponse
                    {
                        Id = res.Id,
                        LoyaltyAccountId = res.LoyaltyAccountId,
                        EventId = res.EventId.ToLower(),
                        EventType = (capitalizeType) ? char.ToUpper(res.EventType[0]) + res.EventType.Substring(1) : res.EventType.ToLower(),
                        Name = res.Name,
                        Description = res.Description,
                        Quantity = res.Quantity
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return null;

        }

        public async Task<PagedResultSetResponse<PointsDetailsDto>> GetAllPointsDetails(string tenantId, string loyaltyAccountId, int pageSize, string continuationToken = null)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (string.IsNullOrWhiteSpace(loyaltyAccountId))
                throw new ArgumentNullException(nameof(loyaltyAccountId), "The LoyaltyAccountId must be provided.");

            try
            {
                var res = await _loyaltyAccountPointsDetailsAdapter.GetAllAccountPointsDetailsAsync(tenantId, loyaltyAccountId, pageSize, continuationToken);
                if (res?.Count > 0)
                {
                    return new PagedResultSetResponse<PointsDetailsDto>
                    {
                        Count = res.Count,
                        ContinuationToken = res.ContinuationToken,
                        Entities = res.Entities.Select(x => x.ToDto()).ToList()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return null;

        }


        #endregion

        #region Loyalty Account state

        //public async Task<LoyaltyAccountRuleState> GetLoyaltyAccountRuleState(string tenantId, string loyaltyAccountId)
        //{
        //    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(loyaltyAccountId))
        //        throw new Exception("Invalid request");

        //    try
        //    {
        //        var res = await _loyaltyAccountRuleStateAdapter.LoadEnqueuedStatesAsync(tenantId, loyaltyAccountId);
        //        return res?.FirstOrDefault(); /// should there be more than one document per account?
        //    }
        //    catch(Exception ex)
        //    {
        //        _logger.LogError(ex);
        //        throw;
        //    }
        //}

        //public async Task<LoyaltyAccountRuleState> UpsertLoyaltyAccountRuleState(string tenantId, LoyaltyAccountRuleState ruleState)
        //{
        //    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(ruleState?.LoyaltyAccountId))
        //        throw new Exception("Invalid request");

        //    try
        //    {
        //        var res = await _loyaltyAccountRuleStateAdapter.SaveEnqueuedStatesAsync(tenantId, ruleState.LoyaltyAccountId, new List<LoyaltyAccountRuleState> { ruleState });
        //        return res?.FirstOrDefault(); /// should there be more than one document per account?
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex);
        //        throw;
        //    }
        //}

        #endregion

        #region Tags

        public async Task<PagedResultSet<TagDto>> GetLoyaltyAccountTagsAsync(string tenantId, string loyaltyAccountId, string type, int pageSize, string? continuationToken = null)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) 
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (string.IsNullOrWhiteSpace(loyaltyAccountId)) 
                throw new ArgumentNullException(nameof(loyaltyAccountId), "The LoyaltyAccountId must be provided.");
            if (string.IsNullOrWhiteSpace(type)) 
                throw new ArgumentNullException(nameof(type), "The Tag Type must be provided.");
            if (pageSize <= 0)
                throw new ArgumentException("Page size must be greater than zero.", nameof(pageSize));

            PagedResultSet<TagDto> tags = new PagedResultSet<TagDto>();
            try
            {
                var res = await _tagAdapter.FetchEntityTagsAsync(tenantId, loyaltyAccountId, type, pageSize, continuationToken);
                tags.Count = res.Count;
                tags.ContinuationToken = res.ContinuationToken;
                tags.Entities = res.Entities.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return tags;
        }

        public async Task<TagDto> TagLoyaltyAccountAsync(string tenantId, TagDto tag)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) 
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");
            if (tag == null)
                throw new ArgumentNullException(nameof(tag), "The Tag must be provided.");
            if (string.IsNullOrEmpty(tag.EntityId))
                throw new ArgumentNullException(nameof(tag), "The Tag EntityId must be provided.");
            if (string.IsNullOrEmpty(tag.Value))
                throw new ArgumentNullException(nameof(tag), "The Tag Value must be provided.");

            try
            {
                var res = await _tagAdapter.UpsertTagAsync(tenantId, tag.FromDto());
                tag = res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return tag;
        }

        #endregion

        #region Stats
        //public async Task<UserStatsDto> GetUserStatsAsync(string tenantId, string loyaltyAccountId)
        //{
        //    return null;
        //}

        #endregion

        public async static Task<List<PointAccountType>> GetPointAccountTypes(string tenantId)
        {
            return await PointAccountTypeCache.Instance.GetAllPointAccountTypes(tenantId) ?? new List<PointAccountType>();
        }

        private async Task<List<PointLedger>> ExpirePoints(string tenantId, LoyaltyAccount loyaltyAccount, List<string> acctTypeIds,
            decimal amount, bool isPercent, Func<LedgerEntry, bool> predicate)
        {
            if (loyaltyAccount == null)
                throw new Exception($"No loyaltyaccount provided.");

            var storedLedgers = loyaltyAccount.PointLedgers;
            if (storedLedgers == null)
            {
                // Fetch account points
                var lst = await _loyaltyAccountPointLedgerAdapter.FetchLoyaltyAccountLedgersAsync(tenantId, loyaltyAccount.Id, 100);
                if (!string.IsNullOrEmpty(lst?.ContinuationToken)) throw new Exception($"Maximum number of Point Ledgers has been exceeded for account: {loyaltyAccount.Id}");
                storedLedgers = lst.Entities ?? new List<PointLedger>();
            }

            if (!storedLedgers?.Any() ?? true)
            {
                return null;
            }

            // First pass: Check if there are any expirations without acquiring locks
            var ledgersWithExpirations = new List<(PointLedger ledger, List<LedgerEntry> expSet)>();
            foreach (var atid in acctTypeIds)
            {
                var ledger = storedLedgers.FirstOrDefault(x => x.PointAccountTypeId.Equals(atid));
                if (ledger == null)
                {
                    continue;
                }

                // Find null expiration date entries and set them (read-only operation)
                var lstentries = ledger.Entries?.SelectMany(x => x.Value.Where(y => y.ExpirationDate.IsNullOrMinDate()).ToList()) ?? new List<LedgerEntry>();
                if (lstentries.Any()) 
                {
                    var thisAcctType = await _cache.GetPointAccountTypeAsync(ledger.TenantId, atid);
                    if (string.IsNullOrEmpty(thisAcctType?.ExpiresToPointAccountTypeId))
                    {
                        _logger.LogError($"ExpirePoints: ledger id: {ledger.Id} - No point account type found for {ledger.PointAccountTypeId} or no expiration date set.");
                    }

                    // Note: We're modifying entries here, but this is just setting expiration dates
                    // which is needed to properly evaluate the predicate. This is a read-only check phase.
                    foreach (var entry in lstentries)
                    {
                        entry.ExpirationDate = (!thisAcctType.PointsLifespanEndDate.IsNullOrMinDate()) ? thisAcctType.PointsLifespanEndDate : null;
                        if (!entry.ExpirationDate.IsNullOrMinDate()) continue;
                        
                        double days = Decimal.ToDouble(thisAcctType.PointsLifespanDays ?? 0);
                        entry.ExpirationDate ??= DateTimeOffset.UtcNow.AddDays(days);
                    }
                }

                // Apply the predicate to filter LedgerEntries
                var expSet = ledger.Entries?
                    .SelectMany(d => d.Value)?
                    .Where(predicate)?
                    .ToList();

                if (expSet?.Any() ?? false)
                {
                    ledgersWithExpirations.Add((ledger, expSet));
                }
            }

            // If no expirations found, return early without acquiring locks
            if (!ledgersWithExpirations.Any())
            {
                return storedLedgers;
            }

            // Get or create account-specific semaphore for this account
            var accountSemaphore = _accountSemaphores.GetOrAdd(loyaltyAccount.Id, _ => new SemaphoreSlim(1, 1));
            bool isLocked = false;
            bool reusedCallerLease = !string.IsNullOrEmpty(loyaltyAccount.LockLeaseKey);
            
            try
            {
                // Acquire account-specific semaphore before processing
                await accountSemaphore.WaitAsync();
                isLocked = true;

                // Try to acquire database-level lock with retries
                var numretries = 3;
                while (numretries > 0)
                {
                    try
                    {
                        // Reuse the caller lease when ExpireBy* / ProcessEvent already locked this account; a new key loses the outer lock.
                        var expireLockKey = reusedCallerLease
                            ? loyaltyAccount.LockLeaseKey
                            : Guid.NewGuid().ToString();
                        var accountId = loyaltyAccount.Id;
                        loyaltyAccount = await TryLockAccount(tenantId, loyaltyAccount, expireLockKey, 500);
                        if (loyaltyAccount == null) throw new Exception($"Concurrency failure trying to lock account: {accountId}");
                        break;
                    }
                    catch (LockAcquisitionException laex)
                    {
                        _logger.LogError(laex);
                        --numretries;
                    }
                    catch (ConcurrencyException cex)
                    {
                        _logger.LogError(cex);
                        --numretries;
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
                if (numretries <= 0) throw new Exception($"Concurrency failure while expiring points for account: {loyaltyAccount.Id}");

                // Second pass: Process the expirations now that we have locks
                // Use retry logic with ETag refresh for optimistic concurrency across container nodes
                const int maxETagRetries = 3;
                int etagRetryCount = 0;
                bool success = false;

                while (!success && etagRetryCount < maxETagRetries)
                {
                    try
                    {
                        // Re-fetch ledgers with fresh ETags if this is a retry
                        if (etagRetryCount > 0)
                        {
                            _logger.LogInformation($"ETag retry attempt {etagRetryCount} for account {loyaltyAccount.Id} - re-fetching ledgers with fresh ETags");
                            var freshLst = await _loyaltyAccountPointLedgerAdapter.FetchLoyaltyAccountLedgersAsync(tenantId, loyaltyAccount.Id, 100);
                            if (!string.IsNullOrEmpty(freshLst?.ContinuationToken)) 
                                throw new Exception($"Maximum number of Point Ledgers has been exceeded for account: {loyaltyAccount.Id}");
                            storedLedgers = freshLst.Entities ?? new List<PointLedger>();

                            // Re-check for expirations with fresh data
                            ledgersWithExpirations.Clear();
                            foreach (var atid in acctTypeIds)
                            {
                                var ledger = storedLedgers.FirstOrDefault(x => x.PointAccountTypeId.Equals(atid));
                                if (ledger == null)
                                {
                                    continue;
                                }

                                // Find null expiration date entries and set them
                                var lstentries = ledger.Entries?.SelectMany(x => x.Value.Where(y => y.ExpirationDate.IsNullOrMinDate()).ToList()) ?? new List<LedgerEntry>();
                                if (lstentries.Any()) 
                                {
                                    var thisAcctType = await _cache.GetPointAccountTypeAsync(ledger.TenantId, atid);
                                    if (string.IsNullOrEmpty(thisAcctType?.ExpiresToPointAccountTypeId))
                                    {
                                        _logger.LogError($"ExpirePoints: ledger id: {ledger.Id} - No point account type found for {ledger.PointAccountTypeId} or no expiration date set.");
                                    }

                                    foreach (var entry in lstentries)
                                    {
                                        entry.ExpirationDate = (!thisAcctType.PointsLifespanEndDate.IsNullOrMinDate()) ? thisAcctType.PointsLifespanEndDate : null;
                                        if (!entry.ExpirationDate.IsNullOrMinDate()) continue;
                                        
                                        double days = Decimal.ToDouble(thisAcctType.PointsLifespanDays ?? 0);
                                        entry.ExpirationDate ??= DateTimeOffset.UtcNow.AddDays(days);
                                    }
                                }

                                // Apply the predicate to filter LedgerEntries
                                var expSet = ledger.Entries?
                                    .SelectMany(d => d.Value)?
                                    .Where(predicate)?
                                    .ToList();

                                if (expSet?.Any() ?? false)
                                {
                                    ledgersWithExpirations.Add((ledger, expSet));
                                }
                            }

                            // If no expirations found after re-fetch, exit successfully
                            if (!ledgersWithExpirations.Any())
                            {
                                _logger.LogInformation($"No expirations found after re-fetch for account {loyaltyAccount.Id} - another process may have already processed them");
                                return storedLedgers;
                            }
                        }

                        // Process the expirations
                        bool hadExpirations = false;
                        foreach (var (ledger, expSet) in ledgersWithExpirations)
                        {
                            await SaveLedgerExpirations(storedLedgers, ledger, expSet, amount, isPercent);
                            hadExpirations = true;
                        }
                        
                        if (hadExpirations)
                        {
                            loyaltyAccount.ResettleASAP = true;
                        }
                        
                        success = true;
                        return storedLedgers;
                    }
                    catch (Exception ex) when (IsETagConcurrencyError(ex))
                    {
                        etagRetryCount++;
                        _logger.LogWarning(ex, $"ETag concurrency conflict on attempt {etagRetryCount} for account {loyaltyAccount.Id}. Retrying with fresh data...");
                        
                        if (etagRetryCount >= maxETagRetries)
                        {
                            _logger.LogError($"Max ETag retries ({maxETagRetries}) exceeded for account {loyaltyAccount.Id}");
                            throw new Exception($"ETag concurrency failure after {maxETagRetries} retries while expiring points for account: {loyaltyAccount.Id}", ex);
                        }

                        // Small delay before retry to allow other operations to complete
                        await Task.Delay(50 * etagRetryCount); // Exponential backoff: 50ms, 100ms, 150ms
                    }
                }

                // Should never reach here, but just in case
                throw new Exception($"Unexpected state in ExpirePoints retry logic for account: {loyaltyAccount.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            finally
            {
                if (isLocked)
                {
                    // Only release a lease this method acquired. A reused caller key stays held for ProcessEvent / ExpireBy*.
                    if (loyaltyAccount != null && !reusedCallerLease)
                    {
                        loyaltyAccount.LockLeaseExpiration = null;
                        loyaltyAccount.LockLeaseKey = null;
                        await _loyaltyAccountAdapter.UpsertLoyaltyAccountAsync(tenantId, loyaltyAccount);
                    }
                    // Release account-specific semaphore
                    accountSemaphore.Release();
                }
            }
        }

        private async Task SaveLedgerExpirations(List<PointLedger> storedLedgers, PointLedger ledger, List<LedgerEntry> expSet, decimal amount, bool isPercent)
        {
            PointLedger rollbackLedger = null;
            if (ledger == null || (!expSet?.Any() ?? true))
            {
                return;
            }
            string loyaltyAccountId = ledger.AccountId;
            string tenantId = ledger.TenantId;
            
            //Get point account type config for the ledger and obtain's it's expires to ledger (account type)
            ledger.PointAccountType ??= await _cache.GetPointAccountTypeAsync(ledger.TenantId, ledger.PointAccountTypeId);
            if (string.IsNullOrEmpty(ledger.PointAccountType?.ExpiresToPointAccountTypeId))
            {
                _logger.LogError($"SaveLedgerExpirations: ledger id: {ledger.Id} - No point account type found for {ledger.PointAccountTypeId} or no expiration date set.");
                return; //TODO: Follow up:: Eventual concern is that this masks config issues resulting in points that don't expire when they should
            }
            var expAcctType = await _cache.GetPointAccountTypeAsync(ledger.TenantId, ledger.PointAccountType.ExpiresToPointAccountTypeId);
            if (expAcctType == null)
            {
                _logger.LogError($"SaveLedgerExpirations: ledger id: {ledger.Id} - Destination point account type {ledger.PointAccountType.ExpiresToPointAccountTypeId} was not found for tenant {ledger.TenantId}.");
                return;
            }

            var expLedger = storedLedgers.FirstOrDefault(x =>
                (x.PointAccountType?.Id ?? x.PointAccountTypeId).Equals(ledger.PointAccountType?.ExpiresToPointAccountTypeId));
            expLedger ??= new PointLedger(ledger.AccountId, expAcctType.Id, 0, 0, 1, new Dictionary<string, List<LedgerEntry>>(), ledger.TenantId, Guid.NewGuid().ToString());
            if (expLedger == null) throw new Exception("Unable to resolve point expiration account type.");
            expLedger.PointAccountType ??= expAcctType;
            rollbackLedger = expLedger.Clone();

            // Add/update expiration ledger
            var deps = expSet.Sum(x => (x.PointsDeposited ?? 0) * (isPercent && amount > 0 && amount <= 1 ? amount : 1));
            var adj = deps; // (!isPercent && amount > 0 ? amount : deps) - expSet.Sum(xentry => xentry.PointsWithdrawn ?? 0);

            expLedger.CurrentBalance += adj;
            expLedger.LifetimeTotal += adj;
            
            expLedger.Entries ??= new Dictionary<string, List<LedgerEntry>>();

            // Remove expired entries from the source ledger
            ledger.CurrentBalance -= adj;
            
            //Remove expiring entries
            expSet.ForEach(xentry =>
            {
                var hopEarnDate = xentry.EarnDate;
                var hopExistingExpiration = xentry.ExpirationDate;

                //Deduct points (expiring)
                xentry.SpendablePoints ??= 0;
                xentry.SpendablePoints -= (xentry.SpendablePoints ?? 0) * (isPercent && amount > 0 && amount <= 1 ? amount : 1);

                //Ensure rolling expiration is maintained if not by PointsLifespanEndDate
                xentry.ExpirationDate ??= DateTimeOffset.UtcNow;
                if (expLedger.PointAccountType.PointsLifespanEndDate.IsNullOrMinDate())
                    xentry.ExpirationDate = xentry.ExpirationDate.Value.AddDays(Decimal.ToDouble(expLedger.PointAccountType.PointsLifespanDays ?? 0));
                else
                    xentry.ExpirationDate = expLedger.PointAccountType.PointsLifespanEndDate;

                if (!ledger.PointAccountType.PointsLifespanEndDate.IsNullOrMinDate() || xentry.SpendablePoints <= 0)
                {
                    //if these are not rolling points (set point lifespan end date) OR no spendable points left
                    // remove them from the earning ledger and add to the expiration (destination) ledger
                    var eventKey = EventKeyUtility.ToEventKey(xentry.EventType, xentry.EventId);

                    if (!ledger.Entries.ContainsKey(eventKey))
                        throw new Exception($"SaveLedgerExpirations: missing ledger entry for key: {eventKey}");

                    ledger.Entries[eventKey].Remove(xentry);
                    if (ledger.Entries[eventKey]?.Count == 0)
                    {
                        //remove entry completely
                        ledger.Entries.Remove(eventKey);
                    }

                    // Dest clock: end-date, else EarnDate + dest days, else last-resort existing expiration under dest days, else unset.
                    xentry.ExpirationDate = ComputeDestExpirationOnHop(hopEarnDate, hopExistingExpiration, expAcctType);

                    if (!expLedger.Entries.ContainsKey(eventKey))
                        expLedger.Entries.Add(eventKey, new List<LedgerEntry> { xentry });
                    else
                        expLedger.Entries[eventKey].Add(xentry);
                }
            });

            // Save both ledgers with ETag-based optimistic concurrency
            // The ETags on the ledger objects will be used by the adapter for optimistic concurrency control
            PointLedger savedExpLedger = null;
            try
            {
                // Save expiration ledger first
                savedExpLedger = await _loyaltyAccountPointLedgerAdapter.UpsertLoyaltyAccountLedgerAsync(tenantId, expLedger);
                if (savedExpLedger == null)
                {
                    throw new Exception($"Failed to save expiration ledger for account {expLedger.AccountId}, ledger {expLedger.Id}");
                }

                // Update the expiration ledger's ETag from the response for potential rollback
                if (savedExpLedger.ETag != null)
                {
                    expLedger.ETag = savedExpLedger.ETag;
                }

                // Save source ledger (this may throw an exception if ETag conflicts)
                var savedSourceLedger = await _loyaltyAccountPointLedgerAdapter.UpsertLoyaltyAccountLedgerAsync(tenantId, ledger);
                if (savedSourceLedger == null)
                {
                    throw new Exception($"Failed to save source ledger for account {ledger.AccountId}, ledger {ledger.Id}");
                }

                // Update the source ledger's ETag from the response
                if (savedSourceLedger.ETag != null)
                {
                    ledger.ETag = savedSourceLedger.ETag;
                }

                return;
            }
            catch (Exception ex) when (IsETagConcurrencyError(ex))
            {
                // ETag conflict - let it propagate up to trigger retry logic in ExpirePoints
                // Don't attempt rollback here as the retry will re-fetch fresh data
                throw;
            }
            catch (Exception ex)
            {
                // For other exceptions, attempt rollback of expiration ledger if it was saved
                if (savedExpLedger != null && rollbackLedger != null)
                {
                    _logger.LogWarning(ex, $"Attempting to rollback expiration ledger for account {expLedger.AccountId}, ledger {expLedger.Id}");
                    try
                    {
                        // Preserve the ETag from the saved expiration ledger for rollback
                        rollbackLedger.ETag = expLedger.ETag;
                        var rollbackRes = await _loyaltyAccountPointLedgerAdapter.UpsertLoyaltyAccountLedgerAsync(tenantId, rollbackLedger);
                        if (rollbackRes == null)
                        {
                            _logger.LogError($"Failed to rollback expiration ledger for account {expLedger.AccountId}, ledger {expLedger.Id}");
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, $"Exception during rollback of expiration ledger for account {expLedger.AccountId}, ledger {expLedger.Id}");
                        throw new Exception($"An exception occurred and rollback failed. LoyaltyAccount Id: {expLedger.AccountId}, Ledger Id: {expLedger.Id}", ex);
                    }
                }
                throw;
            }
        }

        private static DateTimeOffset? ComputeDestExpirationOnHop(DateTimeOffset? earnDate, DateTimeOffset? existingExpiration, PointAccountType dest)
        {
            if (dest == null)
                return existingExpiration;

            if (!dest.PointsLifespanEndDate.IsNullOrMinDate())
                return dest.PointsLifespanEndDate;

            if (dest.PointsLifespanDays.HasValue)
            {
                if (!earnDate.IsNullOrMinDate())
                    return earnDate.Value.AddDays(Decimal.ToDouble(dest.PointsLifespanDays.Value));
                if (!existingExpiration.IsNullOrMinDate())
                    return existingExpiration;
                return null;
            }

            return null;
        }

        private void ValidateParametersDate(string tenantId, string loyaltyAccountId, DateTimeOffset? date)
        {
            if (string.IsNullOrEmpty(tenantId))
                throw new Exception("The TenantId must be provided.");

            if (string.IsNullOrEmpty(loyaltyAccountId))
                throw new Exception("The account id must be provided.");

            if (date != null && date == DateTimeOffset.MinValue)
                throw new Exception("A valid date must be provided.");

        }

        private void ValidateParametersAmount(decimal amount, bool isPercent = false)
        {
            if (amount < 0 || amount > 100000 || isPercent && amount > 1)
                throw new Exception("The amount provided is not valid, it must be non-negative and < 100000, or < 100 if a percent.");

        }

        /// <summary>
        /// Determines if an exception is related to ETag/concurrency conflicts.
        /// Checks exception message for common concurrency error indicators.
        /// </summary>
        private static bool IsETagConcurrencyError(Exception ex)
        {
            if (ex == null) return false;
            
            var message = ex.Message ?? string.Empty;
            return message.Contains("Concurrency error", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("PreconditionFailed", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("(412)", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("412", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("ConditionNotMet", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("conditional header", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<RuleBase> GetAccountValidationRules()
        {
            return new SimpleRule<bool>
            {
                LeftProvider = new PathValueProvider("event.isdisabled"),
                RightProvider = new ConstantValueProvider(false),
                Evaluator = new NumericEvaluation(NumEvalType.Equal)
            };

        }

        private async Task<bool> RemoveEntityWithRetryAsync(string tenantId, LoyaltyAccount loyaltyAccount, WrappedEventPayload entity, string modelId, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await _dynamicDataAdapter.RemoveEntityAsync(tenantId, entity.Id, modelId, new Dictionary<string, string>
                    {
                        { "TenantId", tenantId },
                        { "accountid", loyaltyAccount.Id }
                    });
                    return result;
                }
                catch (APIErrorsException apiEx)
                {
                    // Don't retry on API errors (like 404, 400, etc.)
                    _logger.LogError(apiEx, "API error removing entity. TenantId: {TenantId}, ModelId: {ModelId}, EntityId: {EntityId}",
                        tenantId, modelId, entity?.Id);
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "Failed to remove entity after {MaxRetries} attempts. TenantId: {TenantId}, ModelId: {ModelId}, EntityId: {EntityId}",
                            maxRetries, tenantId, modelId, entity?.Id);
                        throw;
                    }

                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100);
                    _logger.LogWarning("Remove entity attempt {Attempt} failed, retrying in {Delay}ms. Error: {Error}",
                        attempt, delay.TotalMilliseconds, ex.Message);

                    await Task.Delay(delay);
                }
            }

            return false;
        }
    }
}
