using Journeys.Core.Caching;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Outcomes
{
    public abstract class PointOutcomeBase : OutcomeBase
    {
        public List<string> AffectedPointAccountTypeIds { get; set; }

        [JsonIgnore]
        public List<PointAccountType>? AffectedPointAccountTypes { get; set; }

        [JsonIgnore]
        public List<PointLedgerDto> PointResults { get; set; } = new List<PointLedgerDto>();

        protected virtual async Task EnsurePointAccounType(string tenantId)
        {
            AffectedPointAccountTypeIds ??= new List<string>();
            AffectedPointAccountTypes ??= new List<PointAccountType>();
            
            foreach(var patId in AffectedPointAccountTypeIds)
            {
                if (!AffectedPointAccountTypes.Any(x => x.Id.Equals(patId)))
                {
                    var pat = await PointAccountTypeCache.Instance.GetPointAccountTypeAsync(tenantId, patId);
                    if (pat != null) 
                        AffectedPointAccountTypes.Add(pat);
                    else
                        throw new Exception($"Unable to resolve the Point Account Type from provided id: {patId}");
                }
            }
        }

        public static decimal RoundToSignificantDigits(decimal value, decimal decimalPlaces, MidpointRounding mode)
        {
            int places = Convert.ToInt32(decimalPlaces);
            return Math.Round(value, places, mode);
        }
    }
}
