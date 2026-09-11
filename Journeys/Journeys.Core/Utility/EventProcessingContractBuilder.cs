using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public static class EventProcessingContractBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static EventProcessingContractDigest BuildFromModel(ModelDto model)
    {
        var digest = new EventProcessingContractDigest
        {
            EventModelId = model?.ID ?? string.Empty,
            EventModelName = model?.Name,
            EventModelType = model?.ModelType,
            ModelTag = model?.Tag,
            IsProcessEventEligible = EventModelEligibilityValidation.IsProcessEventEligible(model),
            IsLoyaltyAccountCreationEvent = EventModelEligibilityValidation.IsLoyaltyAccountCreationEvent(model),
            ProcessingRole = EventModelEligibilityValidation.ResolveProcessingRole(model)
        };
        digest.ProcessingRoleDescription =
            EventModelEligibilityValidation.GetProcessingRoleDescription(digest.ProcessingRole);
        digest.Warnings.AddRange(EventModelEligibilityValidation.GetEligibilityWarnings(model));

        if (model?.ModelMetaData == null)
        {
            digest.Warnings.Add("modelMetaData is missing.");
            return digest;
        }

        if (model.ModelMetaData.TryGetValue("Wrapper", out var wrapper) && !string.IsNullOrWhiteSpace(wrapper))
            digest.WrapperModelId = wrapper.Trim();
        else
            digest.Warnings.Add("Wrapper is missing in modelMetaData.");

        if (model.ModelMetaData.TryGetValue("AccountXIdSymbol", out var accountPath) && !string.IsNullOrWhiteSpace(accountPath))
            digest.AccountLink.SymbolPath = accountPath.Trim();
        else
            digest.Warnings.Add("AccountXIdSymbol is missing in modelMetaData.");

        if (model.ModelMetaData.TryGetValue("TimeOfOccurrence", out var toe) && !string.IsNullOrWhiteSpace(toe))
            digest.TimeOfOccurrenceSymbol = toe.Trim();

        if (EventModelContractValidation.TryParseNaturalKeySymbols(model, out var nkSymbols, out var nkError))
            digest.NaturalKey.Symbols = nkSymbols.ToList();
        else if (!string.IsNullOrEmpty(nkError))
            digest.Warnings.Add(nkError);

        digest.Warnings.AddRange(EventModelContractValidation.GetAccountXIdSymbolWarnings(model));

        foreach (var sym in digest.NaturalKey.Symbols)
        {
            if (!EventModelContractValidation.SymbolDeclaredOnEventModel(sym, model))
                digest.Warnings.Add($"Natural key symbol '{sym}' is not declared on event model '{model.Name}'.");
        }

        return digest;
    }

    public static string SerializeDigest(EventProcessingContractDigest digest) =>
        JsonSerializer.Serialize(digest, JsonOpts);

    public static bool TryBuildFromToolResult(string? toolResultJson, out string? digestJson)
    {
        digestJson = null;
        if (string.IsNullOrWhiteSpace(toolResultJson))
            return false;

        // Tolerate MCP/MEAI result wrappers (text-content envelope or double-encoded string) so a
        // successful GetModel/SaveModel still resolves a contract digest.
        toolResultJson = ToolResultJsonNormalizer.Unwrap(toolResultJson);
        if (string.IsNullOrWhiteSpace(toolResultJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(toolResultJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var model = JsonSerializer.Deserialize<ModelDto>(toolResultJson, JsonOpts);
            if (model == null || string.IsNullOrWhiteSpace(model.ID) && string.IsNullOrWhiteSpace(model.Name))
                return false;

            var digest = BuildFromModel(model);
            digestJson = SerializeDigest(digest);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
