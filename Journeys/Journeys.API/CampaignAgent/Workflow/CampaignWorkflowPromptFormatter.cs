using System.Text;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

public static class CampaignWorkflowPromptFormatter
{
    public static string BuildHint(
        CampaignWorkflowState state,
        IReadOnlyList<string>? toolsThisTurn = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WORKFLOW (coach mode — checklist is advisory, not a lock).");
        sb.Append("- Brief captured: ").AppendLine(CampaignWorkflowChecklist.IsBriefCaptured(state) ? "yes" : "no");
        sb.Append("- Active focus: ").AppendLine(state.Phase.ToString());
        sb.Append("- Campaign kind: ").AppendLine(state.CampaignKind.ToString());
        sb.Append("- Checklist: ").AppendLine(CampaignWorkflowChecklist.FormatDigest(state));

        if (CampaignWorkflowChecklist.IsBriefCaptured(state)
            && state.CampaignKind == CampaignWorkflowKind.EventDriven
            && !state.UserSkippedEventModels)
        {
            var readiness = EventModelsReadiness.Evaluate(state);
            sb.Append("- Events gate: ").AppendLine(readiness.IsReady ? "open" : "closed");
            if (!readiness.IsReady && readiness.Blockers.Count > 0)
                sb.Append("- Events blockers: ").AppendLine(string.Join(", ", readiness.Blockers));
            sb.Append("- Model gate passed: ").AppendLine(state.ModelGatePassed ? "yes" : "no");
            if (!readiness.IsReady || !CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.EventModels))
                sb.AppendLine("Do not declare Events or Campaign complete until checklist shows [✓ Events] and Events gate is open.");
        }

        var coach = CampaignWorkflowStepManager.GetCoachHint(state, toolsThisTurn);
        if (!string.IsNullOrEmpty(coach))
            sb.Append("Coach nudge: ").AppendLine(coach);

        if (toolsThisTurn is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine(WorkflowSkillRegistry.BuildSessionSkillBlock(state, toolsThisTurn));
        }

        return sb.ToString().TrimEnd();
    }
}
