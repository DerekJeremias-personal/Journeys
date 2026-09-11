using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class CreationSnapshotArtifactTests
{
    [Fact]
    public void MergePatFromUpsertResult_dedupes_by_id()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        const string pat = """{"id":"pat-1","name":"Spendable","ledgerType":"Spendable"}""";

        CreationSnapshotArtifact.MergePatFromUpsertResult(state, pat);
        CreationSnapshotArtifact.MergePatFromUpsertResult(state, pat);

        var snap = CreationSnapshotArtifact.Read(state);
        Assert.Single(snap!.PointAccountTypes);
        Assert.Equal("pat-1", snap.PointAccountTypes[0].Id);
    }

    [Fact]
    public void MergeFromJourneyUpsert_sets_creation_complete_when_pat_present()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CreationSnapshot = JsonSerializer.Serialize(new CampaignCreationSnapshotDto
        {
            PointAccountTypes = [new PointAccountSnapshotItemDto { Id = "pat-1" }]
        });

        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "camp-1", "Draft", ruleSetCount: 2, outcomeCount: 3);

        var snap = CreationSnapshotArtifact.Read(state);
        Assert.True(snap!.CreationComplete);
        Assert.Equal("camp-1", snap.CampaignId);
        Assert.Equal(2, snap.JourneyRuleSetCount);
    }

    [Fact]
    public void Clear_removes_snapshot_and_deferred_flag()
    {
        var artifacts = new CampaignWorkflowArtifactsDocument
        {
            CreationSnapshot = """{"creationComplete":true}""",
            DeferredMutatorRetry = true
        };

        CreationSnapshotArtifact.Clear(artifacts);

        Assert.Null(artifacts.CreationSnapshot);
        Assert.False(artifacts.DeferredMutatorRetry);
    }
}
