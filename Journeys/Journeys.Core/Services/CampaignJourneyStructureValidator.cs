using System;
using System.Collections.Generic;
using System.Reflection;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Services
{
    /// <summary>
    /// Structural validation of <see cref="Campaign.Journey"/> before persist (UpsertCampaign).
    /// Catches common deserialization gaps (e.g. missing <c>$type</c> on nested <see cref="IValueProvider"/>)
    /// that would otherwise fail at rule evaluation time.
    /// </summary>
    public static class CampaignJourneyStructureValidator
    {
        private const string MessageSuffix =
            " Nested IValueProvider objects must include a $type discriminator so they deserialize correctly (see campaign governance).";

        public static void Validate(Campaign campaign)
        {
            if (campaign?.Journey == null)
                return;

            var errors = new List<string>();
            var visitedRules = new HashSet<RuleBase>(ReferenceEqualityComparer.Instance);
            var visitedProviders = new HashSet<object>(ReferenceEqualityComparer.Instance);

            VisitJourneyNode(campaign.Journey, errors, visitedRules, visitedProviders, null);

            if (errors.Count == 0)
                return;

            var dict = new Dictionary<string, string>();
            for (var i = 0; i < errors.Count; i++)
                dict[$"journey.validation.{i}"] = errors[i];

            throw new APIErrorsException(dict);
        }

        private static void VisitJourneyNode(
            JourneyNode node,
            List<string> errors,
            HashSet<RuleBase> visitedRules,
            HashSet<object> visitedProviders,
            string? journeyNodePath)
        {
            var nodePath = string.IsNullOrEmpty(journeyNodePath) ? $"nodeId={node.Id}" : $"{journeyNodePath}/nodeId={node.Id}";

            if (node.Rules != null)
            {
                foreach (var ruleSet in node.Rules)
                {
                    var ruleSetLabel = RuleSetDisplayName(ruleSet);
                    VisitRule(ruleSet?.RuleTree, ruleSetLabel, "RuleTree", nodePath, errors, visitedRules, visitedProviders);
                    if (ruleSet?.Outcomes != null)
                    {
                        for (var i = 0; i < ruleSet.Outcomes.Count; i++)
                            VisitOutcome(ruleSet.Outcomes[i], ruleSetLabel, nodePath, i, errors, visitedRules, visitedProviders);
                    }
                }
            }

            if (node.NavigationCriteria != null)
            {
                foreach (var (navType, criteria) in node.NavigationCriteria)
                {
                    if (criteria is not SimpleNavigationCriteria simple || simple.NavConstraint == null)
                        continue;

                    var navPath = $"navigation.{navType}";
                    VisitRule(
                        simple.NavConstraint,
                        navPath,
                        navPath,
                        nodePath,
                        errors,
                        visitedRules,
                        visitedProviders);
                }
            }

            if (node.Children == null)
                return;

            for (var c = 0; c < node.Children.Count; c++)
                VisitJourneyNode(node.Children[c], errors, visitedRules, visitedProviders, $"{nodePath}/child[{c}]");
        }

        private static string RuleSetDisplayName(RuleSet ruleSet)
        {
            if (!string.IsNullOrWhiteSpace(ruleSet.Name))
                return ruleSet.Name;
            if (!string.IsNullOrWhiteSpace(ruleSet.Id))
                return ruleSet.Id;
            return "unknown";
        }

        private static void VisitOutcome(
            OutcomeBase? outcome,
            string ruleSetDisplayName,
            string journeyNodePath,
            int outcomeIndex,
            List<string> errors,
            HashSet<RuleBase> visitedRules,
            HashSet<object> visitedProviders)
        {
            if (outcome == null)
                return;

            ValidateDepositPointsOutcomeContract(outcome, ruleSetDisplayName, journeyNodePath, outcomeIndex, errors);
            ValidateSpendPointsOutcomeContract(outcome, ruleSetDisplayName, journeyNodePath, outcomeIndex, errors);
            ValidateExpirePointsOutcomeContract(outcome, ruleSetDisplayName, journeyNodePath, outcomeIndex, errors);
            ValidateOutcomeEventIdProviderContract(outcome, ruleSetDisplayName, journeyNodePath, outcomeIndex, errors);

            foreach (var prop in outcome.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;
                if (!typeof(IValueProvider).IsAssignableFrom(prop.PropertyType))
                    continue;
                var value = prop.GetValue(outcome) as IValueProvider;
                VisitValueProvider(value, ruleSetDisplayName, $"{outcome.Kind}.{prop.Name}", journeyNodePath, errors, visitedRules, visitedProviders);
            }
        }

        private static void VisitRule(
            RuleBase? rule,
            string ruleSetDisplayName,
            string rulePath,
            string journeyNodePath,
            List<string> errors,
            HashSet<RuleBase> visitedRules,
            HashSet<object> visitedProviders)
        {
            if (rule == null)
                return;

            if (!visitedRules.Add(rule))
                return;

            if (rule is CompositeRuleBase composite)
            {
                ValidateCompositeChildren(composite, ruleSetDisplayName, rulePath, journeyNodePath, errors);
                if (composite.Children != null)
                {
                    for (var i = 0; i < composite.Children.Count; i++)
                    {
                        var child = composite.Children[i];
                        VisitRule(child, ruleSetDisplayName, $"{rulePath}/Children[{i}]", journeyNodePath, errors, visitedRules, visitedProviders);
                    }
                }

                return;
            }

            if (rule is TemporalConstraintRule temporal)
            {
                if (temporal.TimeOfOccurrenceProvider == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_TEMPORAL_MISSING_TIME_PROVIDER",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={rule.Kind} field=TimeOfOccurrenceProvider"));
                }

                if (temporal.TemporalEvaluation == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_TEMPORAL_MISSING_EVALUATION",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={rule.Kind} field=TemporalEvaluation"));
                }
                else
                {
                    ValidateTemporalComparison(
                        temporal.TemporalEvaluation,
                        ruleSetDisplayName,
                        journeyNodePath,
                        rulePath,
                        rule.Kind,
                        errors);
                }

                // Comparison is struct TimeSpan — always present; no null check.
            }

            if (rule is HistoricalRule historical)
            {
                if (historical.HistoricalValueProvider == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_HISTORICAL_MISSING_VALUE_PROVIDER",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={rule.Kind} field=HistoricalValueProvider"));
                }
            }

            if (rule is TaxonomicRule taxonomic)
            {
                if (taxonomic.LeftProvider == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_TAXONOMIC_MISSING_LEFT_PROVIDER",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={taxonomic.Kind} field=LeftProvider"));
                }

                if (taxonomic.Evaluator == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_TAXONOMIC_MISSING_EVALUATOR",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={taxonomic.Kind} field=Evaluator"));
                }
                else
                {
                    ValidateEvaluatorComparison(
                        taxonomic.Evaluator,
                        ruleSetDisplayName,
                        journeyNodePath,
                        rulePath,
                        taxonomic.Kind,
                        errors);
                }
            }
            else if (TryGetSimpleRuleInstantiation(rule, out var simpleRuleType))
            {
                // Non-taxonomic SimpleRule<T>: Evaluate requires left, right, and evaluator (see SimpleRule<T>.Evaluate).
                var leftProp = simpleRuleType!.GetProperty(nameof(SimpleRule<decimal>.LeftProvider), BindingFlags.Public | BindingFlags.Instance);
                var rightProp = simpleRuleType.GetProperty(nameof(SimpleRule<decimal>.RightProvider), BindingFlags.Public | BindingFlags.Instance);
                var evalProp = simpleRuleType.GetProperty(nameof(SimpleRule<decimal>.Evaluator), BindingFlags.Public | BindingFlags.Instance);
                if (leftProp?.GetValue(rule) == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_SIMPLE_RULE_MISSING_LEFT_PROVIDER",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={rule.Kind} field=LeftProvider"));
                }

                if (rightProp?.GetValue(rule) == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_SIMPLE_RULE_MISSING_RIGHT_PROVIDER",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={rule.Kind} field=RightProvider"));
                }

                if (evalProp?.GetValue(rule) == null)
                {
                    errors.Add(TierAError(
                        "TIER_A_SIMPLE_RULE_MISSING_EVALUATOR",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={rule.Kind} field=Evaluator"));
                }
                else if (evalProp.GetValue(rule) is IEvaluatable evaluator)
                {
                    ValidateEvaluatorComparison(
                        evaluator,
                        ruleSetDisplayName,
                        journeyNodePath,
                        rulePath,
                        rule.Kind,
                        errors);
                }
            }

            foreach (var prop in rule.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                var value = prop.GetValue(rule);
                if (value == null)
                    continue;

                if (typeof(IValueProvider).IsAssignableFrom(prop.PropertyType))
                {
                    VisitValueProvider((IValueProvider)value, ruleSetDisplayName, $"{rule.Kind}.{prop.Name}", journeyNodePath, errors, visitedRules, visitedProviders);
                }
                else if (typeof(RuleBase).IsAssignableFrom(prop.PropertyType))
                {
                    VisitRule((RuleBase)value, ruleSetDisplayName, $"{rulePath}.{prop.Name}", journeyNodePath, errors, visitedRules, visitedProviders);
                }
            }
        }

        private static void ValidateCompositeChildren(
            CompositeRuleBase composite,
            string ruleSetDisplayName,
            string rulePath,
            string journeyNodePath,
            List<string> errors)
        {
            var count = composite.Children?.Count ?? 0;
            if (composite is NotRule)
            {
                if (count != 1)
                {
                    errors.Add(TierAError(
                        "TIER_A_NOT_RULE_CHILD_COUNT",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind=NotRule expectedChildren=1 actualChildren={count}"));
                }

                return;
            }

            if (composite is AndRule or OrRule)
            {
                if (composite.Children == null || composite.Children.Count == 0)
                {
                    errors.Add(TierAError(
                        "TIER_A_COMPOSITE_EMPTY_CHILDREN",
                        ruleSetDisplayName,
                        journeyNodePath,
                        $"rulePath={rulePath} kind={composite.Kind} expectedChildren>=1 actualChildren={count}"));
                }
            }
        }

        private static bool TryGetSimpleRuleInstantiation(RuleBase rule, out Type? simpleRuleType)
        {
            for (var t = rule.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SimpleRule<>))
                {
                    simpleRuleType = t;
                    return true;
                }
            }

            simpleRuleType = null;
            return false;
        }

        private static string TierAError(string code, string ruleSetDisplayName, string journeyNodePath, string details) =>
            $"[violation={code}] ruleSet={ruleSetDisplayName} {journeyNodePath} {details}";

        private static void ValidateEvaluatorComparison(
            IEvaluatable evaluator,
            string ruleSetDisplayName,
            string journeyNodePath,
            string rulePath,
            string ruleKind,
            List<string> errors)
        {
            switch (evaluator)
            {
                case NumericEvaluation numeric when numeric.Comparison == NumEvalType.Unassigned:
                    AddUnassignedComparisonError(
                        ruleSetDisplayName,
                        journeyNodePath,
                        rulePath,
                        ruleKind,
                        nameof(NumericEvaluation),
                        errors);
                    break;
                case StringEvaluation str when str.Comparison == StringEvalType.Unassigned:
                    AddUnassignedComparisonError(
                        ruleSetDisplayName,
                        journeyNodePath,
                        rulePath,
                        ruleKind,
                        nameof(StringEvaluation),
                        errors);
                    break;
                case DateEvaluation date when date.Comparison == DateEvalType.Unassigned:
                    AddUnassignedComparisonError(
                        ruleSetDisplayName,
                        journeyNodePath,
                        rulePath,
                        ruleKind,
                        nameof(DateEvaluation),
                        errors);
                    break;
            }
        }

        private static void ValidateTemporalComparison(
            TemporalEvaluation temporalEvaluation,
            string ruleSetDisplayName,
            string journeyNodePath,
            string rulePath,
            string ruleKind,
            List<string> errors)
        {
            if (temporalEvaluation.Comparison != TemporalEvalType.Unassigned)
                return;

            AddUnassignedComparisonError(
                ruleSetDisplayName,
                journeyNodePath,
                rulePath,
                ruleKind,
                nameof(TemporalEvaluation),
                errors);
        }

        private static void AddUnassignedComparisonError(
            string ruleSetDisplayName,
            string journeyNodePath,
            string rulePath,
            string ruleKind,
            string evaluatorType,
            List<string> errors)
        {
            errors.Add(TierAError(
                "JOURNEY_EVAL_UNASSIGNED_COMPARISON",
                ruleSetDisplayName,
                journeyNodePath,
                $"rulePath={rulePath} kind={ruleKind} evaluator={evaluatorType} field=comparison "
                + "— set comparison to a valid enum value (e.g. GreaterThanOrEqual). "
                + "Legacy EvalType is accepted on deserialize but comparison is canonical."));
        }

        private static void VisitValueProvider(
            IValueProvider? provider,
            string ruleSetDisplayName,
            string path,
            string journeyNodePath,
            List<string> errors,
            HashSet<RuleBase> visitedRules,
            HashSet<object> visitedProviders)
        {
            if (provider == null)
                return;

            if (!visitedProviders.Add(provider))
                return;

            switch (provider)
            {
                case AggregateValueProvider agg:
                    ValidateAggregate(agg, path, errors);
                    VisitValueProvider(agg.RowProvider, ruleSetDisplayName, $"{path}.RowProvider", journeyNodePath, errors, visitedRules, visitedProviders);
                    VisitValueProvider(agg.RowPropertyProvider, ruleSetDisplayName, $"{path}.RowPropertyProvider", journeyNodePath, errors, visitedRules, visitedProviders);
                    VisitRule(agg.Constraint, ruleSetDisplayName, $"{path}.Constraint", journeyNodePath, errors, visitedRules, visitedProviders);
                    return;

                case SimpleCalculationProvider calc:
                    VisitValueProvider(
                        calc.InstanceValueProvider,
                        ruleSetDisplayName,
                        $"{path}.InstanceValueProvider",
                        journeyNodePath,
                        errors,
                        visitedRules,
                        visitedProviders);
                    VisitRule(calc.CalculationGatingConstraint, ruleSetDisplayName, $"{path}.CalculationGatingConstraint", journeyNodePath, errors, visitedRules, visitedProviders);
                    VisitRule(calc.TemporalConstraint, ruleSetDisplayName, $"{path}.TemporalConstraint", journeyNodePath, errors, visitedRules, visitedProviders);
                    return;

                default:
                    VisitNestedValueProvidersByReflection(provider, ruleSetDisplayName, path, journeyNodePath, errors, visitedRules, visitedProviders);
                    return;
            }
        }

        private static void VisitNestedValueProvidersByReflection(
            IValueProvider provider,
            string ruleSetDisplayName,
            string path,
            string journeyNodePath,
            List<string> errors,
            HashSet<RuleBase> visitedRules,
            HashSet<object> visitedProviders)
        {
            foreach (var prop in provider.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;
                if (!typeof(IValueProvider).IsAssignableFrom(prop.PropertyType))
                    continue;
                var nested = prop.GetValue(provider) as IValueProvider;
                VisitValueProvider(nested, ruleSetDisplayName, $"{path}.{prop.Name}", journeyNodePath, errors, visitedRules, visitedProviders);
            }
        }

        private static void ValidateAggregate(AggregateValueProvider agg, string path, List<string> errors)
        {
            if (agg.RowProvider == null)
                errors.Add($"{path}: AggregateValueProvider.RowProvider is required.{MessageSuffix}");
            if (agg.RowPropertyProvider == null)
                errors.Add($"{path}: AggregateValueProvider.RowPropertyProvider is required.{MessageSuffix}");
            if (agg.AggregateType == AggregateType.Undefined)
                errors.Add($"{path}: AggregateValueProvider.AggregateType must not be Undefined.");
        }

        private static void ValidateDepositPointsOutcomeContract(
            OutcomeBase outcome,
            string ruleSetDisplayName,
            string journeyNodePath,
            int outcomeIndex,
            List<string> errors)
        {
            if (outcome is not DepositPointsOutcome deposit)
                return;

            if (deposit.DollarAmountProvider == null)
            {
                var n = outcomeIndex + 1;
                errors.Add(
                    $"[violation=TIER_A_DEPOSIT_MISSING_DOLLAR_AMOUNT_PROVIDER] ruleSet={ruleSetDisplayName} {journeyNodePath} outcomeIndex={outcomeIndex} outcomeOrdinal={n} kind={outcome.Kind} field=DollarAmountProvider Rule set outcome [DepositPoints] #{n}: DollarAmountProvider is required.");
            }

            ValidatePointOutcomePatIds(
                deposit,
                "TIER_A_DEPOSIT_MISSING_AFFECTED_PAT",
                "DepositPoints",
                ruleSetDisplayName,
                journeyNodePath,
                outcomeIndex,
                errors);
        }

        private static void ValidateSpendPointsOutcomeContract(
            OutcomeBase outcome,
            string ruleSetDisplayName,
            string journeyNodePath,
            int outcomeIndex,
            List<string> errors)
        {
            if (outcome is not SpendPointsOutcome spend)
                return;

            if (spend.WithdrawlAmountProvider == null)
            {
                var n = outcomeIndex + 1;
                errors.Add(
                    $"[violation=TIER_A_SPEND_MISSING_WITHDRAWAL_AMOUNT_PROVIDER] ruleSet={ruleSetDisplayName} {journeyNodePath} outcomeIndex={outcomeIndex} outcomeOrdinal={n} kind={outcome.Kind} field=WithdrawlAmountProvider Rule set outcome [SpendPoints] #{n}: WithdrawlAmountProvider is required.");
            }

            ValidatePointOutcomePatIds(
                spend,
                "TIER_A_SPEND_MISSING_AFFECTED_PAT",
                "SpendPoints",
                ruleSetDisplayName,
                journeyNodePath,
                outcomeIndex,
                errors);
        }

        private static void ValidateExpirePointsOutcomeContract(
            OutcomeBase outcome,
            string ruleSetDisplayName,
            string journeyNodePath,
            int outcomeIndex,
            List<string> errors)
        {
            if (outcome is not ExpirePointsOutcome expire)
                return;

            ValidatePointOutcomePatIds(
                expire,
                "TIER_A_EXPIRE_MISSING_AFFECTED_PAT",
                "ExpirePoints",
                ruleSetDisplayName,
                journeyNodePath,
                outcomeIndex,
                errors);
        }

        private static void ValidatePointOutcomePatIds(
            PointOutcomeBase outcome,
            string violationCode,
            string outcomeLabel,
            string ruleSetDisplayName,
            string journeyNodePath,
            int outcomeIndex,
            List<string> errors)
        {
            var ids = outcome.AffectedPointAccountTypeIds;
            if (ids != null && ids.Any(id => !string.IsNullOrWhiteSpace(id)))
                return;

            var n = outcomeIndex + 1;
            errors.Add(
                $"[violation={violationCode}] ruleSet={ruleSetDisplayName} {journeyNodePath} outcomeIndex={outcomeIndex} outcomeOrdinal={n} kind={outcome.Kind} field=AffectedPointAccountTypeIds Rule set outcome [{outcomeLabel}] #{n}: at least one point account type id is required.");
        }

        private static void ValidateOutcomeEventIdProviderContract(
            OutcomeBase outcome,
            string ruleSetDisplayName,
            string journeyNodePath,
            int outcomeIndex,
            List<string> errors)
        {
            if (outcome is not (DepositPointsOutcome or SpendPointsOutcome or ExpirePointsOutcome or TagOutcome))
                return;

            if (outcome.EventIdProvider != null)
                return;

            var outcomeKind = outcome switch
            {
                DepositPointsOutcome => "DepositPoints",
                SpendPointsOutcome => "SpendPoints",
                ExpirePointsOutcome => "ExpirePoints",
                TagOutcome => "Tag",
                _ => outcome.Kind
            };

            var n = outcomeIndex + 1;
            errors.Add(
                $"[violation=TIER_A_OUTCOME_MISSING_EVENT_ID_PROVIDER] ruleSet={ruleSetDisplayName} {journeyNodePath} outcomeIndex={outcomeIndex} outcomeOrdinal={n} kind={outcome.Kind} field=EventIdProvider Rule set outcome [{outcomeKind}] #{n}: EventIdProvider is required.");
        }
    }
}
