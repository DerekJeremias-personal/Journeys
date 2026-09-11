using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Pairs streamed <see cref="FunctionCallContent"/> with <see cref="FunctionResultContent"/> by call id.
/// </summary>
public static class CampaignWorkflowStreamExtractor
{
    public static IReadOnlyList<(string ToolName, string ResultJson)> ExtractFromStreamedUpdates(
        IReadOnlyList<ChatResponseUpdate> streamedUpdates)
    {
        var callIdToName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var u in streamedUpdates)
        {
            foreach (var c in u.Contents)
            {
                if (c is FunctionCallContent fc && !string.IsNullOrEmpty(fc.CallId))
                    callIdToName[fc.CallId] = fc.Name ?? string.Empty;
            }
        }

        var results = new List<(string ToolName, string ResultJson)>();
        foreach (var u in streamedUpdates)
        {
            foreach (var c in u.Contents)
            {
                if (c is not FunctionResultContent fr || string.IsNullOrEmpty(fr.CallId))
                    continue;
                if (!callIdToName.TryGetValue(fr.CallId, out var name))
                    name = string.Empty;

                // MCP/MEAI results arrive either as a raw string, a double-encoded JSON string, or a
                // text-content envelope ({"$type":"text","text":"<json>"}). SerializeResultToJson keeps
                // string/JsonElement payloads as raw JSON; Unwrap then strips the envelope/encoding so the
                // engine's object-shaped gates (event-model contract, PAT manifest) can parse the payload
                // instead of stalling the phase.
                var json = BackendToolResultEnricher.SerializeResultToJson(fr.Result) ?? "{}";
                json = ToolResultJsonNormalizer.Unwrap(json) ?? json;

                results.Add((name, json));
            }
        }

        return results;
    }
}
