using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Fetches a single model via Backend MCP get_model (raw JSON, bypassing digest wrapper).
/// </summary>
internal static class EventModelSingleModelFetcher
{
    private static readonly string[] GetModelToolCandidates = { "get_model", "GetModel" };

    public static async Task<string?> FetchAsync(
        McpClient mcp,
        IEnumerable<AITool> tools,
        string tenantId,
        string modelId,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var toolNamesToTry = BuildGetModelToolNamesToTry(tools);
        if (toolNamesToTry.Count == 0)
        {
            logger?.LogWarning("EventModelSingleModelFetcher: no get_model tool advertised.");
            return null;
        }

        Exception? lastFailure = null;
        foreach (var toolName in toolNamesToTry)
        {
            try
            {
                var result = await mcp
                    .CallToolAsync(
                        toolName,
                        new Dictionary<string, object?> { ["tenantId"] = tenantId, ["modelId"] = modelId },
                        progress: null,
                        options: null,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsError == true)
                {
                    logger?.LogWarning(
                        "EventModelSingleModelFetcher: get_model returned error for {ModelId} via '{ToolName}'.",
                        modelId,
                        toolName);
                    continue;
                }

                if (!TryGetJson(result, out var root))
                {
                    logger?.LogWarning(
                        "EventModelSingleModelFetcher: get_model returned no parseable JSON for {ModelId} via '{ToolName}'.",
                        modelId,
                        toolName);
                    continue;
                }

                if (root.ValueKind == JsonValueKind.String)
                    return root.GetString();

                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("text", out var textProp)
                    && textProp.ValueKind == JsonValueKind.String)
                    return textProp.GetString();

                return root.GetRawText();
            }
            catch (Exception ex) when (IsUnknownToolException(ex))
            {
                lastFailure = ex;
                logger?.LogWarning(ex, "EventModelSingleModelFetcher: fetch via '{ToolName}' failed for {ModelId}.", toolName, modelId);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "EventModelSingleModelFetcher: fetch failed for {ModelId}.", modelId);
                return null;
            }
        }

        if (lastFailure is not null)
            logger?.LogWarning(lastFailure, "EventModelSingleModelFetcher: all get_model candidates failed for {ModelId}.", modelId);

        return null;
    }

    internal static List<string> BuildGetModelToolNamesToTry(IEnumerable<AITool> tools)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in GetModelToolCandidates)
        {
            if (seen.Add(candidate))
                names.Add(candidate);
        }

        foreach (var candidate in GetModelToolCandidates)
        {
            var resolved = CampaignAgentBackendMcp.TryResolveAdvertisedToolName(tools, [candidate]);
            if (resolved != null && seen.Add(resolved))
                names.Add(resolved);
        }

        return names;
    }

    internal static bool TryExtractModelJson(string payload, out string? modelJson)
    {
        modelJson = null;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("text", out var textProp)
                && textProp.ValueKind == JsonValueKind.String)
            {
                modelJson = textProp.GetString();
                return !string.IsNullOrWhiteSpace(modelJson);
            }

            modelJson = root.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsUnknownToolException(Exception ex) =>
        ex.Message.Contains("Unknown tool", StringComparison.OrdinalIgnoreCase)
        || (ex.InnerException?.Message.Contains("Unknown tool", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool TryGetJson(CallToolResult result, out JsonElement root)
    {
        root = default;
        if (result.StructuredContent is JsonElement je)
        {
            root = je;
            return true;
        }

        if (result.Content is { Count: > 0 } contents)
        {
            foreach (var c in contents)
            {
                if (c is TextContentBlock tcb && !string.IsNullOrWhiteSpace(tcb.Text))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(tcb.Text);
                        root = doc.RootElement.Clone();
                        return true;
                    }
                    catch (JsonException)
                    {
                        /* try next */
                    }
                }
            }
        }

        return false;
    }
}
