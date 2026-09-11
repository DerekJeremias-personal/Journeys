using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Detects save_model payloads that are not event payloads during the EventModels gate
/// and PAT-shaped fabrications in any workflow phase.
/// </summary>
public static class EventModelSaveGuard
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] PatMetaDataKeys =
    [
        "ledgertype",
        "isspendable",
        "pointaccounttypename"
    ];

    public static bool LooksLikePointAccountTypeModel(ModelDto? model) =>
        model?.IsContainer == true
        && HasSymbol(model, "pointsourceid")
        && HasSymbol(model, "ledgertype");

    public static bool LooksLikePatFabrication(ModelDto? model)
    {
        if (model == null)
            return false;

        if (IsLegitimateEventPayload(model))
            return false;

        if (LooksLikePointAccountTypeModel(model))
            return true;

        if (HasPatMetaDataKeys(model))
            return true;

        if (HasBalanceAttribute(model) && PatNamingHeuristics.LooksTierQualLike(model.Name ?? model.DisplayName))
            return true;

        if ((HasSymbol(model, "ledgertype") || HasSymbol(model, "isspendable"))
            && !IsEventable(model))
            return true;

        return false;
    }

    public static bool LooksLikePatFabricationFromJson(string? toolJson) =>
        TryParseModel(toolJson, out var model) && LooksLikePatFabrication(model);

    public static bool LooksLikeCampaignContainerModel(ModelDto? model) =>
        model?.IsContainer == true
        && (HasSymbol(model, "extcampaignid") || HasSymbol(model, "journey"));

    public static bool CanMergeToResolved(EventProcessingContractDigest incoming) =>
        incoming.IsProcessEventEligible
        && !EventModelsReadiness.IsWrapperDigest(incoming);

    public static string GetPatFabricationRemediation() =>
        "save_model created a point-account-type-shaped model; use upsert_point_account_type — not save_model. "
        + "Tier-qualification counter: ledgerType NonSpendable, isSpendable false. "
        + "Register via UpsertPointAccountType so PointAccountManifest.items is populated.";

    public static string? GetBypassRemediation(string? toolJson)
    {
        if (!TryParseModel(toolJson, out var model))
            return null;

        if (WrapperModelContractValidator.LooksLikeEventWrapper(model))
            return null;

        if (LooksLikePatFabrication(model))
            return GetPatFabricationRemediation();

        if (LooksLikeCampaignContainerModel(model))
            return "save_model created a campaign-shaped container; use upsert_campaign — not save_model.";

        if (model.IsContainer == true)
            return "save_model saved a loyalty container that is not an eventable event payload; Events gate unchanged. Persist the event model (tag eventable) or confirm reuse of an existing one.";

        return "save_model did not persist an eventable event payload; Events gate unchanged. Set tag eventable and modelMetaData, or confirm reuse of an existing event model.";
    }

    private static bool IsLegitimateEventPayload(ModelDto model) =>
        IsEventable(model)
        && model.ModelMetaData != null
        && model.ModelMetaData.Keys.Any(k =>
            string.Equals(k, "Wrapper", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(model.ModelMetaData[k]));

    private static bool IsEventable(ModelDto model) =>
        string.Equals(model.Tag, "eventable", StringComparison.OrdinalIgnoreCase);

    private static bool HasPatMetaDataKeys(ModelDto model)
    {
        if (model.ModelMetaData == null || model.ModelMetaData.Count == 0)
            return false;

        return model.ModelMetaData.Keys.Any(k =>
            PatMetaDataKeys.Any(p => string.Equals(k, p, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasBalanceAttribute(ModelDto model) =>
        HasSymbol(model, "balance") || HasSymbol(model, "currentbalance");

    private static bool HasSymbol(ModelDto model, string symbol) =>
        model.Attributes?.Any(a =>
            string.Equals(a.Symbol, symbol, StringComparison.OrdinalIgnoreCase)) == true;

    private static bool TryParseModel(string? toolJson, out ModelDto? model)
    {
        model = null;
        if (string.IsNullOrWhiteSpace(toolJson))
            return false;

        var normalized = ToolResultJsonNormalizer.Unwrap(toolJson);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        try
        {
            model = JsonSerializer.Deserialize<ModelDto>(normalized, JsonOpts);
            return model != null && !string.IsNullOrWhiteSpace(model.ID);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
