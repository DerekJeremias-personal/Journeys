using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.RulesEngine
{
    public class EventPayloadResponseDto : ResponseBase
    {
        public string ProcessedEventModelId { get; set; }
        public string EventNaturalKey { get; set; }
        public string LoyaltyAccountId { get; set; }
        public JsonElement Event { get; set; }
        public string? SpendablePointsAwarded
        {
            get
            {
                OutcomeStates ??= new List<OutcomeStateBaseDto>();
                if (OutcomeStates.Count > 0)
                {
                    StringBuilder builder = null;
                    var outcomes = OutcomeStates
                                    .Where(x => x.IsAwarded || (x.IssuingOutcomeId?.Contains("-Awarded") ?? false))
                                    .ToList();

                    foreach (var outcome in outcomes)
                    {
                        if (outcome.IssuingEventType == null || outcome.IssuingEventId == null)
                            continue;
                        if (builder == null)
                            builder = new StringBuilder($"{outcome.IssuingEventType.ToLowerInvariant()}|{outcome.IssuingEventId.ToLowerInvariant()}::Deposits:");

                        var patId = outcome.PointAccountTypeId ??
                                        ((outcome.IssuingOutcomeId?.Contains("|") ?? false) ?
                                            outcome.IssuingOutcomeId.Split('|')[1] : "spendable");
                        builder.Append($" {patId} : {outcome.PointsDeposited};");
                    }
                    return builder?.ToString() ?? null;
                }
                return null;
            }
        }

        public DateTimeOffset TimeOfOccurrence { get; set; }
        public DateTimeOffset LastProcessed { get; set; } = DateTimeOffset.UtcNow;


        public List<string>? AppliedCampaigns { get; set; }
        public List<string>? AppliedRuleSetIds { get; set; }

        public Dictionary<string, ProviderStateBaseDto?>? ProviderStates { get; set; }
        public List<OutcomeStateBaseDto>? OutcomeStates { get; set; }
        public Dictionary<string, JourneyStateDto>? JourneyStates { get; set; }
        public new Dictionary<string, string>? Errors { get; set; }
    }
}
