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
/// Pre-materialization validation of journey polymorphic metadata (<c>Kind</c> and <c>$type</c> discriminators).
/// </summary>
public static class CampaignJourneyPolymorphicMetadataValidator
{
    private static readonly string[] RuleRecursionProps =
    [
        "children", "Children",
        "navConstraint", "NavConstraint",
        "isApplicableConstraint", "IsApplicableConstraint",
        "temporalConstraint", "TemporalConstraint"
    ];

    public static void ValidateCampaignJson(JsonElement campaignRoot)
    {
        if (!TryGetJourneyObject(campaignRoot, out var journey))
            return;

        var errors = new List<string>();
        WalkJourneyJson(journey, errors);

        if (errors.Count == 0)
            return;

        ThrowValidationErrors(errors);
    }

    public static void ValidateCampaign(Campaign campaign)
    {
        if (campaign?.Journey == null)
            return;

        var errors = new List<string>();
        WalkJourneyNode(campaign.Journey, errors);

        if (errors.Count == 0)
            return;

        ThrowValidationErrors(errors);
    }

    public static void ValidateJourneyDto(JourneyDto? journey)
    {
        if (journey == null)
            return;

        var errors = new List<string>();
        WalkJourneyDto(journey, errors);

        if (errors.Count == 0)
            return;

        ThrowValidationErrors(errors);
    }

    private static void WalkJourneyJson(JsonElement node, List<string> errors)
    {
        var nodeId = GetNodeId(node);
        var ctx = new ValidationContext(nodeId);

        if (TryGetProperty(node, "navigation", out var navigation) || TryGetProperty(node, "Navigation", out navigation))
            WalkNavigationJson(navigation, ctx, errors);

        if (TryGetProperty(node, "rules", out var rules) || TryGetProperty(node, "Rules", out rules))
            WalkRulesArrayJson(rules, ctx, errors);

        if (!TryGetProperty(node, "children", out var children) && !TryGetProperty(node, "Children", out children))
            return;

        if (children.ValueKind != JsonValueKind.Array)
            return;

        foreach (var child in children.EnumerateArray())
        {
            if (child.ValueKind == JsonValueKind.Object)
                WalkJourneyJson(child, errors);
        }
    }

    private static void WalkJourneyNode(JourneyNode node, List<string> errors)
    {
        var ctx = new ValidationContext(node.Id ?? "unknown");

        if (HasJsonElement(node.Navigation))
            WalkNavigationJson(node.Navigation!.Value, ctx, errors);

        if (node.Rules is { Count: > 0 })
        {
            foreach (var ruleSet in node.Rules)
            {
                if (ruleSet == null)
                    continue;

                var ruleSetCtx = ctx with { RuleSetName = RuleSetLabel(ruleSet) };

                if (HasJsonElement(ruleSet.RuleJsonElement))
                    WalkRuleJson(ruleSet.RuleJsonElement!.Value, "RuleTree", ruleSetCtx, errors);

                if (HasJsonElement(ruleSet.OutcomesJsonElement))
                    WalkOutcomesJson(ruleSet.OutcomesJsonElement!.Value, ruleSetCtx, errors);
            }
        }

        if (node.Children == null)
            return;

        foreach (var child in node.Children)
        {
            if (child != null)
                WalkJourneyNode(child, errors);
        }
    }

    private static void WalkJourneyDto(JourneyDto node, List<string> errors)
    {
        var ctx = new ValidationContext(node.Id ?? "unknown");

        if (HasJsonElement(node.Navigation))
            WalkNavigationJson(node.Navigation!.Value, ctx, errors);

        if (node.Rules is { Count: > 0 })
        {
            foreach (var ruleSet in node.Rules)
            {
                if (ruleSet == null)
                    continue;

                var ruleSetCtx = ctx with { RuleSetName = RuleSetLabel(ruleSet) };

                if (HasJsonElement(ruleSet.RuleJsonElement))
                    WalkRuleJson(ruleSet.RuleJsonElement!.Value, "RuleTree", ruleSetCtx, errors);

                if (HasJsonElement(ruleSet.OutcomesJsonElement))
                    WalkOutcomesJson(ruleSet.OutcomesJsonElement!.Value, ruleSetCtx, errors);
            }
        }

        if (node.Children == null)
            return;

        foreach (var child in node.Children)
        {
            if (child != null)
                WalkJourneyDto(child, errors);
        }
    }

