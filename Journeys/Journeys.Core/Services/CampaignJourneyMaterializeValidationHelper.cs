using System.Text.Json;
using System.Text.RegularExpressions;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Services;

internal static class CampaignJourneyMaterializeValidationHelper
{
    private static readonly Regex UnrecognizedTypeDiscriminatorRegex = new(
        "unrecognized type discriminator id '([^']+)'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsJsonMaterializationFailure(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is JourneyMaterializePathException)
                return true;
            if (e is JsonException or NotSupportedException)
                return true;
            if (e is InvalidOperationException ioe &&
                string.Equals(ioe.Source, "System.Text.Json", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsArrayObjectShapeFailure(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is JsonException je && je.Message.Contains("expected a JSON object", StringComparison.OrdinalIgnoreCase))
            {
                if (je.Message.Contains("received String", StringComparison.OrdinalIgnoreCase))
                    continue;

                return true;
            }

            if (ContainsArrayObjectMismatch(e.Message))
                return true;
        }

        return false;
    }

    public static bool IsStringEncodedShapeFailure(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is JsonException je && je.Message.Contains("received String", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string BuildMaterializeMessage(Exception ex)
    {
        const string hint =
            " Check journey JSON: rule and outcome payloads must match model shapes (e.g. TagOutcome dates use EffectiveStartDateProvider / EffectiveEndDateProvider with ConstantValueProvider, not EffectiveStartDate as a nested object).";

        if (ex is JourneyMaterializePathException pathEx)
        {
            if (pathEx.Message.Contains(JourneyRuleShapeRules.ViolationCode, StringComparison.Ordinal)
                || pathEx.Message.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, StringComparison.Ordinal))
                return pathEx.Message;

            return pathEx.Message + hint;
        }

        if (IsStringEncodedShapeFailure(ex))
        {
            return $"[violation={JourneyRuleShapeRules.StringEncodedViolationCode}] journey.materialize — " +
                   "ruleJsonElement must be an embedded JSON object with Kind, not a JSON string. " +
                   "Do not stringify rule JSON; embed { \"Kind\": \"...\", ... } directly.";
        }

        var innerMessage = ex.InnerException?.Message ?? ex.Message;
        if (ContainsArrayObjectMismatch(innerMessage) || ContainsArrayObjectMismatch(ex.Message))
        {
            return $"[violation={JourneyRuleShapeRules.ViolationCode}] journey.materialize — " +
                   "ruleJsonElement must be a single rule object with Kind, not a JSON array. " +
                   "Wrap multiple rules in AndRule.Children or split into separate RuleSet entries in journey.rules[].";
        }

        var match = UnrecognizedTypeDiscriminatorRegex.Match(innerMessage);
        if (match.Success)
        {
            var typeDisc = match.Groups[1].Value;
            return $"[violation=TIER_A_UNKNOWN_TYPE_DISCRIMINATOR] journey.materialize $type={typeDisc} — call GetRulesEngineContractSummary for allowed typeDiscriminatorCatalog.";
        }

        var core = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
        return core + hint;
    }

    public static APIErrorsException ToMaterializeException(Exception ex) =>
        new(new Dictionary<string, string>
        {
            { "journey.materialize", BuildMaterializeMessage(ex) }
        });

    private static bool ContainsArrayObjectMismatch(string message) =>
        message.Contains("type 'Object'", StringComparison.Ordinal)
        && message.Contains("type 'Array'", StringComparison.Ordinal);
}
