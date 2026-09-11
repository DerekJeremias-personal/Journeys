using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;
using Xunit;

namespace Journeys.Tests.Workflow;

public class BuildContextPackageResolverTests
{
    [Fact]
    public void Resolve_patRequired_empty_manifest()
    {
        var state = BuildState(manifestEmpty: true);
        var pkg = BuildContextPackageResolver.Resolve(
            state, ["upsert_point_account_type", "validate_campaign"]);
        Assert.Equal(CampaignBuildSubStep.PatRequired, pkg.SubStep);
        Assert.True(pkg.ToolSurfaceIncludesPatUpsert);
    }

    [Fact]
    public void Resolve_journeyRequired_when_manifest_populated()
    {
        var state = BuildState(manifestEmpty: false);
        var pkg = BuildContextPackageResolver.Resolve(state, ["validate_campaign"]);
        Assert.Equal(CampaignBuildSubStep.JourneyRequired, pkg.SubStep);
        Assert.False(pkg.ToolSurfaceIncludesPatUpsert);
    }

    private static CampaignWorkflowState BuildState(bool manifestEmpty)
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved =
            """{"objective":"Bronze Silver Gold tier program"}""";
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        if (!manifestEmpty)
        {
            state.Artifacts.PointAccountManifest =
                """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        }

        return state;
    }
}
