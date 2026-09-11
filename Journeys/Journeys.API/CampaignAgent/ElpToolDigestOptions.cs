namespace Journeys.API.CampaignAgent;

/// <summary>Per-tool digest toggles for Journeys MCP tools exposed to the campaign agent.</summary>
public sealed class JourneysToolDigestOptions
{
    public bool AssistantContextDigestEnabled { get; set; } = true;
    public bool MutationDigestEnabled { get; set; } = true;
    public bool RulesContractDigestEnabled { get; set; } = true;
    public bool RulePatternRecipesDigestEnabled { get; set; } = true;
    public bool ExampleCampaignDigestEnabled { get; set; } = true;
    public bool ValidationDigestEnabled { get; set; } = true;
    public bool ListCampaignsDigestEnabled { get; set; } = true;
}
