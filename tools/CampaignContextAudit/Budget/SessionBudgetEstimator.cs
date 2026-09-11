using CampaignContextAudit.Mapping;
using CampaignContextAudit.Models;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;

namespace CampaignContextAudit.Budget;

public static class SessionBudgetEstimator
{
    public static int EstimateSessionChars(
        CampaignWorkflowState state,
        IReadOnlyList<AgentMessageDoc> historyRows,
        string? tenantSliceText = null)
    {
        var header = $"Session context: authenticated tenantId is \"{state.TenantId}\".\n";
        var messages = historyRows.Select(d => AgentMessageDocMapper.ToAgentMessage(d)).ToList();
        var threadBlock = AuditThreadContextBuilder.Build(messages).ToPromptBlock();
        var openingUser = messages.FirstOrDefault(m => m.Role == "user")?.Content;
        var artifacts = CampaignWorkflowArtifactPromptBuilder.Build(state, openingUser);
        var body = header;
        if (!string.IsNullOrEmpty(threadBlock)) body += "\n" + threadBlock;
        if (!string.IsNullOrEmpty(artifacts)) body += "\n\n" + artifacts;
        if (!string.IsNullOrWhiteSpace(tenantSliceText)) body += "\n" + tenantSliceText.Trim();
        return ("\n---\nSESSION\n---\n" + body.TrimEnd()).Length;
    }
}
