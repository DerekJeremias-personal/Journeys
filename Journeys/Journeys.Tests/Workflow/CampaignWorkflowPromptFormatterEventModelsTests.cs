using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowPromptFormatterEventModelsTests
{
    private static CampaignWorkflowState EventModelsState(EarningIntent intent, string? recommendedId)
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.Artifacts.EarningIntent = intent;
        state.Artifacts.CampaignDesignBriefProposed = "Brief captured for tests.";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = recommendedId,
            Candidates =
            {
                new EventModelCandidate { EventModelId = "o1", Name = "Order", IsStrongMatch = recommendedId == "o1" },
                new EventModelCandidate { EventModelId = "e1", Name = "EmailOpened" }
            }
        });
        return state;
    }

    [Fact]
    public void Hint_uses_coach_mode_and_checklist_digest()
    {
        var hint = CampaignWorkflowPromptFormatter.BuildHint(EventModelsState(EarningIntent.PointEarning, "o1"));

        Assert.Contains("coach mode", hint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Checklist:", hint);
        Assert.Contains("[✓ Brief]", hint);
        Assert.Contains("Active focus:", hint);
    }

    [Fact]
    public void Hint_shows_brief_not_captured_when_missing()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.DataAnalysis;

        var hint = CampaignWorkflowPromptFormatter.BuildHint(state);

        Assert.Contains("Brief captured: no", hint);
        Assert.Contains("[○ Brief]", hint);
        Assert.Contains("Coach:", hint);
    }

    [Fact]
    public void Hint_includes_coach_nudge_when_pending_review_unsatisfied()
    {
        var state = EventModelsState(EarningIntent.PointEarning, "o1");
        PendingEventModelSpecArtifact.Write(state, new PendingEventModelSpecDto { Name = "Review" });

        var hint = CampaignWorkflowPromptFormatter.BuildHint(state);

        Assert.Contains("Coach nudge:", hint);
        Assert.Contains("Events gate: closed", hint);
        Assert.Contains("Review", hint);
        Assert.Contains("Do not declare Events or Campaign complete", hint);
    }
}
