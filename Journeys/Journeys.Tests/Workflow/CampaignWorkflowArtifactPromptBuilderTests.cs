using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowArtifactPromptBuilderTests
{
    [Fact]
    public void CampaignSetup_includes_validation_summary_line()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignSetup;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        state.Artifacts.CampaignValidationSummary =
            """{"validatedAtUtc":"2026-06-15T12:00:00Z","payloadFingerprint":"a1b2c3d4","isValid":true,"errorCount":0,"warningCount":1,"topWarningCodes":[],"phase":"CampaignSetup"}""";

        var block = CampaignWorkflowArtifactPromptBuilder.Build(state);

        Assert.Contains("Validation: isValid=true", block);
        Assert.Contains("warnings=1", block);
        Assert.Contains("fingerprint=a1b2c3d4", block);
    }

    [Fact]
    public void Verification_includes_pat_manifest_and_creation_snapshot()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.Verification;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        state.Artifacts.PointAccountManifest = """{"schemaVersion":1,"items":[{"id":"pat-1"}]}""";
        state.Artifacts.CreationSnapshot =
            """{"schemaVersion":1,"creationComplete":true,"campaignId":"camp-1","journeyRuleSetCount":2}""";

        var block = CampaignWorkflowArtifactPromptBuilder.Build(state);

        Assert.Contains("PointAccountManifest", block);
        Assert.Contains("pat-1", block);
        Assert.Contains("CreationSnapshot", block);
        Assert.Contains("camp-1", block);
    }
}
