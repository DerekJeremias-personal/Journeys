using System.Text.Json;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Builds machine-readable campaign JSON with journey.children[] (never nodes[]) for validate-first authoring.
/// </summary>
public static class JourneyAuthoringTemplateBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string? Build(string? pointAccountManifestJson, string? eventModelId, string? patternId)
    {
        if (string.IsNullOrWhiteSpace(eventModelId))
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

        var template = new Dictionary<string, object?>
        {
            ["events"] = new[] { eventModelId },
            ["journey"] = new Dictionary<string, object?>
            {
                ["navigation"] = new Dictionary<string, object?>
                {
                    ["Entry"] = new Dictionary<string, object?>
                    {
                        ["$type"] = "SimpleNavigationCriteria",
                        ["name"] = "Entry",
                        ["navConstraint"] =
                            "<SimpleRule — ConstantValueProvider true + BoolEvaluation Equal>"
                    }
                },
                ["children"] = new[]
                {
                    BuildTierChild("Bronze", tqp.Id, spendable.Id, 0),
                    BuildTierChild("Silver", tqp.Id, spendable.Id, 500),
                    BuildTierChild("Gold", tqp.Id, spendable.Id, 1000)
                }
            },
            ["_authoringNotes"] = new Dictionary<string, object?>
            {
                ["pattern"] = effectivePattern,
                ["eventsShape"] = "string GUID array only",
                ["depositEarn"] = "PathValueProvider event.ordertotal + PointsPerDollar — not AggregateValueProvider",
                ["forbidden"] = "journey.nodes[] — use children[] only"
            }
        };

        return JsonSerializer.Serialize(template, JsonOpts);
    }

    private static Dictionary<string, object?> BuildTierChild(
        string name,
        string tqpPatId,
        string spendablePatId,
        int threshold) =>
        new()
        {
            ["name"] = name,
            ["navigation"] = new Dictionary<string, object?>
            {
                ["Transition"] =
                    $"<AndRule + PointBalanceProvider on {tqpPatId} threshold {threshold}+>"
            },
            ["rules"] = new[]
            {
                "<RuleSet wrapper ref — DepositPointsOutcome spendable "
                + spendablePatId
                + " + TQP "
                + tqpPatId
                + ">"
            }
        };
}
