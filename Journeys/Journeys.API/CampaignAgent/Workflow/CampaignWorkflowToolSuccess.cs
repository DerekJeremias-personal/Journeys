using Journeys.CampaignAgent.Remediation;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Heuristic: MCP tools returning JSON with top-level <c>errors</c> or <c>error: true</c> are failures.
/// </summary>
public static class CampaignWorkflowToolSuccess
{
    public static bool LooksSuccessful(string? resultJson) =>
        ToolResultSuccessEvaluator.LooksSuccessful(resultJson);
}
