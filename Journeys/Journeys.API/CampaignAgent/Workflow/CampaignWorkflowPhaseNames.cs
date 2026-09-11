namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>Data warehouse MCP tool names (Journeys MCP server).</summary>
public static class CampaignWorkflowPhaseNames
{
    public static readonly HashSet<string> DataWarehouseTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetProgramPerformanceSummary",
        "GetCampaignOutcomeStats",
        "GetMemberEngagementBands",
        "GetProductCategoryLift"
    };

    /// <summary>MCP tool used to capture the campaign objective when warehouse tools are disabled.</summary>
    public const string ProposeCampaignDesignBriefToolName = "ProposeCampaignDesignBrief";

    public static bool IsObjectiveProposalTool(string? name) =>
        !string.IsNullOrEmpty(name)
        && (string.Equals(name, ProposeCampaignDesignBriefToolName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "propose_campaign_design_brief", StringComparison.OrdinalIgnoreCase));

    public static readonly HashSet<string> BackendModelTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "ListModels", "list_models",
        "GetAllModels", "get_all_models",
        "GetModel", "get_model",
        "GetManyModels", "get_many_models",
        "GetModelAttributesForRules", "get_model_attributes_for_rules",
        "BuildTaxonomicRule", "build_taxonomic_rule",
        "ListExampleModels", "list_example_models",
        "GetExampleModel", "get_example_model",
        "BuildEventWrapper", "build_event_wrapper",
        "SaveModel", "save_model",
        "DeleteModel", "delete_model"
    };

    public static readonly HashSet<string> MutatingJourneysTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "UpsertCampaign", "upsert_campaign",
        "DeleteCampaign", "delete_campaign",
        "UpsertPointAccountType", "upsert_point_account_type",
        "ProcessEvent", "process_event",
        "MoveTier", "move_tier"
    };

    public static readonly HashSet<string> MutatingBackendTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "SaveModel", "save_model",
        "DeleteModel", "delete_model"
    };

    public static bool IsDataWarehouseTool(string? name) =>
        !string.IsNullOrEmpty(name) && DataWarehouseTools.Contains(name);

    public static bool IsMutatingTool(string? name) =>
        !string.IsNullOrEmpty(name)
        && (MutatingJourneysTools.Contains(name) || MutatingBackendTools.Contains(name));
}
