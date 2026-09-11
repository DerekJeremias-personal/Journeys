using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;

namespace Journeys.Core.Services;

/// <summary>
/// Detects common agent journey JSON mistakes (flat RuleBase in <c>journey.rules[]</c>) before persist.
/// </summary>
public static class CampaignJourneyAuthoringShapeValidator
{
    private static readonly string[] RuleSetWrapperProps =
    [
        "ruleJsonElement", "RuleJsonElement",
        "outcomesJsonElement", "OutcomesJsonElement"
    ];

    private static readonly string[] FlatRuleProps =
    [
        "kind", "Kind",
        "children", "Children",
        "leftProvider", "LeftProvider",
        "rightProvider", "RightProvider",
        "evaluator", "Evaluator"
    ];

    private static readonly string[] InlineOutcomeProps = ["outcomes", "Outcomes"];

    public static void ValidateCampaignJson(JsonElement campaignRoot)
    {
        if (!TryGetJourneyObject(campaignRoot, out var journey))
            return;

        var errors = new List<string>();
        WalkJourneyJson(journey, "journey", errors);

        if (errors.Count == 0)
            return;

        ThrowShapeErrors(errors);
    }

    public static void ValidateInlineManifestForbidden(JsonElement campaignRoot, int workflowPatCount)
    {
        if (workflowPatCount <= 0)
            return;

        if (!TryGetProperty(campaignRoot, "pointAccountManifest", out var manifest)
            && !TryGetProperty(campaignRoot, "PointAccountManifest", out manifest))
            return;

        if (manifest.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return;

        ThrowShapeErrors([
            "[violation=JOURNEY_SHAPE_INLINE_MANIFEST] path=campaign/pointAccountManifest — "
            + "use WORKFLOW PointAccountManifest ids in outcomes; do not embed pointAccountManifest in campaign JSON."
        ]);
    }

    public static void ValidateCampaign(Campaign campaign)
    {
        if (campaign?.Journey == null)
            return;

        var errors = new List<string>();
        WalkJourneyNode(campaign.Journey, "journey", errors);

        if (errors.Count == 0)
            return;

        ThrowShapeErrors(errors);
    }

    public static void ValidateJourneyDto(JourneyDto? journey)
    {
        if (journey == null)
            return;

        var errors = new List<string>();
        WalkJourneyDto(journey, "journey", errors);

        if (errors.Count == 0)
            return;

        ThrowShapeErrors(errors);
    }

    private static void WalkJourneyJson(JsonElement node, string path, List<string> errors)
    {
        if ((TryGetProperty(node, "nodes", out var nodesProp) || TryGetProperty(node, "Nodes", out nodesProp))
            && nodesProp.ValueKind == JsonValueKind.Array
            && nodesProp.GetArrayLength() > 0)
        {
            errors.Add(
                $"[violation=JOURNEY_SHAPE_NODES_NOT_CHILDREN] path={path}/nodes — "
                + "tier journeys use journey.children[] (or node-level Rules), not journey.nodes[]. "
                + "Remove nodes and author children[] with per-tier RuleSet entries.");
            return;
        }

        if (TryGetProperty(node, "rules", out var rules) || TryGetProperty(node, "Rules", out rules))
            ValidateRulesArrayJson(rules, $"{path}/rules", errors);

        if (TryGetProperty(node, "navigation", out var navigation) || TryGetProperty(node, "Navigation", out navigation))
            JourneyRuleShapeRules.WalkNavigationShape(navigation, $"{path}/navigation", errors);

        if (!TryGetProperty(node, "children", out var children) && !TryGetProperty(node, "Children", out children))
            return;

        if (children.ValueKind != JsonValueKind.Array)
            return;

        var i = 0;
        foreach (var child in children.EnumerateArray())
        {
            if (child.ValueKind == JsonValueKind.Object)
                WalkJourneyJson(child, $"{path}/children[{i}]", errors);
            i++;
        }
    }

    private static void ValidateRulesArrayJson(JsonElement rules, string path, List<string> errors)
    {
        if (rules.ValueKind != JsonValueKind.Array)
            return;

        var i = 0;
        foreach (var entry in rules.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object)
                ValidateRuleSetEntryJson(entry, $"{path}[{i}]", errors);
            i++;
        }
    }

    private static void ValidateRuleSetEntryJson(JsonElement entry, string path, List<string> errors)
    {
        var hasWrapper = HasAnyProperty(entry, RuleSetWrapperProps);
        var hasFlatRule = HasAnyProperty(entry, FlatRuleProps);
        var hasInlineOutcomes = HasAnyProperty(entry, InlineOutcomeProps);

        if (!hasWrapper && (hasFlatRule || hasInlineOutcomes))
        {
            errors.Add(
                $"[violation=JOURNEY_SHAPE_FLAT_RULE_IN_RULESET_ARRAY] path={path} — "
                + "journey.rules[] must contain RuleSet objects with ruleJsonElement (Kind rule) "
                + "and outcomesJsonElement (outcome array). Do not place Kind/children/outcomes directly in rules[].");
            return;
        }

        if (hasWrapper && !HasNonEmptyJsonProperty(entry, "ruleJsonElement", "RuleJsonElement")
                      && !HasNonEmptyJsonProperty(entry, "outcomesJsonElement", "OutcomesJsonElement"))
        {
            errors.Add(
                $"[violation=JOURNEY_SHAPE_EMPTY_RULESET] path={path} — "
                + "RuleSet entry has no ruleJsonElement or outcomesJsonElement payload.");
        }

        if (TryGetProperty(entry, "ruleJsonElement", out var ruleJson) || TryGetProperty(entry, "RuleJsonElement", out ruleJson))
            JourneyRuleShapeRules.ValidateSingleRuleElement(ruleJson, $"{path}/ruleJsonElement", "ruleJsonElement", errors);
    }

