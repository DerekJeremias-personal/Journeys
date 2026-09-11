using System.Text;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>Builds WORKFLOW ARTIFACTS block for SESSION from phase and stored artifacts.</summary>
public static class CampaignWorkflowArtifactPromptBuilder
{
    public static string Build(CampaignWorkflowState state, string? openingUserMessage = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WORKFLOW ARTIFACTS (authoritative for this phase; prefer over memory)");

        var salient = SalientFactsPromptBuilder.Build(state, openingUserMessage);
        if (!string.IsNullOrEmpty(salient))
        {
            sb.AppendLine();
            sb.AppendLine(salient);
        }

        switch (state.Phase)
        {
            case CampaignWorkflowPhase.DataAnalysis:
                AppendSection(sb, "CampaignDesignBrief (draft)", state.Artifacts.CampaignDesignBriefProposed);
                break;

            case CampaignWorkflowPhase.EventModels:
                AppendDigest(sb, "CampaignDesignBrief", state.Artifacts.CampaignDesignBriefApproved ?? state.Artifacts.CampaignDesignBriefProposed);
                AppendEventModelContracts(sb, state);
                break;

            case CampaignWorkflowPhase.CampaignSetup:
                AppendDigest(sb, "CampaignDesignBrief", state.Artifacts.CampaignDesignBriefApproved);
                AppendEventModelContracts(sb, state);
                AppendValidationSummary(sb, state);
                break;

            case CampaignWorkflowPhase.PointAccountTypes:
                AppendDigest(sb, "CampaignDesignBrief", state.Artifacts.CampaignDesignBriefApproved);
                AppendEventModelContracts(sb, state);
                AppendSection(sb, "CampaignShellRef", state.Artifacts.CampaignShellRef);
                AppendSection(sb, "PointAccountManifest", state.Artifacts.PointAccountManifest);
                AppendJourneyPatternSkeleton(sb, state);
                AppendJourneyScaffold(sb, state);
                break;

            case CampaignWorkflowPhase.CampaignBuild:
            case CampaignWorkflowPhase.CampaignJourney:
                AppendDigest(sb, "CampaignDesignBrief", state.Artifacts.CampaignDesignBriefApproved);
                AppendEventModelContracts(sb, state);
                AppendSection(sb, "CampaignShellRef", state.Artifacts.CampaignShellRef);
                AppendSection(sb, "PointAccountManifest", state.Artifacts.PointAccountManifest);
                AppendJourneyPatternSkeleton(sb, state);
                AppendJourneyScaffold(sb, state);
                AppendValidationSummary(sb, state);
                break;

            case CampaignWorkflowPhase.Verification:
            case CampaignWorkflowPhase.Done:
                AppendDigest(sb, "CampaignDesignBrief", state.Artifacts.CampaignDesignBriefApproved);
                AppendEventModelContracts(sb, state);
                AppendSection(sb, "CampaignShellRef", state.Artifacts.CampaignShellRef);
                AppendSection(sb, "PointAccountManifest", state.Artifacts.PointAccountManifest);
                AppendSection(sb, "JourneyDigest", state.Artifacts.JourneyDigestApproved ?? state.Artifacts.JourneyDigestProposed);
                AppendSection(sb, "CreationSnapshot", state.Artifacts.CreationSnapshot);
                AppendValidationSummary(sb, state);
                break;
        }

        if (state.IsAwaitingApproval)
        {
            sb.AppendLine();
            if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.LivePromotion)
            {
                sb.AppendLine("Awaiting user approval: LivePromotion — verify Draft first; reply 'promote to Live' to allow Live upsert.");
            }
            else
            {
                sb.Append("Awaiting user approval: ").AppendLine(state.Artifacts.AwaitingApproval.ToString());
            }
            sb.AppendLine("Do not call mutating tools for the next phase until the user approves (approve / continue / looks good).");
        }

        if (state.Artifacts.ValidationStalled)
        {
            sb.AppendLine();
            sb.Append("Validation stalled: cycleCount=")
                .Append(state.Artifacts.ValidationStallCycleCount);
            if (!string.IsNullOrWhiteSpace(state.Artifacts.ValidationStallTrippedAtUtc))
                sb.Append(", trippedAt=").Append(state.Artifacts.ValidationStallTrippedAtUtc);
            sb.AppendLine();
            sb.AppendLine("Do not call validate_campaign or upsert_campaign until the user sends a new message with guidance.");
            var stallHint = CampaignValidationCoach.GetStallCoachHint(state);
            if (!string.IsNullOrWhiteSpace(stallHint))
                sb.AppendLine(stallHint);
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendEventModelContracts(StringBuilder sb, CampaignWorkflowState state)
    {
        AppendSection(sb, "ResolvedEventModelContracts", EventModelContractsAccumulator.SerializeResolvedForPrompt(state));
        AppendSection(sb, "DiscoveredEventModelContracts", EventModelContractsAccumulator.SerializeDiscoveredForPrompt(state));
    }

    private static void AppendSection(StringBuilder sb, string title, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;
        sb.AppendLine().Append("--- ").Append(title).AppendLine(" ---");
        sb.AppendLine(json);
    }

    private static void AppendDigest(StringBuilder sb, string title, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;
        var digest = json.Length > 1200 ? json.Substring(0, 1200) + "…" : json;
        sb.AppendLine().Append("--- ").Append(title).Append(" (digest) ---");
        sb.AppendLine(digest);
    }

    private static void AppendValidationSummary(StringBuilder sb, CampaignWorkflowState state)
    {
        var line = CampaignValidationCoach.FormatPromptLine(state);
        if (string.IsNullOrWhiteSpace(line))
            return;
        sb.AppendLine().AppendLine(line);
    }

    private static void AppendJourneyScaffold(StringBuilder sb, CampaignWorkflowState state)
    {
        if (WorkflowSkillRegistry.ResolveBuildSubStep(state) != CampaignBuildSubStep.JourneyRequired)
            return;

        AppendJourneyAuthoringTemplate(sb, state);

        if (string.IsNullOrWhiteSpace(state.Artifacts.JourneyScaffold))
            return;

        sb.AppendLine().AppendLine("--- JourneyScaffold ---");
        sb.AppendLine(state.Artifacts.JourneyScaffold);
    }

    private static void AppendJourneyAuthoringTemplate(StringBuilder sb, CampaignWorkflowState state)
    {
        if (string.IsNullOrWhiteSpace(state.Artifacts.JourneyAuthoringTemplateJson))
            return;

        sb.AppendLine().AppendLine("--- JourneyAuthoringTemplate ---");
        sb.AppendLine(state.Artifacts.JourneyAuthoringTemplateJson);
    }

    private static void AppendJourneyPatternSkeleton(StringBuilder sb, CampaignWorkflowState state)
    {
        if (!state.Artifacts.JourneyPatternPrepComplete
            || string.IsNullOrWhiteSpace(state.Artifacts.JourneyPatternSkeletonJson))
        {
            return;
        }

        sb.AppendLine().Append("--- JourneyPatternSkeleton (").Append(state.Artifacts.JourneyPatternId).AppendLine(") ---");
        var skeleton = state.Artifacts.JourneyPatternSkeletonJson;
        if (skeleton.Length > 2500)
            skeleton = skeleton[..2500] + "…";
        sb.AppendLine(skeleton);
    }
}
