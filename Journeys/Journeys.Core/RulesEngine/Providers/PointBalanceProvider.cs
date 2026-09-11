using Journeys.Core.Extensions;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Providers
{
    public class PointBalanceProvider : ProviderBase, IValueProvider
    {
        public override string Kind => ProviderKindDiscriminators.PathValueProvider;

        public string PointAccountTypeId { get; set; }

        public PointBalanceProvider(string pointAccountTypeId)
        {
            PointAccountTypeId = pointAccountTypeId;
        }

        public virtual async Task<T?> GetValue<T>(RulesEngineState entity, CancellationToken token)
        {
            if (entity.LoyaltyAccount == null || entity.LoyaltyAccountService == null) 
                throw new Exception("PointBalanceProvider: Null LoyaltyAccount or LoyaltyAccountService");

            var ledgers = entity.LoyaltyAccount.PointLedgers;
            if (ledgers == null)
            {
                var dtoledgers = await entity.LoyaltyAccountService.GetLoyaltyAccountPointsAsync(entity.TenantId, entity.LoyaltyAccount.Id);
                ledgers = dtoledgers?.Select(x => x.FromDto()).ToList();
                entity.LoyaltyAccount.PointLedgers = ledgers;
            }
            var ledger = ledgers?.FirstOrDefault(l => l.PointAccountTypeId.Equals(PointAccountTypeId));
            if (ledger != null && !string.IsNullOrWhiteSpace(entity.EventType) && !string.IsNullOrWhiteSpace(entity.EventId))
            {
                string thisEntryKey = EventKeyUtility.ToEventKey(entity.EventType, entity.EventId);
                var thisEntry = (ledger.Entries.ContainsKey(thisEntryKey)) ? ledger.Entries[thisEntryKey] : null;
                if (thisEntry?.Count > 0)
                {
                    //remove this amount from the balance as we are reprocessing the same event, so don't count it for tier qual
                    return (T?)(object)(ledger.CurrentBalance - thisEntry.Sum(x => (x.PointsDeposited ?? 0) - (x.PointsWithdrawn ?? 0)));
                }
            }
            return (T?)(object)(ledger?.CurrentBalance ?? 0);
        }
    }
}
