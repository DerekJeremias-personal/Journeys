using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Providers;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;

namespace Journeys.Core.Services;

/// <summary>
/// Tier A navigation checks: tier nodes need Entry/Transition criteria so members can enter nodes and fire rule sets.
/// </summary>
public static class CampaignJourneyNavigationValidator
{
    private static readonly string[] InventedProviderTypes =
    [
        "CurrentBalanceProvider",
        "AccountBalanceProvider",
        "BalanceProvider",
        "TierBalanceProvider"
    ];

    private static readonly string[] NavigationChannelNames =
    [
        "Entry", "entry",
        "Transition", "transition",
        "Exit", "exit"
    ];

    public static void ValidateCampaignJson(JsonElement campaignRoot)
    {
        if (!TryGetJourney(campaignRoot, out var journey))
            return;

        var errors = new List<string>();
        WalkJourneyJson(journey, "journey", isRoot: true, errors);
        ScanInventedProviders(journey, "journey", errors);

        if (errors.Count == 0)
            return;

        ThrowNavigationErrors(errors);
    }

    public static void ValidateCampaign(Campaign campaign)
    {
        if (campaign?.Journey == null)
            return;

        var errors = new List<string>();
        WalkJourneyNode(campaign.Journey, "journey", isRoot: true, errors);

        if (errors.Count == 0)
            return;

        ThrowNavigationErrors(errors);
    }

    private static void WalkJourneyJson(JsonElement node, string path, bool isRoot, List<string> errors)
    {
        var ruleCount = CountRulesJson(node);
        var hasChildren = TryGetArray(node, "children", out var children)
                          || TryGetArray(node, "Children", out children);
        var childRuleNodes = 0;

        if (hasChildren)
        {
            var i = 0;
            foreach (var child in children.EnumerateArray())
            {
                if (child.ValueKind != JsonValueKind.Object)
                {
                    i++;
                    continue;
                }

                if (CountRulesJson(child) > 0)
                {
                    childRuleNodes++;
                    if (!HasNavigationChannel(child))
                    {
                        errors.Add(
                            $"[violation=JOURNEY_NAV_MISSING_FOR_RULE_NODE] path={path}/children[{i}] nodeId={ReadNameOrId(child)} — "
                            + "tier/rule nodes with rules[] require journey.navigation Entry or Transition "
                            + "(use PointBalanceProvider + tier-qualification pointAccountTypeId in navConstraint).");
                    }
                }

                WalkJourneyJson(child, $"{path}/children[{i}]", isRoot: false, errors);
                i++;
            }
        }

        if (isRoot && childRuleNodes > 0 && !HasNavigationChannel(node, "Entry", "entry"))
        {
            errors.Add(
                $"[violation=JOURNEY_NAV_ROOT_ENTRY_REQUIRED] path={path} — "
                + "multi-tier journeys require root journey.navigation.Entry so members can enter the journey "
                + "(use always-true Bool navConstraint or PointBalanceProvider threshold).");
        }

        if (isRoot && ruleCount > 0 && childRuleNodes == 0 && !HasNavigationChannel(node, "Entry", "entry"))
        {
            errors.Add(
                $"[violation=JOURNEY_NAV_ROOT_ENTRY_REQUIRED] path={path} — "
                + "root rule sets require journey.navigation.Entry or members never enter the node and AppliedRuleSetIds stay empty.");
        }

        ValidatePointBalanceProvidersJson(node, path, errors);
    }

    private static void WalkJourneyNode(JourneyNode node, string path, bool isRoot, List<string> errors)
    {
        var ruleCount = node.Rules?.Count(r => r != null && CampaignJourneyAuthoringShapeValidator.HasRuleSetPayload(r)) ?? 0;
        var childRuleNodes = 0;

        if (node.Children != null)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child == null)
                    continue;

