using System;

namespace Journeys.Core.Models
{
    /// <summary>
    /// Aggregated state for a historical rule stored on LoyaltyAccount. Keyed by CampaignId|RuleId.
    /// </summary>
    public class HistoricalRuleState
    {
        public int Count { get; set; }
        public decimal Value { get; set; }
        public DateTimeOffset? FirstOccurrence { get; set; }
    }
}
