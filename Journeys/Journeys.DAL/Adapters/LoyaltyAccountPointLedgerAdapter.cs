using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Journeys.DTO.Responses;
using Journeys.DTO.Exceptions;

namespace Journeys.DAL.Adapters
{
    public class LoyaltyAccountPointLedgerAdapter : BaseAdapter<PointLedger>, ILoyaltyAccountPointLedgerAdapter
    {
        private const string LEDGER_MODEL_ID = "56ff2535-f1d9-4de7-a0db-67ed2087063a";

        public LoyaltyAccountPointLedgerAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter)
        {
        }

        public async Task<PagedResultSet<PointLedger>> FetchLoyaltyAccountLedgersAsync(string tenantId, string accountId, int pageSize, int? pageNumber = null, string? continuationToken = null)
        {
            var resset = await base.FetchEntityByKeyAsync(tenantId, accountId, LEDGER_MODEL_ID, pageSize, continuationToken);
            resset.Entities?.ForEach(x => x.ModelId = LEDGER_MODEL_ID);
            return new PagedResultSet<PointLedger>
            {
                
                ContinuationToken = resset.ContinuationToken,
                Count = resset.Count,
                Entities = (pageNumber != null) ?
                        new List<PointLedger> { resset.Entities.FirstOrDefault(t => t.PageNumber == pageNumber) } :
                        resset.Entities
            };
        }

        public async Task<List<PointLedger>> GetLoyaltyAccountLedgersAsync(string tenantId, List<string> filters)
        {
            return null;
                //await base.GetEntitiesByFiltersAsync(tenantId, LEDGER_MODEL_ID, filters).ConfigureAwait(false);
        }

        public async Task<PointLedger> UpsertLoyaltyAccountLedgerAsync(string tenantId, PointLedger ledger)
        {
            var storedledger = await base.UpsertEntityAsync(tenantId, LEDGER_MODEL_ID, ledger, typeof(PointLedger));
            storedledger.ModelId = LEDGER_MODEL_ID;
            return storedledger;
        }

        //public async Task DeleteLoyaltyAccountLedgerAsync(string tenantId, string loyaltyAccountLedgerId)
        //{
        //    await base.DeleteEntityAsync(tenantId, LEDGER_MODEL_ID, loyaltyAccountLedgerId);
        //}

        public async Task DeleteLoyaltyAccountLedgerAsync(string tenantId, string loyaltyAccountId, string id)
        {
            await base.DeleteEntityAsync(tenantId, LEDGER_MODEL_ID, id, new Dictionary<string, string>
            {
                { "TenantId", tenantId },
                { "accountid", loyaltyAccountId }
            });
        }
    }
}