    private static void WalkJourneyNode(JourneyNode node, string path, List<string> errors)
    {
        if (node.Rules is { Count: > 0 })
            ValidateRuleSets(node.Rules, $"{path}/rules", errors);

        if (HasJsonElement(node.Navigation))
            JourneyRuleShapeRules.WalkNavigationShape(node.Navigation!.Value, $"{path}/navigation", errors);

        if (node.Children == null)
            return;

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] != null)
                WalkJourneyNode(node.Children[i], $"{path}/children[{i}]", errors);
        }
    }

    private static void WalkJourneyDto(JourneyDto node, string path, List<string> errors)
    {
        if (node.Rules is { Count: > 0 })
            ValidateRuleSetDtos(node.Rules, $"{path}/rules", errors);

        if (HasJsonElement(node.Navigation))
            JourneyRuleShapeRules.WalkNavigationShape(node.Navigation!.Value, $"{path}/navigation", errors);

        if (node.Children == null)
            return;

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i] != null)
                WalkJourneyDto(node.Children[i], $"{path}/children[{i}]", errors);
        }
    }

    private static void ValidateRuleSets(IReadOnlyList<RuleSet> ruleSets, string path, List<string> errors)
    {
        for (var i = 0; i < ruleSets.Count; i++)
        {
            var rs = ruleSets[i];
            if (rs == null)
                continue;

            if (!HasRuleSetPayload(rs))
            {
                errors.Add(
                    $"[violation=JOURNEY_SHAPE_EMPTY_RULESET] path={path}[{i}] ruleSet={RuleSetLabel(rs)} — "
                    + "deserialized as an empty RuleSet (likely flat RuleBase placed in rules[]). "
                    + "Wrap with ruleJsonElement and outcomesJsonElement per campaign journey governance.");
            }

            if (HasJsonElement(rs.RuleJsonElement))
            {
                JourneyRuleShapeRules.ValidateSingleRuleElement(
                    rs.RuleJsonElement!.Value, $"{path}[{i}]/ruleJsonElement", "ruleJsonElement", errors);
            }
        }
    }

    private static void ValidateRuleSetDtos(IReadOnlyList<RuleSetDto> ruleSets, string path, List<string> errors)
    {
        for (var i = 0; i < ruleSets.Count; i++)
        {
            var rs = ruleSets[i];
            if (rs == null)
                continue;

            if (!HasRuleSetDtoPayload(rs))
            {
                errors.Add(
                    $"[violation=JOURNEY_SHAPE_EMPTY_RULESET] path={path}[{i}] ruleSet={RuleSetLabel(rs)} — "
                    + "RuleSet has no ruleJsonElement or outcomesJsonElement.");
            }

            if (HasJsonElement(rs.RuleJsonElement))
            {
                JourneyRuleShapeRules.ValidateSingleRuleElement(
                    rs.RuleJsonElement!.Value, $"{path}[{i}]/ruleJsonElement", "ruleJsonElement", errors);
            }
        }
    }

    internal static bool HasRuleSetPayload(RuleSet ruleSet) =>
        HasJsonElement(ruleSet.RuleJsonElement) || HasJsonElement(ruleSet.OutcomesJsonElement)
        || ruleSet.RuleTree != null || ruleSet.Outcomes is { Count: > 0 };

    internal static bool HasRuleSetDtoPayload(RuleSetDto ruleSet) =>
        HasJsonElement(ruleSet.RuleJsonElement) || HasJsonElement(ruleSet.OutcomesJsonElement);

    private static bool HasJsonElement(JsonElement? el) =>
        el != null && el.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static string RuleSetLabel(RuleSet rs) =>
        !string.IsNullOrWhiteSpace(rs.Name) ? rs.Name : rs.Id ?? "unknown";

    private static string RuleSetLabel(RuleSetDto rs) =>
        !string.IsNullOrWhiteSpace(rs.Name) ? rs.Name : rs.Id ?? "unknown";

    private static bool TryGetJourneyObject(JsonElement campaignRoot, out JsonElement journey)
    {
        if (TryGetProperty(campaignRoot, "journey", out journey) || TryGetProperty(campaignRoot, "Journey", out journey))
            return journey.ValueKind == JsonValueKind.Object;
        journey = default;
        return false;
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value))
            return true;
        value = default;
        return false;
    }

    private static bool HasAnyProperty(JsonElement el, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out _))
                return true;
        }

        return false;
    }

    private static bool HasNonEmptyJsonProperty(JsonElement el, string camel, string pascal)
    {
        if (!TryGetProperty(el, camel, out var value) && !TryGetProperty(el, pascal, out value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().Any(),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => true,
            _ => false
        };
    }

    private static void ThrowShapeErrors(List<string> errors)
    {
        var dict = new Dictionary<string, string>();
        for (var i = 0; i < errors.Count; i++)
            dict[$"journey.shape.{i}"] = errors[i];

        throw new APIErrorsException(dict);
    }
}
