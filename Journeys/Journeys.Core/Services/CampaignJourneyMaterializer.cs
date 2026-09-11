using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;

namespace Journeys.Core.Services;

/// <summary>
/// Forces deserialization of journey rule trees and outcome sets so JSON/schema failures surface during upsert validation,
/// not lazily at runtime.
/// </summary>
internal static class CampaignJourneyMaterializer
{
    public static void Materialize(Campaign campaign)
    {
        if (campaign?.Journey == null)
            return;

        VisitNode(campaign.Journey, "journey");
    }

    private static void VisitNode(JourneyNode node, string path)
    {
        if (node.Rules != null)
        {
            for (var i = 0; i < node.Rules.Count; i++)
                MaterializeRuleSet(node.Rules[i], $"{path}/rules[{i}]");
        }

        if (node.Children == null)
            return;

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] != null)
                VisitNode(node.Children[i], $"{path}/children[{i}]");
        }
    }

    private static void MaterializeRuleSet(RuleSet? ruleSet, string path)
    {
        if (ruleSet == null)
            return;

        TryMaterialize(() => _ = ruleSet.RuleTree, $"{path}/ruleJsonElement");
        TryMaterialize(() => _ = ruleSet.Outcomes, $"{path}/outcomesJsonElement");
    }

    private static void TryMaterialize(Action materialize, string jsonPath)
    {
        try
        {
            materialize();
        }
        catch (Exception ex) when (CampaignJourneyMaterializeValidationHelper.IsJsonMaterializationFailure(ex))
        {
            if (CampaignJourneyMaterializeValidationHelper.IsStringEncodedShapeFailure(ex))
            {
                var slot = jsonPath.EndsWith("outcomesJsonElement", StringComparison.Ordinal)
                    ? "outcomesJsonElement"
                    : "ruleJsonElement";

                throw new JourneyMaterializePathException(
                    JourneyRuleShapeRules.FormatStringEncodedViolation(jsonPath, slot),
                    ex);
            }

            if (CampaignJourneyMaterializeValidationHelper.IsArrayObjectShapeFailure(ex))
            {
                var slot = jsonPath.EndsWith("outcomesJsonElement", StringComparison.Ordinal)
                    ? "outcomesJsonElement"
                    : "ruleJsonElement";

                throw new JourneyMaterializePathException(
                    JourneyRuleShapeRules.FormatViolation(jsonPath, slot),
                    ex);
            }

            throw new JourneyMaterializePathException(
                $"path={jsonPath} — {ex.InnerException?.Message ?? ex.Message}",
                ex);
        }
    }
}
