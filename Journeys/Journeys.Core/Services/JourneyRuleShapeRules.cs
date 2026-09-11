using System.Text.Json;

namespace Journeys.Core.Services;

/// <summary>
/// Shared journey rule JSON shape checks (single-rule slots must be objects, not arrays or strings).
/// </summary>
public static class JourneyRuleShapeRules
{
    public const string ViolationCode = "JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT";

    public const string StringEncodedViolationCode = "JOURNEY_SHAPE_RULE_JSON_STRING_ENCODED";

    private static readonly string[] SingleRuleSlotNames =
    [
        "navConstraint", "NavConstraint",
        "isApplicableConstraint", "IsApplicableConstraint",
        "temporalConstraint", "TemporalConstraint"
    ];

    public static void ValidateSingleRuleElement(
        JsonElement element,
        string path,
        string slotName,
        ICollection<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Array)
            errors.Add(FormatViolation(path, slotName));
        else if (element.ValueKind == JsonValueKind.String)
            errors.Add(FormatStringEncodedViolation(path, slotName));
        else if (element.ValueKind == JsonValueKind.Object)
            WalkRuleTreeShape(element, path, errors);
    }

    public static void WalkRuleTreeShape(JsonElement rule, string path, ICollection<string> errors)
    {
        if (rule.ValueKind == JsonValueKind.Array)
        {
            errors.Add(FormatViolation(path, "rule"));
            return;
        }

        if (rule.ValueKind == JsonValueKind.String)
        {
            errors.Add(FormatStringEncodedViolation(path, "rule"));
            return;
        }

        if (rule.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in rule.EnumerateObject())
        {
            if (IsSingleRuleSlot(prop.Name))
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    errors.Add(FormatViolation($"{path}/{prop.Name}", prop.Name));
                else if (prop.Value.ValueKind == JsonValueKind.String)
                    errors.Add(FormatStringEncodedViolation($"{path}/{prop.Name}", prop.Name));
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                    WalkRuleTreeShape(prop.Value, $"{path}/{prop.Name}", errors);
            }
            else if (IsChildrenProperty(prop.Name) && prop.Value.ValueKind == JsonValueKind.Array)
            {
                var i = 0;
                foreach (var child in prop.Value.EnumerateArray())
                {
                    if (child.ValueKind == JsonValueKind.Object)
                        WalkRuleTreeShape(child, $"{path}/{prop.Name}[{i}]", errors);
                    i++;
                }
            }
        }
    }

    public static void WalkNavigationShape(JsonElement navigation, string path, ICollection<string> errors)
    {
        if (navigation.ValueKind != JsonValueKind.Object)
            return;

        foreach (var channel in navigation.EnumerateObject())
        {
            if (channel.Value.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var prop in channel.Value.EnumerateObject())
            {
                if (!IsSingleRuleSlot(prop.Name))
                    continue;

                ValidateSingleRuleElement(prop.Value, $"{path}/{channel.Name}/{prop.Name}", prop.Name, errors);
            }
        }
    }

    public static string FormatViolation(string path, string slotName) =>
        $"[violation={ViolationCode}] path={path} — {slotName} must be a single rule object with Kind " +
        "(e.g. AndRule, SimpleRule), not a JSON array. Wrap multiple rules in AndRule.Children " +
        "or split into separate RuleSet entries in journey.rules[].";

    public static string FormatStringEncodedViolation(string path, string slotName) =>
        $"[violation={StringEncodedViolationCode}] path={path} — {slotName} must be an embedded JSON object with Kind, " +
        "not a JSON string. Do not stringify rule JSON; embed { \"Kind\": \"...\", ... } directly.";

    private static bool IsSingleRuleSlot(string name) =>
        SingleRuleSlotNames.Any(n => string.Equals(n, name, StringComparison.Ordinal));

    private static bool IsChildrenProperty(string name) =>
        string.Equals(name, "children", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Children", StringComparison.OrdinalIgnoreCase);
}
