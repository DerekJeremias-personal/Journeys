using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class WrappedEventPayloadStatus
    {
        public const string ACTIVE = "Active";
        public const string STABLE = "Stable";
    }
    public class WrappedEventPayload
    {
        public WrappedEventPayload()
        {
            AppliedCampaigns = new List<string>();
            AppliedRuleSetIds = new List<string>();
            ProviderStates = new Dictionary<string, ProviderStateBaseDto>();
            OutcomeStates = new List<OutcomeStateBaseDto>();
            JourneyStates = new Dictionary<string, JourneyStateDto>();
        }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// <![CDATA[WrappedEventPayloadStatus]]> for legal values.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("naturalkey")]
        public string NaturalKey { get; set; }

        [JsonPropertyName("timeofoccurrence")]
        public DateTimeOffset TimeOfOccurrence { get; set; }

        [JsonPropertyName("lastprocessed")]
        public DateTimeOffset LastProcessed { get; set; } = DateTimeOffset.UtcNow;

        [JsonPropertyName("accountid")]
        public string AccountId { get; set; }
        [JsonPropertyName("db_status")]
        public string? DbStatus { get; set; }

        [JsonPropertyName("event")]
        public JsonElement Event { get; set; }

        [JsonPropertyName("appliedcampaigns")]
        public List<string>? AppliedCampaigns { get; set; }

        [JsonPropertyName("appliedrulesetids")]
        public List<string>? AppliedRuleSetIds { get; set; }

        [JsonPropertyName("providerstates")]
        public Dictionary<string, ProviderStateBaseDto> ProviderStates { get; set; }

        [JsonPropertyName("outcomestates")]
        public List<OutcomeStateBaseDto>? OutcomeStates { get; set; }

        [JsonPropertyName("journeystates")]
        public Dictionary<string, JourneyStateDto> JourneyStates { get; set; }
        [JsonIgnore()]
        public string MetaType { get; set; }

        /// <summary>
        /// Drop empty lists so Backend does not bind JSON arrays onto Object-typed wrapper attributes.
        /// Empty dictionaries stay (they serialize as objects).
        /// </summary>
        public void OmitEmptyCollections()
        {
            if (AppliedCampaigns is { Count: 0 })
                AppliedCampaigns = null;
            if (AppliedRuleSetIds is { Count: 0 })
                AppliedRuleSetIds = null;
            if (OutcomeStates is { Count: 0 })
                OutcomeStates = null;
        }
        
    }
}
