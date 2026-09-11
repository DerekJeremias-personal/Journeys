using System.Linq;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Filters tools from Backend MCP for the campaign agent. By default uses a model-focused allowlist
/// (see BackEnd.Web Mcp/BACKEND-MCP-Contract.md). When <see cref="ExposeFullBackendMcpToolSurface"/> is used,
/// all tools from <c>ListTools</c> are passed through.
/// </summary>
internal static class CampaignAgentBackendMcp
{
    // Names must match whatever the Backend MCP server advertises in ListTools (PascalCase or snake_case).
    private static readonly HashSet<string> AllowedToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ListModels",
        "list_models",
        "GetAllModels",
        "get_all_models",
        "GetModel",
        "get_model",
        "GetManyModels",
        "get_many_models",
        "GetModelAttributesForRules",
        "get_model_attributes_for_rules",
        "BuildTaxonomicRule",
        "build_taxonomic_rule",
        "ListExampleModels",
        "list_example_models",
        "GetExampleModel",
        "get_example_model",
        "BuildEventWrapper",
        "build_event_wrapper",
        "SaveModel",
        "save_model",
        "DeleteModel",
        "delete_model"
    };

    /// <summary>
    /// Filters tools returned from Backend MCP. When <paramref name="exposeFullBackendMcpToolSurface"/> is true,
    /// returns every tool (BackEnd.Web remains the authority for what is registered). Otherwise applies the
    /// model-catalog allowlist and optional SaveModel / DeleteModel gates.
    /// </summary>
    public static IReadOnlyList<AITool> FilterTools(
        IEnumerable<AITool> tools,
        bool allowSaveModel,
        bool allowDeleteModel,
        bool exposeFullBackendMcpToolSurface)
    {
        if (exposeFullBackendMcpToolSurface)
            return tools.ToList();

        return tools
            .Where(t => AllowedToolNames.Contains(t.Name))
            .Where(t => allowSaveModel || !IsSaveModelToolName(t.Name))
            .Where(t => allowDeleteModel || !IsDeleteModelToolName(t.Name))
            .ToList();
    }

    public static bool IsSaveModelToolName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (string.Equals(name, "SaveModel", StringComparison.OrdinalIgnoreCase))
            return true;
        // Common MCP/JSON name for the same tool
        if (string.Equals(name, "save_model", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public static bool IsDeleteModelToolName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (string.Equals(name, "DeleteModel", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(name, "delete_model", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>Names commonly used by Backend MCP for deleting a dynamic model entity (best-effort discovery).</summary>
    /// <remarks>Do not include <c>DeleteModel</c>/<c>delete_model</c> here; those delete model definitions, not entity rows.</remarks>
    private static readonly string[] DeleteEntityToolNameCandidates =
    {
        "DeleteEntity",
        "delete_entity",
        "RemoveModel",
        "remove_model",
        "DeleteDynamicEntity",
        "delete_dynamic_entity",
    };

    private static readonly string[] DeleteModelToolNameCandidates =
    {
        "DeleteModel",
        "delete_model",
    };

    private static readonly string[] ListModelsToolNameCandidates =
    {
        "ListModels",
        "list_models",
        "GetAllModels",
        "get_all_models",
        "ListExampleModels",
        "list_example_models",
    };

    private static readonly string[] UpsertCampaignToolNameCandidates =
    {
        "UpsertCampaign",
        "upsert_campaign",
    };

    private static readonly string[] GetModelToolNameCandidates =
    {
        "GetModel",
        "get_model",
    };

    /// <summary>Returns the server-advertised tool name (preserves casing) for the first matching candidate.</summary>
    public static string? TryResolveAdvertisedToolName(IEnumerable<AITool> tools, IReadOnlyList<string> candidatesOrdered)
    {
        foreach (var wanted in candidatesOrdered)
        {
            foreach (var t in tools)
            {
                if (string.Equals(t.Name, wanted, StringComparison.OrdinalIgnoreCase))
                    return t.Name;
            }
        }

        return null;
    }

    public static string? TryResolveDeleteEntityToolName(IEnumerable<AITool> tools) =>
        TryResolveAdvertisedToolName(tools, DeleteEntityToolNameCandidates);

    public static string? TryResolveDeleteModelToolName(IEnumerable<AITool> tools) =>
        TryResolveAdvertisedToolName(tools, DeleteModelToolNameCandidates);

    public static string? TryResolveListModelsToolName(IEnumerable<AITool> tools) =>
        TryResolveAdvertisedToolName(tools, ListModelsToolNameCandidates);

    /// <summary>
    /// Replaces catalog tools (ListModels/GetAllModels/list_models/get_all_models) with digest wrappers.
    /// Non-catalog tools are returned unchanged. When <paramref name="enabled"/> is false, returns the input as-is.
    /// Already-wrapped tools are not double-wrapped.
    /// </summary>
    public static IReadOnlyList<AITool> WrapCatalogDigest(
        IEnumerable<AITool> tools,
        string? forcedTag,
        bool enabled,
        ILogger? logger)
    {
        var list = tools.ToList();
        if (!enabled)
            return list;

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is AIFunction fn and not ModelCatalogDigestAIFunction && IsListModelsToolName(fn.Name))
                list[i] = new ModelCatalogDigestAIFunction(fn, forcedTag, enabled: true, logger);
        }

        return list;
    }

    private static bool IsListModelsToolName(string? name) =>
        !string.IsNullOrEmpty(name)
        && ListModelsToolNameCandidates.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Replaces the upsert_campaign tool with a mutation-digest wrapper. Non-matching tools are returned
    /// unchanged. When <paramref name="enabled"/> is false, returns the input as-is. Already-wrapped tools
    /// are not double-wrapped.
    /// </summary>
    public static IReadOnlyList<AITool> WrapMutationDigest(
        IEnumerable<AITool> tools,
        bool enabled,
        ILogger? logger)
    {
        var list = tools.ToList();
        if (!enabled)
            return list;

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is AIFunction fn and not CampaignMutationDigestAIFunction && IsUpsertCampaignToolName(fn.Name))
                list[i] = new CampaignMutationDigestAIFunction(fn, enabled: true, logger);
        }

        return list;
    }

    private static bool IsUpsertCampaignToolName(string? name) =>
        !string.IsNullOrEmpty(name)
        && UpsertCampaignToolNameCandidates.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Replaces get_model with a read-digest wrapper. Non-matching tools are returned unchanged.
    /// When <paramref name="enabled"/> is false, returns the input as-is. Already-wrapped tools are not double-wrapped.
    /// </summary>
    public static IReadOnlyList<AITool> WrapModelReadDigest(
        IEnumerable<AITool> tools,
        bool enabled,
        ILogger? logger,
        Func<CampaignWorkflowState>? getState = null)
    {
        var list = tools.ToList();
        if (!enabled && getState is null)
            return list;

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is AIFunction fn and not ModelReadDigestAIFunction && IsGetModelToolName(fn.Name))
                list[i] = new ModelReadDigestAIFunction(fn, enabled, logger, getState);
        }

        return list;
    }

    private static bool IsGetModelToolName(string? name) =>
        !string.IsNullOrEmpty(name)
        && GetModelToolNameCandidates.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
}
