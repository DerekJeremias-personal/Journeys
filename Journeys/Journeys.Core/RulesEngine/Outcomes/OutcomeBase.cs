using Journeys.Core.Interfaces.Services;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Outcomes
{
    [JsonConverter(typeof(OutcomeBaseJsonConverter))]
    public abstract class OutcomeBase
    {
        public string Id { get; set; }

        public abstract string Kind { get; }
        public string EventId { get; set; }
        public string EventType { get; set; }

        public string? PointAccountTypeId { get; set; }

        public IValueProvider? EventIdProvider { get; set; }

        public IValueProvider? EventTypeProvider { get; set; } = new PathValueProvider($"event.{EventService.EVENT_TYPE_SYMBOL_KEY}");

        public abstract Task<OutcomeResult> CalculateOutcomeAsync(RulesEngineState state, CancellationToken token);

        public abstract Task<OutcomeResult> AwardOutcomeAsync(RulesEngineState state, ILoyaltyAccountService loyaltyAccountService, CancellationToken token);

        protected async Task<string?> ResolveEventIdAsync(RulesEngineState state, CancellationToken token)
        {
            if (EventIdProvider != null)
                return await EventIdProvider.GetValue<string>(state, token);
            return state?.EventId;
        }

        protected async Task<string?> ResolveEventTypeAsync(RulesEngineState state, CancellationToken token)
        {
            if (EventTypeProvider != null)
                return await EventTypeProvider.GetValue<string>(state, token);
            return state?.EventType;
        }

    }

    public class OutcomeResult
    {
        [JsonIgnore]
        public OutcomeBase IssuingOutcome { get; set; }
        [JsonIgnore]
        public string IssuingJourneyNodeId { get; set; }

        public string IssuingOutcomeId { get; set; }
        public string IssuingOutcomeKind { get; set; }
        public string IssuingEventId { get; set; }
        public string IssuingEventType { get; set; }

        public decimal? PointsAwarded { get; set; }
        public decimal? PointsRevoked { get; set; }
        public string? PointAccountTypeId { get; set; }
        
        public string? CampaignId { get; set; }
        public string? RuleSetId { get; set; }
        public bool IsAwarded { get; set; }
    }
}
