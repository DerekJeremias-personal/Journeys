using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

public static class BcpArtifactRefresh
{
    public static void Refresh(CampaignWorkflowState state)
    {
        var subStep = WorkflowSkillRegistry.ResolveBuildSubStep(state);
        if (subStep == CampaignBuildSubStep.JourneyRequired)
        {
            if (string.IsNullOrWhiteSpace(state.Artifacts.JourneyScaffold))
            {
                var brief = state.Artifacts.CampaignDesignBriefApproved
                            ?? state.Artifacts.CampaignDesignBriefProposed;
                var episode = RuleEpisodeInferrer.Infer(brief, null, state.Artifacts.LastValidateViolationCode);
                state.Artifacts.JourneyScaffold = JourneyScaffoldBuilder.Build(
                    episode,
                    state.Artifacts.PointAccountManifest,
                    state.Artifacts.JourneyPatternId,
                    brief);
            }

            if (string.IsNullOrWhiteSpace(state.Artifacts.JourneyAuthoringTemplateJson))
            {
                var eventId = !string.IsNullOrWhiteSpace(state.Artifacts.SelectedEventModelId)
                    ? state.Artifacts.SelectedEventModelId
                    : EventModelExpectedIdResolver.Resolve(state)
                      ?? EventModelContractsAccumulator.ReadResolved(state)
                          .FirstOrDefault(d => d.IsProcessEventEligible)?.EventModelId;
                state.Artifacts.JourneyAuthoringTemplateJson = JourneyAuthoringTemplateBuilder.Build(
                    state.Artifacts.PointAccountManifest,
                    eventId,
                    state.Artifacts.JourneyPatternId);
            }
        }

        if (CreationSnapshotArtifact.Read(state) is { CreationComplete: true }
            && string.IsNullOrWhiteSpace(state.Artifacts.EventPayloadScaffold))
        {
            state.Artifacts.EventPayloadScaffold = EventPayloadScaffoldBuilder.Build(state);
        }
    }
}
