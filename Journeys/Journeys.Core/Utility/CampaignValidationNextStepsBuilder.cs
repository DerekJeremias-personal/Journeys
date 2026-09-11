using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Responses;

namespace Journeys.Core.Utility;

public static class CampaignValidationNextStepsBuilder
{
    private const int MaxSteps = 5;

    public static List<string> Build(
        IReadOnlyList<CampaignValidationFindingDto> errors,
        IReadOnlyList<CampaignValidationFindingDto> warnings,
        CampaignValidationSummaryDto summary)
    {
        List<string> steps;
        if (errors.Count > 0)
            steps = BuildRemediateSteps(errors);
        else if (warnings.Count > 0)
            steps = BuildImproveSteps(warnings);
        else
            steps = BuildAffirmSteps(summary);

        return Deduplicate(steps).Take(MaxSteps).ToList();
    }

    private static List<string> BuildRemediateSteps(IReadOnlyList<CampaignValidationFindingDto> errors)
    {
        var steps = new List<string>();
        foreach (var error in errors.Take(3))
            steps.Add(RemediateForCode(error));

        steps.Add("After hard errors are resolved, re-validate before saving.");
        if (!steps.Any(s => s.Contains("upsert_campaign", StringComparison.OrdinalIgnoreCase)
                            || s.Contains("/save", StringComparison.OrdinalIgnoreCase)))
        {
            steps.Add("When validation is clean: save as Draft via upsert_campaign or POST .../save.");
        }

        return steps;
    }

    private static List<string> BuildImproveSteps(IReadOnlyList<CampaignValidationFindingDto> warnings)
    {
        var steps = new List<string>();
        var ordered = warnings
            .OrderBy(WarningPriority)
            .ThenBy(w => w.Code, StringComparer.Ordinal)
            .ToList();

        foreach (var warning in ordered.Take(2))
            steps.Add(ImproveForCode(warning));

        steps.Add($"Review {warnings.Count} advisory item(s), then re-validate before saving as Draft.");
        return steps;
    }

    private static List<string> BuildAffirmSteps(CampaignValidationSummaryDto summary)
    {
        if (summary.HasJourney && summary.RuleSetCount == 0)
        {
            return
            [
                "Validation reported 0 rule sets — journey not authored.",
                "Add journey.rules[] (RuleSet wrappers with ruleJsonElement + outcomesJsonElement) or tier children[] before upsert.",
                "Re-validate after edits; upsert only when summary.ruleSetCount > 0."
            ];
        }

        var steps = new List<string>();
        if (summary.HasJourney && summary.RuleSetCount > 0)
        {
            steps.Add($"Validation clean — ruleSetCount={summary.RuleSetCount}; OK to upsert Draft via upsert_campaign.");
        }
        else
        {
            steps.Add("Campaign structure validated successfully — no blocking errors or advisories.");
            steps.Add("Save as Draft via upsert_campaign (or POST api/Campaign/{tenantId}/save) when ready.");
        }

        if (summary.HasJourney && summary.RuleSetCount > 0)
        {
            steps.Add("Verify Draft via process_event(campaignId) with a allowlisted test account before promotion.");
        }

        if (string.Equals(summary.Status, CampaignStatusStrings.Draft, StringComparison.OrdinalIgnoreCase))
        {
            steps.Add("Promote to Live only after explicit user approval and successful verification.");
        }

        if (!summary.HasJourney)
        {
            steps.Add("Add journey rule sets in CampaignJourney phase before promotion.");
        }

        if (summary.EventModelCount == 0)
        {
            steps.Add("Link at least one event model in Campaign.Events before journey outcomes can fire.");
        }

        return steps;
    }