    private static void WalkRulesArrayJson(JsonElement rules, ValidationContext ctx, List<string> errors)
    {
        if (rules.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in rules.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var ruleSetCtx = ctx with { RuleSetName = GetRuleSetName(entry) };

            if (TryGetProperty(entry, "ruleJsonElement", out var ruleJson) || TryGetProperty(entry, "RuleJsonElement", out ruleJson))
            {
                if (ruleJson.ValueKind == JsonValueKind.Object)
                    WalkRuleJson(ruleJson, "RuleTree", ruleSetCtx, errors);
                else if (ruleJson.ValueKind == JsonValueKind.Array)
                {
                    var shapeErrors = new List<string>();
                    JourneyRuleShapeRules.ValidateSingleRuleElement(
                        ruleJson, $"journey/rules/ruleJsonElement", "ruleJsonElement", shapeErrors);
                    errors.AddRange(shapeErrors);
                }
            }

            if (TryGetProperty(entry, "outcomesJsonElement", out var outcomesJson) || TryGetProperty(entry, "OutcomesJsonElement", out outcomesJson))
                WalkOutcomesJson(outcomesJson, ruleSetCtx, errors);
        }
    }

    private static void WalkNavigationJson(JsonElement navigation, ValidationContext ctx, List<string> errors)
    {
        if (navigation.ValueKind != JsonValueKind.Object)
            return;

        foreach (var channel in navigation.EnumerateObject())
        {
            if (channel.Value.ValueKind != JsonValueKind.Object)
                continue;

            ValidateNavigationCriteriaObject(channel.Value, ctx, errors);
        }
    }

