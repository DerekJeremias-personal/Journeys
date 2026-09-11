using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public static class EventModelContractsAccumulator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static bool TryMergeFromToolResult(CampaignWorkflowState state, string toolName, string toolJson)
    {
        ArgumentNullException.ThrowIfNull(state);
        var merged = false;
        if (IsSaveModelTool(toolName))
            merged = TryMergeResolved(state, toolJson);
        else if (IsDiscoveryModelTool(toolName))
            merged = TryMergeDiscovered(state, toolJson);

        if (IsModelTool(toolName))
            merged = WrapperContractArtifact.TryMergeFromToolJson(state, toolJson) || merged;

        return merged;
    }

    public static bool TryMergeFromToolResult(CampaignWorkflowState state, string toolJson) =>
        TryMergeFromToolResult(state, "save_model", toolJson);

    public static IReadOnlyList<EventProcessingContractDigest> Read(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!string.IsNullOrWhiteSpace(state.Artifacts.EventModelContracts))
            return ParseContractsJson(state.Artifacts.EventModelContracts);

        if (string.IsNullOrWhiteSpace(state.Artifacts.EventModelContract))
            return Array.Empty<EventProcessingContractDigest>();

        return ParseContractsJson(state.Artifacts.EventModelContract);
    }

    public static IReadOnlyList<EventProcessingContractDigest> ReadResolved(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(state.Artifacts.ResolvedEventModelContracts))
            return Array.Empty<EventProcessingContractDigest>();

        return ParseContractsJson(state.Artifacts.ResolvedEventModelContracts);
    }

    public static IReadOnlyList<EventProcessingContractDigest> ReadDiscovered(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(state.Artifacts.DiscoveredEventModelContracts))
            return Array.Empty<EventProcessingContractDigest>();

        return ParseContractsJson(state.Artifacts.DiscoveredEventModelContracts);
    }

    public static bool TryMergeResolved(CampaignWorkflowState state, string toolJson)
    {
        ArgumentNullException.ThrowIfNull(state);
        var normalized = ToolResultJsonNormalizer.Unwrap(toolJson);
        if (!string.IsNullOrWhiteSpace(normalized)
            && TryParseModelDto(normalized, out var model)
            && WrapperModelContractValidator.LooksLikeEventWrapper(model))
            return false;

        if (!EventProcessingContractBuilder.TryBuildFromToolResult(toolJson, out var digestJson))
            return false;

        EventProcessingContractDigest? incoming;
        try
        {
            incoming = JsonSerializer.Deserialize<EventProcessingContractDigest>(digestJson, JsonOpts);
        }
        catch (JsonException)
        {
            return false;
        }

        return TryMergeResolved(state, incoming);
    }

    public static bool TryMergeResolved(CampaignWorkflowState state, EventProcessingContractDigest incoming)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (incoming == null || string.IsNullOrWhiteSpace(incoming.EventModelId))
            return false;

        if (EventModelsReadiness.IsWrapperDigest(incoming))
            return false;

        if (!EventModelSaveGuard.CanMergeToResolved(incoming))
            return false;

        MergeIntoArtifactList(state, incoming, a => a.ResolvedEventModelContracts, (a, json) => a.ResolvedEventModelContracts = json);
        MergeIntoArtifactList(state, incoming, a => a.EventModelContracts, (a, json) => a.EventModelContracts = json);

        return true;
    }

    public static bool TryMergeDiscovered(CampaignWorkflowState state, string toolJson)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!EventProcessingContractBuilder.TryBuildFromToolResult(toolJson, out var digestJson))
            return false;

        EventProcessingContractDigest? incoming;
        try
        {
            incoming = JsonSerializer.Deserialize<EventProcessingContractDigest>(digestJson, JsonOpts);
        }
        catch (JsonException)
        {
            return false;
        }

        if (incoming == null || string.IsNullOrWhiteSpace(incoming.EventModelId))
            return false;

        MergeIntoArtifactList(state, incoming, a => a.DiscoveredEventModelContracts, (a, json) => a.DiscoveredEventModelContracts = json);

        TryPromoteMatchingPendingSpecFromDiscovered(state);
        TryAutoPromoteStrongMatchDefault(state, incoming);
        TryAutoPromoteCompleteDiscoveredContract(state, incoming);

        return true;
    }

    /// <summary>
    /// When get_model returns a complete process-eligible event contract (metadata + wrapper link),
    /// promote to resolved so campaign mutators unlock. Clears a stale UserRequestedNewEventModel
    /// flag when no pending event-model spec is blocking reuse.
    /// </summary>
    private static void TryAutoPromoteCompleteDiscoveredContract(
        CampaignWorkflowState state,
        EventProcessingContractDigest incoming)
    {
        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.EventModelSelection)
            return;

        if (!incoming.IsProcessEventEligible)
            return;
        if (EventModelsReadiness.IsWrapperDigest(incoming))
            return;
        if (string.IsNullOrWhiteSpace(incoming.WrapperModelId))
            return;
        if (string.IsNullOrWhiteSpace(incoming.AccountLink?.SymbolPath))
            return;
        if (incoming.NaturalKey?.Symbols == null || incoming.NaturalKey.Symbols.Count == 0)
            return;

        if (ReadResolved(state).Any(d =>
                string.Equals(d.EventModelId, incoming.EventModelId, StringComparison.OrdinalIgnoreCase)))
            return;

        var pending = PendingEventModelSpecArtifact.Read(state);
        if (pending != null && !string.IsNullOrWhiteSpace(pending.Name))
        {
            if (!string.Equals(incoming.EventModelName, pending.Name, StringComparison.OrdinalIgnoreCase))
                return;
            if (state.Artifacts.UserRequestedNewEventModel)
                return;
        }

        PromoteDiscoveredToResolved(state, incoming.EventModelId);

        if (!ReadResolved(state).Any(d =>
                string.Equals(d.EventModelId, incoming.EventModelId, StringComparison.OrdinalIgnoreCase)))
            return;

        if (state.Artifacts.UserRequestedNewEventModel && pending == null)
            state.Artifacts.UserRequestedNewEventModel = false;

        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.EventModelSelection)
        {
            state.Artifacts.SelectedEventModelId = incoming.EventModelId;
            state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;
        }
    }

    private static void TryAutoPromoteStrongMatchDefault(
        CampaignWorkflowState state,
        EventProcessingContractDigest incoming)
    {
        if (state.Artifacts.UserRequestedNewEventModel)
            return;
        if (!incoming.IsProcessEventEligible)
            return;
        if (EventModelsReadiness.IsWrapperDigest(incoming))
            return;
        if (string.IsNullOrWhiteSpace(incoming.WrapperModelId))
            return;
        if (string.IsNullOrWhiteSpace(incoming.AccountLink?.SymbolPath))
            return;
        if (incoming.NaturalKey?.Symbols == null || incoming.NaturalKey.Symbols.Count == 0)
            return;

        var set = EventModelCandidatesArtifact.Read(state);
        if (set == null || string.IsNullOrWhiteSpace(set.RecommendedDefaultId))
            return;

        var candidate = set.Candidates.FirstOrDefault(c =>
            string.Equals(c.EventModelId, set.RecommendedDefaultId, StringComparison.OrdinalIgnoreCase));
        if (candidate is not { IsStrongMatch: true })
            return;
        if (!string.Equals(incoming.EventModelId, set.RecommendedDefaultId, StringComparison.OrdinalIgnoreCase))
            return;

        PromoteDiscoveredToResolved(state, incoming.EventModelId);

        if (!ReadResolved(state).Any(d =>
                string.Equals(d.EventModelId, incoming.EventModelId, StringComparison.OrdinalIgnoreCase)))
            return;

        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.EventModelSelection)
        {
            state.Artifacts.SelectedEventModelId = incoming.EventModelId;
            state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;
        }
    }

    /// <summary>
    /// When a pending user-stated event model spec is satisfied by an eligible discovered contract
    /// (e.g. existing Review loaded via get_model), promote to resolved so EventModels can advance.
    /// </summary>
    public static bool TryPromoteMatchingPendingSpecFromDiscovered(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var spec = PendingEventModelSpecArtifact.Read(state);
        if (spec == null || string.IsNullOrWhiteSpace(spec.Name))
            return false;

        if (state.Artifacts.UserRequestedNewEventModel)
            return false;

        if (PendingEventModelSpecArtifact.IsSatisfied(state))
            return false;

        var match = ReadDiscovered(state)
            .FirstOrDefault(d =>
                string.Equals(d.EventModelName, spec.Name, StringComparison.OrdinalIgnoreCase)
                && d.IsProcessEventEligible);

        if (match == null)
            return false;

        return TryMergeResolved(state, match);
    }

    public static string SerializeForPrompt(CampaignWorkflowState state)
    {
        var items = Read(state);
        if (items.Count == 0)
            return string.Empty;

        if (items.Count == 1)
            return EventProcessingContractBuilder.SerializeDigest(items[0]);

        return JsonSerializer.Serialize(items, JsonOpts);
    }

    public static string SerializeResolvedForPrompt(CampaignWorkflowState state)
    {
        var items = ReadResolved(state);
        if (items.Count == 0)
            return string.Empty;

        if (items.Count == 1)
            return EventProcessingContractBuilder.SerializeDigest(items[0]);

        return JsonSerializer.Serialize(items, JsonOpts);
    }

    public static string SerializeDiscoveredForPrompt(CampaignWorkflowState state)
    {
        var items = ReadDiscovered(state);
        if (items.Count == 0)
            return string.Empty;

        if (items.Count == 1)
            return EventProcessingContractBuilder.SerializeDigest(items[0]);

        return JsonSerializer.Serialize(items, JsonOpts);
    }

    public static bool ShouldAdvanceToCampaignSetup(CampaignWorkflowState state, string? userMessage)
    {
        ArgumentNullException.ThrowIfNull(state);
        var resolved = ReadResolved(state);
        if (resolved.Count == 0) return false;
        var plannedIds = GetPlannedEventModelIds(state);
        if (plannedIds.Count > 0)
            return plannedIds.All(id => resolved.Any(d => string.Equals(d.EventModelId, id, StringComparison.OrdinalIgnoreCase)));
        return false;
    }

    public static void Clear(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Artifacts.EventModelContracts = null;
        state.Artifacts.EventModelContract = null;
        state.Artifacts.ResolvedEventModelContracts = null;
        state.Artifacts.DiscoveredEventModelContracts = null;
        WrapperContractArtifact.Clear(state);
    }

    public static void PromoteDiscoveredToResolved(CampaignWorkflowState state, string eventModelId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(eventModelId))
            return;

        var discovered = ReadDiscovered(state)
            .FirstOrDefault(d => string.Equals(d.EventModelId, eventModelId, StringComparison.OrdinalIgnoreCase));
        if (discovered == null)
            return;

        if (TryMergeResolved(state, discovered))
            WrapperContractArtifact.TrySeedTrustedReuseFromContract(state, discovered);
    }

    private static bool IsSaveModelTool(string toolName) =>
        string.Equals(toolName, "save_model", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "SaveModel", StringComparison.OrdinalIgnoreCase);

    private static bool IsDiscoveryModelTool(string toolName) =>
        string.Equals(toolName, "get_model", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetModel", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "list_models", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "ListModels", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "get_many_models", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetManyModels", StringComparison.OrdinalIgnoreCase);

    private static bool IsModelTool(string toolName) =>
        IsSaveModelTool(toolName) || IsDiscoveryModelTool(toolName);

    private static void MergeIntoArtifactList(
        CampaignWorkflowState state,
        EventProcessingContractDigest incoming,
        Func<CampaignWorkflowArtifactsDocument, string?> getJson,
        Action<CampaignWorkflowArtifactsDocument, string> setJson)
    {
        var list = ParseContractsJson(getJson(state.Artifacts) ?? string.Empty).ToList();
        var index = list.FindIndex(d =>
            string.Equals(d.EventModelId, incoming.EventModelId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            list[index] = incoming;
        else
            list.Add(incoming);

        setJson(state.Artifacts, JsonSerializer.Serialize(list, JsonOpts));
    }

    private static bool TryParseModelDto(string toolJson, out ModelDto? model)
    {
        model = null;
        if (string.IsNullOrWhiteSpace(toolJson))
            return false;

        try
        {
            model = JsonSerializer.Deserialize<ModelDto>(toolJson, JsonOpts);
            return model != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<EventProcessingContractDigest> ParseContractsJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => DeserializeArray(doc.RootElement),
                JsonValueKind.Object => DeserializeSingle(doc.RootElement),
                _ => Array.Empty<EventProcessingContractDigest>()
            };
        }
        catch (JsonException)
        {
            return Array.Empty<EventProcessingContractDigest>();
        }
    }

    private static IReadOnlyList<EventProcessingContractDigest> DeserializeArray(JsonElement arrayEl)
    {
        var list = new List<EventProcessingContractDigest>();
        foreach (var item in arrayEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var digest = JsonSerializer.Deserialize<EventProcessingContractDigest>(item.GetRawText(), JsonOpts);
            if (digest != null && !string.IsNullOrWhiteSpace(digest.EventModelId))
                list.Add(digest);
        }

        return list;
    }

    private static IReadOnlyList<EventProcessingContractDigest> DeserializeSingle(JsonElement objectEl)
    {
        var digest = JsonSerializer.Deserialize<EventProcessingContractDigest>(objectEl.GetRawText(), JsonOpts);
        if (digest == null || string.IsNullOrWhiteSpace(digest.EventModelId))
            return Array.Empty<EventProcessingContractDigest>();

        return [digest];
    }

    public static List<string> GetPlannedEventModelIds(CampaignWorkflowState state)
    {
        var briefJson = state.Artifacts.CampaignDesignBriefApproved
                        ?? state.Artifacts.CampaignDesignBriefProposed;
        if (string.IsNullOrWhiteSpace(briefJson))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(briefJson);
            if (!doc.RootElement.TryGetProperty("plannedEventModelIds", out var idsEl) ||
                idsEl.ValueKind != JsonValueKind.Array)
                return [];

            var ids = new List<string>();
            foreach (var idEl in idsEl.EnumerateArray())
            {
                if (idEl.ValueKind == JsonValueKind.String)
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                        ids.Add(id);
                }
            }

            return ids;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static Dictionary<string, bool> BuildProcessEventEligibilityMap(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var contract in Read(state))
        {
            if (string.IsNullOrWhiteSpace(contract.EventModelId))
                continue;
            map[contract.EventModelId] = contract.IsProcessEventEligible;
        }

        return map;
    }
}
