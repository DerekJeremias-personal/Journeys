using CampaignContextAudit.Models;
using CampaignContextAudit.Transcript;

namespace CampaignContextAudit.Analysis;

public sealed record ToolEvent(long Sequence, string ToolName, string CallId, int ResultChars, bool IsDigested = false);

public static class ToolCallTimeline
{
    // Read-only discovery tools whose payloads tend to dominate the budget.
    public static readonly HashSet<string> DiscoveryTools =
        new(StringComparer.OrdinalIgnoreCase) { "get_all_models", "list_models", "get_model_attributes_for_rules" };

    public static readonly HashSet<string> ModelMutationTools =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "save_model",
            "upsert_campaign",
            "upsert_point_account_type",
            "validate_campaign"
        };

    public static IReadOnlyList<ToolEvent> Build(IReadOnlyList<AgentMessageDoc> chatRows)
    {
        var resultCharsByCallId = new Dictionary<string, int>(StringComparer.Ordinal);
        var digestedByCallId = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var m in chatRows)
        {
            if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.ToolCallId))
            {
                resultCharsByCallId[m.ToolCallId] = m.ToolResultJson?.Length ?? 0;
                digestedByCallId[m.ToolCallId] = LooksDigested(m.ToolResultJson);
            }

            foreach (var fr in MeaiEnvelope.ExtractFunctionResults(m.Content))
            {
                if (string.IsNullOrWhiteSpace(fr.CallId))
                    continue;
                resultCharsByCallId[fr.CallId] = fr.ResultRaw?.Length ?? 0;
                digestedByCallId[fr.CallId] = LooksDigested(fr.ResultRaw);
            }
        }

        var events = new List<ToolEvent>();
        foreach (var m in chatRows)
        {
            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var fc in MeaiEnvelope.ExtractFunctionCalls(m.Content))
            {
                resultCharsByCallId.TryGetValue(fc.CallId, out var rc);
                digestedByCallId.TryGetValue(fc.CallId, out var digested);
                events.Add(new ToolEvent(m.Sequence, fc.Name, fc.CallId, rc, digested));
            }
        }
        return events.OrderBy(e => e.Sequence).ToList();
    }

    private static bool LooksDigested(string? resultJson) =>
        !string.IsNullOrWhiteSpace(resultJson)
        && resultJson.Contains("Model digested for context efficiency", StringComparison.Ordinal);
}