    private static void ValidateNavigationCriteriaObject(JsonElement navCriteria, ValidationContext ctx, List<string> errors)
    {
        if (TryGetTypeDiscriminator(navCriteria, out var typeDisc))
        {
            if (!RulesEnginePolymorphicCatalog.IsAllowed(RulesEnginePolymorphicCatalog.NavigationCriteriaTypes, typeDisc))
            {
                errors.Add(TierAError(
                    "TIER_A_UNKNOWN_TYPE_DISCRIMINATOR",
                    ctx,
                    $"field=navigation $type={typeDisc} allowed={FormatAllowed(RulesEnginePolymorphicCatalog.NavigationCriteriaTypes)}"));
            }
        }
        else
        {
            errors.Add(TierAError(
                "TIER_A_MISSING_TYPE_DISCRIMINATOR",
                ctx,
                "field=navigation — navigation criteria object requires $type."));
        }

        foreach (var prop in navCriteria.EnumerateObject())
        {
            if (IsRuleRecursionProperty(prop.Name))
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                    WalkRuleJson(prop.Value, "RuleTree", ctx, errors);
            }
            else if (IsOutcomesProperty(prop.Name) && prop.Value.ValueKind == JsonValueKind.Array)
            {
                WalkOutcomesJson(prop.Value, ctx, errors);
            }
            else
            {
                WalkMappedProperty(prop.Name, prop.Value, "navigation", ctx, errors);
            }
        }
    }

    private static void WalkRuleJson(JsonElement rule, string rulePath, ValidationContext ctx, List<string> errors)
    {
        if (rule.ValueKind != JsonValueKind.Object)
            return;

        ValidateRuleKind(rule, rulePath, ctx, errors);

        var kindSuffix = TryGetKind(rule, out var kind) ? kind! : "Unknown";
        var childRulePath = $"{rulePath}/{kindSuffix}";

        foreach (var prop in rule.EnumerateObject())
        {
            if (IsRuleRecursionProperty(prop.Name))
            {
                if (prop.Name.Equals("children", StringComparison.OrdinalIgnoreCase)
                    || prop.Name.Equals("Children", StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in prop.Value.EnumerateArray())
                        {
                            if (child.ValueKind == JsonValueKind.Object)
                                WalkRuleJson(child, childRulePath, ctx, errors);
                        }
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    WalkRuleJson(prop.Value, childRulePath, ctx, errors);
                }
            }
            else
            {
                WalkMappedProperty(prop.Name, prop.Value, childRulePath, ctx, errors);
            }
        }
    }

    private static readonly HashSet<string> PointOutcomeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "DepositPointsOutcome", "SpendPointsOutcome", "ExpirePointsOutcome"
    };

    private static void WalkOutcomesJson(JsonElement outcomes, ValidationContext ctx, List<string> errors)
    {
        if (outcomes.ValueKind != JsonValueKind.Array)
            return;

        var outcomeIndex = 0;
        foreach (var outcome in outcomes.EnumerateArray())
        {
            if (outcome.ValueKind != JsonValueKind.Object)
                continue;

            ValidateOutcomeKind(outcome, ctx, errors);
            ValidatePointOutcomePatJson(outcome, ctx, outcomeIndex, errors);

            foreach (var prop in outcome.EnumerateObject())
                WalkMappedProperty(prop.Name, prop.Value, "Outcome", ctx, errors, inOutcomeContext: true);

            outcomeIndex++;
        }
    }

    private static void ValidatePointOutcomePatJson(
        JsonElement outcome,
        ValidationContext ctx,
        int outcomeIndex,
        List<string> errors)
    {
        if (!TryGetKind(outcome, out var kind) || string.IsNullOrWhiteSpace(kind) || !PointOutcomeKinds.Contains(kind))
            return;

        var hasAlias = HasNonEmptyStringProperty(outcome, "pointAccountTypeId", "PointAccountTypeId");
        var patIds = GetStringArrayProperty(outcome, "affectedPointAccountTypeIds", "AffectedPointAccountTypeIds");
        var hasPatIds = patIds.Any(id => !string.IsNullOrWhiteSpace(id));

        if (hasAlias && !hasPatIds)
        {
            errors.Add(TierAError(
                "TIER_A_OUTCOME_PAT_ALIAS_MISUSED",
                ctx,
                $"outcomeIndex={outcomeIndex} kind={kind} field=pointAccountTypeId — use affectedPointAccountTypeIds[] (array of PAT ids from PointAccountManifest), not pointAccountTypeId on outcomes."));
            return;
        }

        if (!hasPatIds)
        {
            var label = kind switch
            {
                "SpendPointsOutcome" => "SpendPoints",
                "ExpirePointsOutcome" => "ExpirePoints",
                _ => "DepositPoints"
            };
            errors.Add(TierAError(
                MissingPatViolationCode(kind),
                ctx,
                $"outcomeIndex={outcomeIndex} kind={kind} field=AffectedPointAccountTypeIds Rule set outcome [{label}] #{outcomeIndex + 1}: at least one point account type id is required."));
        }
    }

    private static string MissingPatViolationCode(string kind) => kind switch
    {
        "SpendPointsOutcome" => "TIER_A_SPEND_MISSING_AFFECTED_PAT",
        "ExpirePointsOutcome" => "TIER_A_EXPIRE_MISSING_AFFECTED_PAT",
        _ => "TIER_A_DEPOSIT_MISSING_AFFECTED_PAT"
    };

    private static bool HasNonEmptyStringProperty(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(obj, name, out var value) || value.ValueKind != JsonValueKind.String)
                continue;
            if (!string.IsNullOrWhiteSpace(value.GetString()))
                return true;
        }

        return false;
    }

    private static List<string> GetStringArrayProperty(JsonElement obj, params string[] names)
    {
        var result = new List<string>();
        foreach (var name in names)
        {
            if (!TryGetProperty(obj, name, out var value) || value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    result.Add(item.GetString() ?? string.Empty);
            }

            return result;
        }

        return result;
    }

    private static void WalkMappedProperty(
        string propName,
        JsonElement value,
        string rulePath,
        ValidationContext ctx,
        List<string> errors,
        bool inOutcomeContext = false)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;

        if (IsHistoricalValueProviderProperty(propName))
        {
            ValidateTypeDiscriminator(
                value,
                propName,
                RulesEnginePolymorphicCatalog.HistoricalProviderKinds,
                rulePath,
                ctx,
                errors);
            WalkNestedProviders(value, rulePath, ctx, errors);
            return;
        }

        if (IsEvaluatorProperty(propName))
        {
            ValidateTypeDiscriminator(
                value,
                propName,
                RulesEnginePolymorphicCatalog.EvaluatorTypes,
                rulePath,
                ctx,
                errors);
            return;
        }

        if (IsValueProviderProperty(propName))
        {
            ValidateTypeDiscriminator(
                value,
                propName,
                RulesEnginePolymorphicCatalog.ValueProviderKinds,
                rulePath,
                ctx,
                errors);
            WalkNestedProviders(value, rulePath, ctx, errors);
        }
    }

    private static void WalkNestedProviders(JsonElement providerObj, string rulePath, ValidationContext ctx, List<string> errors)
    {
        foreach (var prop in providerObj.EnumerateObject())
            WalkMappedProperty(prop.Name, prop.Value, rulePath, ctx, errors);
    }

    private static void ValidateRuleKind(JsonElement rule, string rulePath, ValidationContext ctx, List<string> errors)
    {
        if (!TryGetKind(rule, out var kind) || string.IsNullOrWhiteSpace(kind))
        {
            errors.Add(TierAError(
                "TIER_A_MISSING_RULE_KIND",
                ctx,
                $"rulePath={rulePath} — rule object requires Kind."));
            return;
        }

        if (!RulesEnginePolymorphicCatalog.IsAllowed(RulesEnginePolymorphicCatalog.RuleKinds, kind))
        {
            errors.Add(TierAError(
                "TIER_A_UNKNOWN_RULE_KIND",
                ctx,
                $"rulePath={rulePath} Kind={kind} allowed={FormatAllowed(RulesEnginePolymorphicCatalog.RuleKinds)}"));
        }
    }

    private static void ValidateOutcomeKind(JsonElement outcome, ValidationContext ctx, List<string> errors)
    {
        if (!TryGetKind(outcome, out var kind) || string.IsNullOrWhiteSpace(kind))
        {
            errors.Add(TierAError(
                "TIER_A_UNKNOWN_OUTCOME_KIND",
                ctx,
                "rulePath=Outcome — outcome object requires Kind."));
            return;
        }

        if (!RulesEnginePolymorphicCatalog.IsAllowed(RulesEnginePolymorphicCatalog.OutcomeKinds, kind))
        {
            errors.Add(TierAError(
                "TIER_A_UNKNOWN_OUTCOME_KIND",
                ctx,
                $"rulePath=Outcome Kind={kind} allowed={FormatAllowed(RulesEnginePolymorphicCatalog.OutcomeKinds)}"));
        }
    }

    private static void ValidateTypeDiscriminator(
        JsonElement obj,
        string fieldName,
        IReadOnlyList<string> allowlist,
        string rulePath,
        ValidationContext ctx,
        List<string> errors)
    {
        if (!TryGetTypeDiscriminator(obj, out var typeDisc) || string.IsNullOrWhiteSpace(typeDisc))
        {
            errors.Add(TierAError(
                "TIER_A_MISSING_TYPE_DISCRIMINATOR",
                ctx,
                $"rulePath={rulePath} field={fieldName} — polymorphic object requires $type."));
            return;
        }

        if (!RulesEnginePolymorphicCatalog.IsAllowed(allowlist, typeDisc))
        {
            errors.Add(TierAError(
                "TIER_A_UNKNOWN_TYPE_DISCRIMINATOR",
                ctx,
                $"rulePath={rulePath} field={fieldName} $type={typeDisc} allowed={FormatAllowed(allowlist)}"));
        }
    }

    private static bool IsRuleRecursionProperty(string name) =>
        RuleRecursionProps.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsOutcomesProperty(string name) =>
        name.Equals("outcomes", StringComparison.OrdinalIgnoreCase);

    private static bool IsHistoricalValueProviderProperty(string name) =>
        name.Equals("historicalValueProvider", StringComparison.OrdinalIgnoreCase);

    private static bool IsEvaluatorProperty(string name) =>
        name.Equals("evaluator", StringComparison.OrdinalIgnoreCase);

    private static bool IsValueProviderProperty(string name) =>
        name.EndsWith("Provider", StringComparison.OrdinalIgnoreCase)
        && !IsHistoricalValueProviderProperty(name);

    private static bool TryGetKind(JsonElement obj, out string? kind)
    {
        kind = null;
        if (TryGetProperty(obj, "kind", out var value) || TryGetProperty(obj, "Kind", out value))
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                kind = value.GetString();
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTypeDiscriminator(JsonElement obj, out string? typeDisc)
    {
        typeDisc = null;
        if (!TryGetProperty(obj, "$type", out var value))
            return false;

        if (value.ValueKind != JsonValueKind.String)
            return false;

        typeDisc = value.GetString();
        return true;
    }

    private static string GetNodeId(JsonElement node)
    {
        if (TryGetProperty(node, "id", out var id) || TryGetProperty(node, "Id", out id))
        {
            if (id.ValueKind == JsonValueKind.String)
                return id.GetString() ?? "unknown";
        }

        return "unknown";
    }

    private static string GetRuleSetName(JsonElement entry)
    {
        if (TryGetProperty(entry, "name", out var name) || TryGetProperty(entry, "Name", out name))
        {
            if (name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString()))
                return name.GetString()!;
        }

        if (TryGetProperty(entry, "id", out var id) || TryGetProperty(entry, "Id", out id))
        {
            if (id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                return id.GetString()!;
        }

        return "unknown";
    }

    private static string RuleSetLabel(RuleSet rs) =>
        !string.IsNullOrWhiteSpace(rs.Name) ? rs.Name : rs.Id ?? "unknown";

    private static string RuleSetLabel(RuleSetDto rs) =>
        !string.IsNullOrWhiteSpace(rs.Name) ? rs.Name : rs.Id ?? "unknown";

    private static bool HasJsonElement(JsonElement? el) =>
        el != null && el.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

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

    private static string FormatAllowed(IReadOnlyList<string> allowlist) =>
        allowlist.Count <= 5
            ? string.Join(",", allowlist)
            : string.Join(",", allowlist.Take(5)) + ",...";

    private static string TierAError(string code, ValidationContext ctx, string details) =>
        $"[violation={code}] ruleSet={ctx.RuleSetName} nodeId={ctx.NodeId} {details}";

    private static void ThrowValidationErrors(List<string> errors)
    {
        var dict = new Dictionary<string, string>();
        for (var i = 0; i < errors.Count; i++)
            dict[$"journey.validation.{i}"] = errors[i];

        throw new APIErrorsException(dict);
    }

    private sealed record ValidationContext(string NodeId, string RuleSetName = "unknown");
}