                var childRules = child.Rules?.Count(r => r != null && CampaignJourneyAuthoringShapeValidator.HasRuleSetPayload(r)) ?? 0;
                if (childRules > 0)
                {
                    childRuleNodes++;
                    if (!HasNavigationChannel(child))
                    {
                        errors.Add(
                            $"[violation=JOURNEY_NAV_MISSING_FOR_RULE_NODE] path={path}/children[{i}] nodeId={child.Id ?? child.Name} — "
                            + "tier/rule nodes with rules require NavigationCriteria Entry or Transition.");
                    }
                }

                WalkJourneyNode(child, $"{path}/children[{i}]", isRoot: false, errors);
            }
        }

        if (isRoot && childRuleNodes > 0 && !HasNavigationChannel(node, NavigationType.Entry))
        {
            errors.Add(
                $"[violation=JOURNEY_NAV_ROOT_ENTRY_REQUIRED] path={path} — "
                + "multi-tier journeys require root Entry navigation.");
        }

        if (isRoot && ruleCount > 0 && childRuleNodes == 0 && !HasNavigationChannel(node, NavigationType.Entry))
        {
            errors.Add(
                $"[violation=JOURNEY_NAV_ROOT_ENTRY_REQUIRED] path={path} — "
                + "root rule sets require Entry navigation.");
        }
    }

    private static void ScanInventedProviders(JsonElement el, string path, List<string> errors)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                if (TryGetTypeDiscriminator(el, out var typeDisc)
                    && InventedProviderTypes.Any(p => string.Equals(p, typeDisc, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(
                        $"[violation=JOURNEY_NAV_INVENTED_PROVIDER] path={path} $type={typeDisc} — "
                        + "use PointBalanceProvider (valueProviderKinds) with pointAccountTypeId for tier-qualification balance checks; "
                        + "CurrentBalanceProvider does not exist.");
                }

                foreach (var prop in el.EnumerateObject())
                    ScanInventedProviders(prop.Value, $"{path}.{prop.Name}", errors);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in el.EnumerateArray())
                {
                    ScanInventedProviders(item, $"{path}[{i}]", errors);
                    i++;
                }
                break;
        }
    }

    private static void ValidatePointBalanceProvidersJson(JsonElement node, string path, List<string> errors)
    {
        if (TryGetProperty(node, "navigation", out var nav) || TryGetProperty(node, "Navigation", out nav))
            WalkPointBalanceInJson(nav, $"{path}/navigation", errors);

        if (TryGetArray(node, "rules", out var rules) || TryGetArray(node, "Rules", out rules))
        {
            foreach (var rs in rules.EnumerateArray())
            {
                if (rs.ValueKind == JsonValueKind.Object)
                    WalkPointBalanceInJson(rs, $"{path}/rules", errors);
            }
        }
    }

    private static void WalkPointBalanceInJson(JsonElement el, string path, List<string> errors)
    {
        if (el.ValueKind == JsonValueKind.Object
            && TryGetTypeDiscriminator(el, out var typeDisc)
            && string.Equals(typeDisc, nameof(PointBalanceProvider), StringComparison.OrdinalIgnoreCase)
            && !HasPointAccountTypeId(el))
        {
            errors.Add(
                $"[violation=JOURNEY_NAV_POINT_BALANCE_MISSING_PAT] path={path} — "
                + "PointBalanceProvider requires pointAccountTypeId (tier-qualification PAT from PointAccountManifest).");
        }

        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                    WalkPointBalanceInJson(prop.Value, $"{path}.{prop.Name}", errors);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in el.EnumerateArray())
                {
                    WalkPointBalanceInJson(item, $"{path}[{i}]", errors);
                    i++;
                }
                break;
        }
    }

    private static bool HasPointAccountTypeId(JsonElement el) =>
        HasNonEmptyString(el, "pointAccountTypeId") || HasNonEmptyString(el, "PointAccountTypeId");

    private static bool HasNonEmptyString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(v.GetString());

    private static int CountRulesJson(JsonElement node)
    {
        if (!TryGetArray(node, "rules", out var rules) && !TryGetArray(node, "Rules", out rules))
            return 0;

        var count = 0;
        foreach (var rs in rules.EnumerateArray())
        {
            if (rs.ValueKind == JsonValueKind.Object && HasRuleSetPayloadJson(rs))
                count++;
        }

        return count;
    }

    private static bool HasRuleSetPayloadJson(JsonElement rs) =>
        HasNonEmptyJson(rs, "ruleJsonElement", "RuleJsonElement")
        || HasNonEmptyJson(rs, "outcomesJsonElement", "OutcomesJsonElement");

    private static bool HasNonEmptyJson(JsonElement el, string camel, string pascal)
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

    private static bool HasNavigationChannel(JsonElement node, params string[] channels)
    {
        if (!TryGetProperty(node, "navigation", out var nav) && !TryGetProperty(node, "Navigation", out nav))
            return false;
        if (nav.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var channel in channels)
        {
            if (nav.TryGetProperty(channel, out var criteria) && criteria.ValueKind == JsonValueKind.Object)
                return HasNavConstraintPayload(criteria);
        }

        return false;
    }

    private static bool HasNavigationChannel(JsonElement node) =>
        NavigationChannelNames.Any(ch => HasNavigationChannel(node, ch));

    private static bool HasNavigationChannel(JourneyNode node, NavigationType type) =>
        node.NavigationCriteria != null
        && node.NavigationCriteria.TryGetValue(type, out var criteria)
        && criteria is SimpleNavigationCriteria simple
        && simple.NavConstraint != null;

    private static bool HasNavigationChannel(JourneyNode node) =>
        node.NavigationCriteria != null
        && node.NavigationCriteria.Keys.Any(k =>
            node.NavigationCriteria[k] is SimpleNavigationCriteria s && s.NavConstraint != null);

    private static bool HasNavConstraintPayload(JsonElement criteria)
    {
        if (TryGetProperty(criteria, "navConstraint", out var nc) || TryGetProperty(criteria, "NavConstraint", out nc))
            return nc.ValueKind == JsonValueKind.Object && nc.EnumerateObject().Any();
        return false;
    }

    private static bool TryGetJourney(JsonElement campaignRoot, out JsonElement journey)
    {
        if (TryGetProperty(campaignRoot, "journey", out journey) || TryGetProperty(campaignRoot, "Journey", out journey))
            return journey.ValueKind == JsonValueKind.Object;
        journey = default;
        return false;
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value) =>
        el.TryGetProperty(name, out value);

    private static bool TryGetArray(JsonElement el, string name, out JsonElement array)
    {
        if (el.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array)
            return true;
        array = default;
        return false;
    }

    private static bool TryGetTypeDiscriminator(JsonElement el, out string typeDisc)
    {
        typeDisc = string.Empty;
        if (el.TryGetProperty("$type", out var t) && t.ValueKind == JsonValueKind.String)
            typeDisc = t.GetString() ?? string.Empty;
        else if (el.TryGetProperty("Kind", out var k) && k.ValueKind == JsonValueKind.String)
            typeDisc = k.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(typeDisc);
    }

    private static string ReadNameOrId(JsonElement el)
    {
        if (el.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            return id.GetString() ?? "unknown";
        if (el.TryGetProperty("Id", out id) && id.ValueKind == JsonValueKind.String)
            return id.GetString() ?? "unknown";
        if (el.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            return name.GetString() ?? "unknown";
        if (el.TryGetProperty("Name", out name) && name.ValueKind == JsonValueKind.String)
            return name.GetString() ?? "unknown";
        return "unknown";
    }

    private static void ThrowNavigationErrors(List<string> errors)
    {
        var dict = new Dictionary<string, string>();
        for (var i = 0; i < errors.Count; i++)
            dict[$"journey.navigation.{i}"] = errors[i];

        throw new APIErrorsException(dict);
    }
}