    private static string RemediateForCode(CampaignValidationFindingDto error)
    {
        var code = error.Code;
        var path = string.IsNullOrWhiteSpace(error.Path) ? error.Field ?? "campaign" : error.Path;

        if (code.Contains("TIER_A_UNKNOWN_TYPE_DISCRIMINATOR", StringComparison.Ordinal))
            return "Call get_rules_engine_contract_summary for allowed $type and Kind values, then fix the cited discriminator.";

        if (code.Contains("TIER_A_HISTORICAL_MISSING_VALUE_PROVIDER", StringComparison.Ordinal)
            || (code.Contains("HISTORICAL", StringComparison.Ordinal) && code.Contains("MISSING", StringComparison.Ordinal)))
            return "Call get_rule_pattern_recipes for HistoricalRule skeletons (historical-spend-threshold, historical-event-count); wire SimpleCalculationProvider to both historicalValueProvider and aggregationValueProvider with shared Id.";

        if (code.Contains("MISSING_AFFECTED_PAT", StringComparison.Ordinal))
            return CampaignAgentGuidanceText.JsonCasingAffectedPatHint + " " + CampaignAgentGuidanceText.JsonCasingAffectedPatKeyCheckHint;

        if (code.Contains("OUTCOME_PAT_ALIAS_MISUSED", StringComparison.Ordinal))
            return "Use AffectedPointAccountTypeIds[] (PascalCase) with PAT GUIDs — not pointAccountTypeId on outcomes (PointAccountTypeId is for PointBalanceProvider in navigation).";

        if (code.Contains("OUTCOME_PAT_NOT_FOUND", StringComparison.Ordinal))
            return "Fix the cited patId using list_point_account_types; ensure PATs were upserted in PointAccountTypes phase.";

        if (code.StartsWith("JOURNEY_NAV", StringComparison.Ordinal))
            return $"Fix navigation at {path}: add Entry/Transition with PointBalanceProvider + pointAccountTypeId or an always-true Bool navConstraint.";

        if (code.Contains("MISSING", StringComparison.Ordinal) && code.StartsWith("TIER_A", StringComparison.Ordinal))
        {
            if (code.Contains("_PROVIDER", StringComparison.Ordinal))
                return CampaignAgentGuidanceText.JsonCasingPascalCaseRuleProvidersHint + " "
                       + $"Fill the required provider for {code} at {path}.";

            return $"Fill the required provider or field for {code} at {path}.";
        }

        if (code.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", StringComparison.Ordinal))
        {
            return $"ruleJsonElement at {path} must be one rule object with Kind — " +
                   "wrap multiple conditions in AndRule.Children or use separate RuleSet entries in journey.rules[].";
        }

        if (code.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, StringComparison.Ordinal))
        {
            return $"ruleJsonElement at {path} must be an embedded JSON object — remove string quotes; " +
                   "paste the rule object directly into ruleJsonElement.";
        }

        if (code.StartsWith("JOURNEY_SHAPE", StringComparison.OrdinalIgnoreCase)
            || (error.Field?.StartsWith("journey.shape", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return "Wrap flat rules in RuleSet objects with ruleJsonElement and outcomesJsonElement per campaign governance.";
        }

        if (string.Equals(error.Field, "journey.materialize", StringComparison.OrdinalIgnoreCase)
            || code.Contains("MATERIALIZE", StringComparison.OrdinalIgnoreCase))
        {
            return $"Fix journey JSON shape at {path}; compare against get_example_campaign and governance contract.";
        }

        if (string.Equals(error.Field, "status", StringComparison.OrdinalIgnoreCase)
            || string.Equals(error.Field, "startDate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(error.Field, "endDate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(error.Field, "name", StringComparison.OrdinalIgnoreCase))
        {
            return $"Fix campaign shell field '{error.Field}' before journey work.";
        }

        return $"Fix {code} at {path}, then re-validate.";
    }

    private static string ImproveForCode(CampaignValidationFindingDto warning) =>
        warning.Code switch
        {
            "WARN_JOURNEY_DUPLICATE_NODE_SIGNATURE" =>
                "Merge or differentiate journey nodes that share the same structural signature (navigation + rules).",
            "WARN_JOURNEY_DUPLICATE_NODE_NAME" or "WARN_JOURNEY_DUPLICATE_NODE_ID" =>
                "Resolve duplicate journey node names or ids — rename tiers or remove redundant copies.",
            "WARN_JOURNEY_RULESET_NO_OUTCOMES" =>
                "Add outcomes to rule sets that gate without member effects.",
            "WARN_JOURNEY_RULESET_NO_RULE_TREE" =>
                "Add rule trees to empty rule set wrappers.",
            "WARN_JOURNEY_NODE_NO_ENTRY" or "WARN_JOURNEY_NODE_NO_NAVIGATION" =>
                "Add Entry navigation so members can enter tiers and fire rule sets.",
            "WARN_JOURNEY_NODE_NO_RULES" =>
                "Add rule sets to navigation-only tiers or remove unused navigation.",
            "WARN_JOURNEY_NODE_UNNAMED" =>
                "Name journey nodes for clearer coaching and debugging.",
            _ => warning.Message
        };

    private static int WarningPriority(CampaignValidationFindingDto warning) =>
        warning.Code switch
        {
            "WARN_JOURNEY_DUPLICATE_NODE_SIGNATURE" => 0,
            "WARN_JOURNEY_DUPLICATE_NODE_NAME" => 1,
            "WARN_JOURNEY_DUPLICATE_NODE_ID" => 1,
            "WARN_JOURNEY_RULESET_NO_OUTCOMES" => 2,
            "WARN_JOURNEY_RULESET_NO_RULE_TREE" => 2,
            "WARN_JOURNEY_NODE_NO_ENTRY" => 3,
            "WARN_JOURNEY_NODE_NO_NAVIGATION" => 3,
            _ => 4
        };

    private static List<string> Deduplicate(IEnumerable<string> steps)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step) || !seen.Add(step))
                continue;
            result.Add(step);
        }

        if (result.Count == 0)
            result.Add("Re-validate the campaign after applying fixes.");

        return result;
    }
}
