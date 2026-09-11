using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowPhaseNormalizerTests
{
    [Theory]
    [InlineData(CampaignWorkflowPhase.CampaignSetup)]
    [InlineData(CampaignWorkflowPhase.PointAccountTypes)]
    [InlineData(CampaignWorkflowPhase.CampaignJourney)]
    public void Normalize_maps_legacy_build_phases_to_CampaignBuild(CampaignWorkflowPhase legacy) =>
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, CampaignWorkflowPhaseNormalizer.Normalize(legacy));

    [Fact]
    public void Normalize_leaves_EventModels_unchanged() =>
        Assert.Equal(CampaignWorkflowPhase.EventModels, CampaignWorkflowPhaseNormalizer.Normalize(CampaignWorkflowPhase.EventModels));
}
