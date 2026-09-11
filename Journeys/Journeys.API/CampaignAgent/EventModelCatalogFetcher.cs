using System.Text.Json;
using Backend.Dto.Structures.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Thin I/O boundary that calls the Backend MCP catalog tool directly (raw <see cref="McpClient"/>, bypassing the
/// AIFunction digest wrapper so full model attributes are returned) and deserializes the result into a list of
/// <see cref="ModelDto"/>. A later orchestrator pre-step hands the result to <c>EventModelRanker.Rank(...)</c>.
/// Ranking logic deliberately lives elsewhere; this class only fetches and parses.
/// </summary>
internal static class EventModelCatalogFetcher
{
    // Prefer snake_case names first — raw Backend MCP CallToolAsync must match server-advertised casing.
    private static readonly string[] CatalogToolCandidates =
        { "get_all_models", "GetAllModels", "list_models", "ListModels" };

    // The Backend MCP server emits camelCase JSON while ModelDto uses PascalCase props with no [JsonPropertyName].
    private static readonly JsonSerializerOptions ModelJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Calls the Backend MCP catalog tool and returns the full model list, or null when the catalog
    /// tool is unavailable or the call fails (caller treats null as "fetch failed / degrade to prose").
    /// </summary>
    public static async Task<List<ModelDto>?> FetchAsync(
        McpClient mcp,
        IEnumerable<AITool> tools,
        string tenantId,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var toolNamesToTry = BuildCatalogToolNamesToTry(tools);
        if (toolNamesToTry.Count == 0)
        {
            logger?.LogWarning("EventModelCatalogFetcher: no catalog tool advertised; catalog unavailable.");
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
                        new Dictionary<string, object?> { ["tenantId"] = tenantId },
                        progress: null,
                        options: null,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsError == true)
                {
                    logger?.LogWarning(
                        "EventModelCatalogFetcher: catalog tool '{ToolName}' returned an error result.",
                        toolName);
                    continue;
                }

                if (!TryGetJson(result, out var root))
                {
                    logger?.LogWarning(
                        "EventModelCatalogFetcher: catalog tool '{ToolName}' returned no parseable JSON.",
                        toolName);
                    continue;
                }

                return ParseModels(root);
            }
            catch (Exception ex) when (IsUnknownToolException(ex))
            {
                lastFailure = ex;
                logger?.LogWarning(ex, "EventModelCatalogFetcher: catalog fetch via '{ToolName}' failed.", toolName);
            }
            catch (Exception ex)
            {
                // Fail-open: a catalog fetch failure must degrade to prose, never break the phase.
                logger?.LogWarning(ex, "EventModelCatalogFetcher: catalog fetch via '{ToolName}' failed.", toolName);
                return null;
            }
        }

        if (lastFailure is not null)
            logger?.LogWarning(lastFailure, "EventModelCatalogFetcher: all catalog tool candidates failed.");

        return null;
    }

    internal static List<string> BuildCatalogToolNamesToTry(IEnumerable<AITool> tools)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in CatalogToolCandidates)
        {
            if (seen.Add(candidate))
                names.Add(candidate);
        }

        foreach (var candidate in CatalogToolCandidates)
        {
            var resolved = CampaignAgentBackendMcp.TryResolveAdvertisedToolName(tools, [candidate]);
            if (resolved != null && seen.Add(resolved))
                names.Add(resolved);
        }

        return names;
    }

    private static bool IsUnknownToolException(Exception ex) =>
        ex.Message.Contains("Unknown tool", StringComparison.OrdinalIgnoreCase)
        || (ex.InnerException?.Message.Contains("Unknown tool", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Pure parse helper: locates the model array within the various envelope shapes the MCP server may return and
    /// deserializes each element individually, skipping any malformed model. Never throws and never returns null.
    /// </summary>
    internal static List<ModelDto> ParseModels(JsonElement root)
    {
        var models = new List<ModelDto>();

        // Some MCP transports wrap the payload as { "$type": "text", "text": "<json>" }; unwrap and parse the inner JSON.
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("text", out var textProp)
            && textProp.ValueKind == JsonValueKind.String)
        {
            var inner = textProp.GetString();
            if (!string.IsNullOrWhiteSpace(inner))
            {
                try
                {
                    using var doc = JsonDocument.Parse(inner);
                    return ParseModels(doc.RootElement.Clone());
                }
                catch (JsonException)
                {
                    return models;
                }
            }
        }

        if (!TryGetModelArray(root, out var array))
            return models;

        foreach (var element in array.EnumerateArray())
        {
            try
            {
                var model = JsonSerializer.Deserialize<ModelDto>(element.GetRawText(), ModelJsonOptions);
                if (model is not null)
                    models.Add(model);
            }
            catch (JsonException)
            {
                // One malformed model (e.g. an attribute missing the required "type" discriminator) must not
                // lose the whole catalog. Skip it and keep going.
            }
        }

        return models;
    }

    private static bool TryGetModelArray(JsonElement root, out JsonElement array)
    {
        array = default;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[] { "models", "Items", "data" })
            {
                if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    array = arr;
                    return true;
                }
            }
        }

        return false;
    }

    // Mirrors CampaignAgentDataClearService.TryGetJsonFromCallToolResult: prefer StructuredContent, else the first
    // TextContentBlock whose text parses as JSON.
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
