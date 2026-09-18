using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Removes tools that are not allowed in the current workflow phase (strict mode).
/// Pre-brief: read-only, warehouse/objective, and non-mutating backend tools.
/// Post-brief, pre-EventModels-gate: model tools only — campaign mutators blocked.
/// Post-EventModels-gate: all tools.
/// </summary>
public static class CampaignWorkflowToolFilter
{
    private static readonly HashSet<string> ReadOnlyJourneysTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "ListCampaigns", "list_campaigns",
        "GetCampaign", "get_campaign",
        "GetCampaignAssistantContext", "get_campaign_assistant_context",
        "ListPointAccountTypes", "list_point_account_types",
        "GetPointAccountType", "get_point_account_type",
        "GetAccount", "get_account",
        "GetRulesEngineContractSummary", "get_rules_engine_contract_summary",
        "GetRulePatternRecipes", "get_rule_pattern_recipes",
        "ValidateCampaign", "validate_campaign"
    };

    public static List<AITool> Apply(IReadOnlyList<AITool> tools, CampaignWorkflowState state, bool dataWarehouseEnabled = true)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return tools.Where(t => IsPreBriefTool(t.Name, dataWarehouseEnabled)).ToList();

        if (EventModelsGateBlocksCampaignMutators(state))
            return FilterListCampaigns(
                tools.Where(t => IsAllowedDuringEventModelsGate(t.Name, dataWarehouseEnabled)).ToList(),
                state);

        if (CampaignJourneyDeliveryGuard.BlocksJourneyMutators(state))
            return FilterListCampaigns(tools.Where(t => !IsJourneyMutator(t.Name)).ToList(), state);

        if (PatRequiredBlocksJourneyMutators(state))
            return FilterListCampaigns(
                tools.Where(t => !IsBlockedDuringPatRequired(t.Name, state)).ToList(),
                state);

        return FilterListCampaigns(tools.ToList(), state);
    }

    private static List<AITool> FilterListCampaigns(List<AITool> tools, CampaignWorkflowState state) =>
        tools.Where(t => !ShouldHideListCampaigns(t.Name, state)).ToList();

    internal static bool ShouldHideListCampaigns(string? name, CampaignWorkflowState state)
    {
        if (!IsListCampaignsTool(name))
            return false;
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return false;
        if (state.Artifacts.ValidationStalled)
            return true;
        return CampaignWorkflowKnownCampaign.HasKnownCampaignId(state);
    }

    private static bool IsListCampaignsTool(string? name) =>
        string.Equals(name, "ListCampaigns", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "list_campaigns", StringComparison.OrdinalIgnoreCase);

    internal static bool EventModelsGateBlocksCampaignMutators(CampaignWorkflowState state) =>
        !EventModelsReadiness.Evaluate(state).IsReady;

    internal static bool PatRequiredBlocksJourneyMutators(CampaignWorkflowState state) =>
        !EventModelsGateBlocksCampaignMutators(state)
        && WorkflowSkillRegistry.ResolveBuildSubStep(state) == CampaignBuildSubStep.PatRequired;

    private static bool IsBlockedDuringPatRequired(string? name, CampaignWorkflowState state)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (string.Equals(name, "validate_campaign", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "ValidateCampaign", StringComparison.OrdinalIgnoreCase))
            return true;

        if ((string.Equals(name, "upsert_campaign", StringComparison.OrdinalIgnoreCase)
             || string.Equals(name, "UpsertCampaign", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(state.Artifacts.CampaignShellRef))
            return true;

        return false;
    }

    private static bool IsAllowedDuringEventModelsGate(string? name, bool dataWarehouseEnabled)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (IsReadOnlyTool(name))
            return true;
        if (dataWarehouseEnabled && CampaignWorkflowPhaseNames.IsDataWarehouseTool(name))
            return true;
        if (!dataWarehouseEnabled && CampaignWorkflowPhaseNames.IsObjectiveProposalTool(name))
            return true;
        if (CampaignWorkflowPhaseNames.BackendModelTools.Contains(name))
            return true;
        return false;
    }

    private static bool IsPreBriefTool(string? name, bool dataWarehouseEnabled)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (IsReadOnlyTool(name)) return true;
        if (dataWarehouseEnabled && CampaignWorkflowPhaseNames.IsDataWarehouseTool(name)) return true;
        if (!dataWarehouseEnabled && CampaignWorkflowPhaseNames.IsObjectiveProposalTool(name)) return true;
        if (CampaignWorkflowPhaseNames.BackendModelTools.Contains(name)
            && (!CampaignWorkflowPhaseNames.MutatingBackendTools.Contains(name)
                || CampaignAgentBackendMcp.IsSaveModelToolName(name)))
            return true;
        return false;
    }

    private static bool IsReadOnlyTool(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (CampaignWorkflowPhaseNames.IsDataWarehouseTool(name))
            return true;
        if (ReadOnlyJourneysTools.Contains(name))
            return true;
        if (CampaignWorkflowPhaseNames.BackendModelTools.Contains(name)
            && !CampaignWorkflowPhaseNames.MutatingBackendTools.Contains(name))
            return true;
        return false;
    }

    private static bool IsJourneyMutator(string? name) =>
        string.Equals(name, "validate_campaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "ValidateCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "upsert_campaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "UpsertCampaign", StringComparison.OrdinalIgnoreCase);
}
