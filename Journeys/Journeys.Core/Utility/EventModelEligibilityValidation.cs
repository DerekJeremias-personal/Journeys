using Backend.Dto.Structures.Model;

namespace Journeys.Core.Utility;

public static class EventModelEligibilityValidation
{
    public const string EventableTag = "eventable";
    public const string RoleStandard = "standard_event";
    public const string RoleLoyaltyAccountCreation = "loyalty_account_creation";

    public static bool IsProcessEventEligible(ModelDto? model) =>
        string.Equals(model?.Tag, EventableTag, StringComparison.Ordinal);

    public static bool IsLoyaltyAccountCreationEvent(ModelDto? model) =>
        model != null && ModelUtility.IsLoyaltyAccount(model);

    public static string ResolveProcessingRole(ModelDto? model) =>
        IsLoyaltyAccountCreationEvent(model) ? RoleLoyaltyAccountCreation : RoleStandard;

    public static string? GetProcessingRoleDescription(string role) => role switch
    {
        RoleLoyaltyAccountCreation =>
            "Account-creation event: ProcessEvent may create or update the loyalty account on first process. Do not use as the default Verification fixture for existing members.",
        _ =>
            "Standard event: ProcessEvent requires an existing loyalty account resolved via AccountXIdSymbol on the payload root."
    };

    public static List<string> GetEligibilityWarnings(ModelDto? model)
    {
        var warnings = new List<string>();
        if (model == null)
            return warnings;

        var eligible = IsProcessEventEligible(model);
        var loyaltyCreation = IsLoyaltyAccountCreationEvent(model);
        var engineEvent = LooksLikeEngineEventModel(model);

        if (!eligible)
        {
            warnings.Add(
                string.IsNullOrWhiteSpace(model.Tag)
                    ? "Model tag is missing; set tag to \"eventable\" for ProcessEvent and Campaign.Events eligibility."
                    : $"Model tag is \"{model.Tag}\"; only tag \"eventable\" is valid for ProcessEvent and Campaign.Events.");
        }

        if (engineEvent && !eligible)
        {
            warnings.Add(
                "Engine event model metadata is present (Wrapper or ProcessingType) but tag is not \"eventable\"; set tag on SaveModel.");
        }

        if (loyaltyCreation && !eligible)
        {
            warnings.Add(
                "IsLoyaltyAccount metadata is true but tag is not \"eventable\"; loyalty account creation events require both.");
        }

        if (loyaltyCreation && eligible)
        {
            warnings.Add(
                "Loyalty account creation event (IsLoyaltyAccount): use for enrollment only — not the default Verification ProcessEvent fixture.");
        }

        return warnings;
    }

    private static bool LooksLikeEngineEventModel(ModelDto model)
    {
        if (model.ModelMetaData == null)
            return false;

        if (model.ModelMetaData.ContainsKey("Wrapper"))
            return true;

        return model.ModelMetaData.TryGetValue("ProcessingType", out var processingType)
               && processingType.Contains("Engine", StringComparison.OrdinalIgnoreCase);
    }
}
