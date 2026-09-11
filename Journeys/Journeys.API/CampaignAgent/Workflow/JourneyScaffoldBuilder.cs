using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

public static class JourneyScaffoldBuilder
{
    public static string? Build(RuleEpisode episode, string? pointAccountManifestJson, string? patternId, string? briefJson = null)
    {
        if (episode != RuleEpisode.TierLadder && string.IsNullOrWhiteSpace(patternId))
            return null;

        var manifest = PointAccountManifestBuilder.Parse(pointAccountManifestJson);
        if (manifest.Items.Count == 0)
            return null;

        var tqp = manifest.Items.FirstOrDefault(i =>
                      i.DisplayLabel.Contains("tier", StringComparison.OrdinalIgnoreCase)
                      || i.DisplayLabel.Contains("qual", StringComparison.OrdinalIgnoreCase))
                  ?? manifest.Items.FirstOrDefault(i => i.IsSpendable != true)
                  ?? manifest.Items[0];

        var spendable = manifest.Items.FirstOrDefault(i => i.IsSpendable == true) ?? manifest.Items[0];

        var effectivePattern = patternId ?? "tier-navigation-point-balance";

        var jsonSkeleton = """
            {
              "events": ["<event-model-guid-string>"],
              "journey": {
                "navigation": {
                  "Entry": {
                    "$type": "SimpleNavigationCriteria",
                    "name": "Entry",
                    "navConstraint": "<SimpleRule JSON — ConstantValueProvider true + BoolEvaluation>"
                  }
                },
                "children": [
                  { "name": "Bronze", "rules": ["<RuleSet wrapper ref>"], "navigation": { "Transition": "<AndRule + PointBalanceProvider on TQP_PAT_ID threshold 0+>" } },
                  { "name": "Silver", "rules": ["<RuleSet wrapper ref>"], "navigation": { "Transition": "<AndRule + PointBalanceProvider on TQP_PAT_ID threshold 500+>" } },
                  { "name": "Gold", "rules": ["<RuleSet wrapper ref>"], "navigation": { "Transition": "<AndRule + PointBalanceProvider on TQP_PAT_ID threshold 1000+>" } }
                ]
              }
            }
            """.Replace("TQP_PAT_ID", tqp.Id, StringComparison.Ordinal);

        var depositTemplate = $$"""
            {
              "Kind": "DepositPointsOutcome",
              "AffectedPointAccountTypeIds": ["{{spendable.Id}}"],
              "DollarAmountProvider": {
                "$type": "PathValueProvider",
                "PropertyPath": "event.ordertotal"
              },
              "PointsPerDollar": 1.0,
              "EventIdProvider": {
                "$type": "PathValueProvider",
                "PropertyPath": "event.orderid"
              }
            }
            """;

        return $"""
                JOURNEY SCAFFOLD (fill PAT GUIDs from PointAccountManifest — do not invent)
                - pattern: {effectivePattern} (see get_example_campaign tier-system-campaign if unsure)
                - validate_campaign before upsert_campaign — fix all errors before first journey upsert
                - journeyRootName: Entry — unnamed root triggers WARN_JOURNEY_NODE_UNNAMED
                - root: navigation.Entry only (SimpleNavigationCriteria $type) — tier nodes use navigation.Transition
                - children: Bronze | Silver | Gold — distinct Transition thresholds per tier + DepositPointsOutcome ×2 (spendable {spendable.Id}, TQP {tqp.Id})
                - events[]: string GUID array at campaign root — modelMetaData keyed by event id at root
                - episode providers: PointBalanceProvider on TQP PAT; earn uses event.ordertotal per brief
                - Do NOT use journey.nodes[] — children[] only
                - Do NOT put navigation.Entry on tier nodes — Transition with AndRule + PointBalanceProvider bands
                - Do NOT embed event objects in events[] — string ids only
                - Do not use AggregateValueProvider or SimpleCalculationProvider as DollarAmountProvider
                - Do not author flat journey.rules[] at root — use children[] tree
                {(MentionsLineItemLanguage(briefJson) ? "- Brief line-item language — still use event.ordertotal unless user explicitly requires line aggregation" : string.Empty)}

                DEPOSIT POINTS OUTCOME TEMPLATE (spendable earn — adapt PAT ids):
                {depositTemplate}

                JSON SKELETON (structural — fill RuleSet refs from journey.rules[] wrappers):
                {jsonSkeleton}
                """;
    }

    private static bool MentionsLineItemLanguage(string? briefJson)
    {
        if (string.IsNullOrWhiteSpace(briefJson))
            return false;

        var lower = briefJson.ToLowerInvariant();
        return lower.Contains("line item", StringComparison.Ordinal)
               || lower.Contains("line-item", StringComparison.Ordinal)
               || lower.Contains("items", StringComparison.Ordinal)
               || lower.Contains("qty", StringComparison.Ordinal)
               || lower.Contains("quantity", StringComparison.Ordinal)
               || lower.Contains("sum ", StringComparison.Ordinal);
    }
}

