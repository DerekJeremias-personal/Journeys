

using Journeys.Core.JsonConverters;
using Journeys.Core.RulesEngine.Engine;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Rules
{
    [JsonConverter(typeof(RuleBaseJsonConverter))]
    public abstract class RuleBase
    {
        /// <summary>
        /// Unique identifier for this rule instance. Used with CampaignId to form the state key (CampaignId|Id)
        /// so that when a campaign is modified we use a new state bucket for the same rule.
        /// </summary>
        public string Id { get; set; }

        public abstract string Kind { get; }

        public abstract Task<bool> Evaluate(RulesEngineState obj, CancellationToken token);

        /// <summary>
        /// Builds the state key for historical rule state and TimeToLive storage: CampaignId|RuleId.
        /// </summary>
        public static string GetHistoricalStateKey(string campaignId, string ruleId)
        {
            if (string.IsNullOrEmpty(campaignId)) throw new ArgumentNullException(nameof(campaignId));
            if (string.IsNullOrEmpty(ruleId)) throw new ArgumentNullException(nameof(ruleId));
            return $"{campaignId}|{ruleId}";
        }
    }

    public static class RuleKindDiscriminators
    {
        public const string AndRule = "AndRule";
        public const string OrRule = "OrRule";
        public const string NotRule = "NotRule";
        public const string SimpleRule = "SimpleRule";
        public const string DatePropertyRule = "DatePropertyRule";
        public const string HistoricalRule = "HistoricalRule";
        public const string NumericPropertyRule = "NumericPropertyRule";
        public const string StringPropertyRule = "StringPropertyRule";
        public const string TemporalConstraintRule = "TemporalConstraintRule";
        public const string TaxonomicRule = "TaxonomicRule";
    }

    public static class ProviderKindDiscriminators
    {
        public const string SimpleCalculationProvider = "SimpleCalculationProvider";
        public const string AggregateValueProvider = "AggregateValueProvider";
        public const string ConstantValueProvider = "ConstantValueProvider";
        public const string LineageValueProvider = "LineageValueProvider";
        public const string PathValueProvider = "PathValueProvider";
    }

    public static class OutcomeKindDiscriminators
    {
        public const string DepositPointsOutcome = "DepositPointsOutcome";
        public const string SpendPointsOutcome = "SpendPointsOutcome";
        public const string ExpirePointsOutcome = "ExpirePointsOutcome";
        public const string TagOutcome = "TagOutcome";
        public const string NotificationOutcome = "NotificationOutcome";
        public const string WorkflowOutcome = "WorkflowOutcome";
        public const string RuleStateOutcome = "RuleStateOutcome";
    }
}

