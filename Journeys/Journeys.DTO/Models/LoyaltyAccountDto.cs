using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class LoyaltyAccountDto : DtoModelBase
    {
        public string? ExtAccountId { get; set; }
        public string? Type { get; set; }

        public bool ResettleASAP { get; set; }

        public List<string>? KnownExternalIds { get; set; }

        public List<TagDto>? Tags { get; set; }

        public List<LoyaltyAccountJourneyDto>? Journeys { get; set; }

        [JsonIgnore]
        public List<PointLedgerDto>? PointLedgers { get; set; }

        /// <summary>Historical rule aggregate state. Key = campaignId|ruleId.</summary>
        public Dictionary<string, HistoricalRuleStateDto>? RuleState { get; set; }
    }

    public class HistoricalRuleStateDto
    {
        public int Count { get; set; }
        public decimal Value { get; set; }
        public DateTimeOffset? FirstOccurrence { get; set; }
    }

    public class LoyaltyAccountJourneyDto
    {
        public string RootJourneyNodeId { get; set; }
        public List<string> JourneyNodeIds { get; set; }

    }
}
