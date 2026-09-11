namespace Journeys.API.CampaignAgent;

/// <summary>Canonical MCP tool name (usually snake_case) → PascalCase aliases the model often guesses.</summary>
internal static class CampaignAgentToolAliasCatalog
{
    public static readonly IReadOnlyList<(string Canonical, string Alias)> Pairs =
    [
        ("upsert_campaign", "UpsertCampaign"),
        ("validate_campaign", "ValidateCampaign"),
        ("get_campaign", "GetCampaign"),
        ("list_campaigns", "ListCampaigns"),
        ("delete_campaign", "DeleteCampaign"),
        ("upsert_point_account_type", "UpsertPointAccountType"),
        ("list_point_account_types", "ListPointAccountTypes"),
        ("get_point_account_type", "GetPointAccountType"),
        ("get_campaign_assistant_context", "GetCampaignAssistantContext"),
        ("get_rules_engine_contract_summary", "GetRulesEngineContractSummary"),
        ("get_rule_pattern_recipes", "GetRulePatternRecipes"),
        ("process_event", "ProcessEvent"),
        ("move_tier", "MoveTier"),
        ("get_model", "GetModel"),
        ("list_models", "ListModels"),
        ("get_all_models", "GetAllModels"),
        ("get_many_models", "GetManyModels"),
        ("get_model_attributes_for_rules", "GetModelAttributesForRules"),
        ("save_model", "SaveModel"),
        ("delete_model", "DeleteModel"),
        ("build_taxonomic_rule", "BuildTaxonomicRule"),
        ("list_example_models", "ListExampleModels"),
        ("get_example_model", "GetExampleModel"),
        ("build_event_wrapper", "BuildEventWrapper"),
        ("propose_campaign_design_brief", "ProposeCampaignDesignBrief"),
    ];
}
