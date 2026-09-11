
using Backend.Dto.Structures.Model;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class LoyaltyAccountAdapter : ReferenceableBaseAdapter<LoyaltyAccount>, ILoyaltyAccountAdapter
    {
        private const string LOYALTY_ACCOUNT_MODEL_ID = "e2cc2404-c60b-4cbc-9f50-6db7ee58e01d";
        private readonly ModelCache _modelCache;
        public LoyaltyAccountAdapter(IDynamicDataAdapter dynAdapter, IExternalReferenceAdapter extReferenceAdapter, ILogger<LoyaltyAccountAdapter> logger, ModelCache modelCache) 
            : base(dynAdapter, extReferenceAdapter, logger)
        {
            _modelCache = modelCache;
        }

        public async Task<LoyaltyAccount> FetchLoyaltyAccountAsync(string tenantId, string accountId, string status)
        {
            return await base.FetchEntityAsync(tenantId, accountId, LOYALTY_ACCOUNT_MODEL_ID, status);
        }

        public async Task<ExternalReference> FetchAccountExternalReferenceAsync(string tenantId, string extId, string type)
        {
            return await base.GetExternalReference(tenantId, LOYALTY_ACCOUNT_MODEL_ID, extId, type);
        }

        public async Task<LoyaltyAccount> FetchLoyaltyAccountByExtIdAsync(string tenantId, ExternalReference extRef, string status)
        {
            return await base.GetEntityByExtId(tenantId, LOYALTY_ACCOUNT_MODEL_ID, extRef, status);
        }

        public async Task<LoyaltyAccount> FetchLoyaltyAccountByExtIdAsync(string tenantId, string extId, string type, string status)
        {
            return await base.GetEntityByExtId(tenantId, LOYALTY_ACCOUNT_MODEL_ID, extId, type, status);
        }

        public async Task<ExternalReference> FetchAliasAsync(string tenantId, string extId, string type)
        {
            return await base.GetExternalReference(tenantId, LOYALTY_ACCOUNT_MODEL_ID, extId, type);
        }

        public async Task<List<LoyaltyAccount>> GetLoyaltyAccountsAsync(string tenantId, List<string> filters)
        {
            return null;
                //await base.GetEntitiesByFiltersAsync(tenantId, LOYALTY_ACCOUNT_MODEL_ID, filters);
        }

        public async Task<List<string>> GetRelatedLoyaltyAccountModelIDsAsync(string tenantId)
        {
            var fullset = await GetRelatedLoyaltyAccountModelsAsync(tenantId) ?? new List<ModelDto>();
            return fullset.Select(x => x.ID).ToList();
        }

        public async Task<List<ModelDto>> GetRelatedLoyaltyAccountModelsAsync(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _modelCache.GetRelatedLoyaltyAccountModelsAsync(tenantId);
        }


        public async Task<LoyaltyAccount> UpsertLoyaltyAccountAsync(string tenantId, LoyaltyAccount account)
        {
            return await base.UpsertEntityAsync(tenantId, LOYALTY_ACCOUNT_MODEL_ID, account, typeof(LoyaltyAccount));
        }
        public async Task<ExternalReference> UpsertExternalReferenceAsync(string tenantId, ExternalReference extRef)
        {
            extRef.ModelId = LOYALTY_ACCOUNT_MODEL_ID;
            return await base.AliasEntityAsync(tenantId, extRef);
        }


        public async Task<LoyaltyAccount> MoveLoyaltyAccountAsync(string tenantId, LoyaltyAccount account, Dictionary<string, string> newPartition)
        {
            return await base.MoveEntityAsync(tenantId, LOYALTY_ACCOUNT_MODEL_ID, account, newPartition, typeof(LoyaltyAccount));
        }

        public async Task DeleteLoyaltyAccountAsync(string tenantId, string loyaltyAccountId)
        {
            if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrEmpty(loyaltyAccountId))
            {
                //await base.DeleteEntityAsync(tenantId, LOYALTY_ACCOUNT_MODEL_ID, loyaltyAccountId);
                await base.DeleteEntityAsync(tenantId, LOYALTY_ACCOUNT_MODEL_ID, loyaltyAccountId, new Dictionary<string, string>
                {
                    { "TenantId", tenantId },
                    { "status", "active" }
                });
            }
        }
    }
}
