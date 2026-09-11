using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public sealed class WrapperContractValidationEntry
{
    public string WrapperModelId { get; set; } = string.Empty;

    public string? WrapperModelName { get; set; }

    public List<string> Errors { get; set; } = [];

    public List<string> Warnings { get; set; } = [];
}

public static class WrapperContractArtifact
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static bool TryMergeFromToolJson(CampaignWorkflowState state, string toolJson, ModelDto? linkedEventModel = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!TryParseModel(toolJson, out var model) || !WrapperModelContractValidator.LooksLikeEventWrapper(model))
            return false;

        var validation = WrapperModelContractValidator.Validate(model, linkedEventModel);
        MergeEntry(state, new WrapperContractValidationEntry
        {
            WrapperModelId = model!.ID ?? string.Empty,
            WrapperModelName = model.Name,
            Errors = validation.Errors.ToList(),
            Warnings = validation.Warnings.ToList()
        });

        return true;
    }

    public static IReadOnlyList<WrapperContractValidationEntry> Read(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(state.Artifacts.WrapperContractValidation))
            return Array.Empty<WrapperContractValidationEntry>();

        try
        {
            return JsonSerializer.Deserialize<List<WrapperContractValidationEntry>>(
                       state.Artifacts.WrapperContractValidation, JsonOpts)
                   ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<WrapperContractValidationEntry>();
        }
    }

    public static void Clear(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Artifacts.WrapperContractValidation = null;
    }

    /// <summary>
    /// When reusing an existing tenant event model, seed wrapper validation from the resolved
    /// processing contract so the Events gate can open without a separate wrapper get_model hop.
    /// </summary>
    public static bool TrySeedTrustedReuseFromContract(
        CampaignWorkflowState state,
        EventProcessingContractDigest contract)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contract);

        if (string.IsNullOrWhiteSpace(contract.WrapperModelId))
            return false;
        if (!contract.IsProcessEventEligible)
            return false;
        if (string.IsNullOrWhiteSpace(contract.AccountLink?.SymbolPath))
            return false;
        if (contract.NaturalKey?.Symbols == null || contract.NaturalKey.Symbols.Count == 0)
            return false;

        if (Read(state).Any(e =>
                string.Equals(e.WrapperModelId, contract.WrapperModelId, StringComparison.OrdinalIgnoreCase)))
            return false;

        MergeEntry(state, new WrapperContractValidationEntry
        {
            WrapperModelId = contract.WrapperModelId,
            WrapperModelName = InferWrapperName(contract),
            Errors = [],
            Warnings =
            [
                "Wrapper structural validation deferred on reuse of existing tenant event model; "
                + "call get_model on the wrapper if process_event fails."
            ]
        });
        return true;
    }

    private static string? InferWrapperName(EventProcessingContractDigest contract)
    {
        if (!string.IsNullOrWhiteSpace(contract.EventModelName))
            return contract.EventModelName + "AndRuleState";
        return null;
    }

    private static void MergeEntry(CampaignWorkflowState state, WrapperContractValidationEntry incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming.WrapperModelId))
            return;

        var list = Read(state).ToList();
        var index = list.FindIndex(e =>
            string.Equals(e.WrapperModelId, incoming.WrapperModelId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            list[index] = incoming;
        else
            list.Add(incoming);

        state.Artifacts.WrapperContractValidation = JsonSerializer.Serialize(list, JsonOpts);
    }

    private static bool TryParseModel(string toolJson, out ModelDto? model)
    {
        model = null;
        if (string.IsNullOrWhiteSpace(toolJson))
            return false;

        toolJson = ToolResultJsonNormalizer.Unwrap(toolJson);
        if (string.IsNullOrWhiteSpace(toolJson))
            return false;

        try
        {
            model = JsonSerializer.Deserialize<ModelDto>(toolJson, JsonOpts);
            return model != null && !string.IsNullOrWhiteSpace(model.ID);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
