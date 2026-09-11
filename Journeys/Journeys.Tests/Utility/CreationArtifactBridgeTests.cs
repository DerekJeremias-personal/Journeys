using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class CreationArtifactBridgeTests
{
    private const string FullCampaignJson = """
    {
      "id": "camp-full",
      "name": "Summer Promo",
      "status": "Draft",
      "startDate": "2025-06-01T00:00:00Z",
      "events": ["evt-a"],
      "journey": {
        "name": "Main",
        "children": [
          {
            "name": "Earn",
            "children": [],
            "rules": [
              {
                "name": "Bronze",
                "ruleJsonElement": { "kind": "NumericPropertyRule" },
                "outcomesJsonElement": [ { "kind": "DepositPointsOutcome" } ]
              }
            ]
          }
        ]
      }
    }
    """;

    [Fact]
    public void TryApplyUpsertOutcome_full_campaign_updates_journey_digest()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Spendable","role":"spendable"}]}""";

        var outcome = CreationArtifactBridge.TryApplyUpsertOutcome(state, FullCampaignJson);

        Assert.True(outcome.JourneyDigestApplied);
        Assert.False(string.IsNullOrWhiteSpace(state.Artifacts.JourneyDigestProposed));
        Assert.Equal(state.Artifacts.JourneyDigestProposed, state.Artifacts.JourneyDigestApproved);
        Assert.Contains("camp-full", state.Artifacts.JourneyDigestProposed!, StringComparison.Ordinal);
    }

    private const string MutationSourceJson = """
    {
      "Id": "camp-full",
      "ExtCampaignId": "tiered loyalty program",
      "Status": "draft",
      "Name": "tiered loyalty program",
      "Events": ["a17daa79-8908-4e89-9fa2-6e5f629a2202"],
      "Journey": {
        "Name": "Tiered Earning Journey",
        "Children": [{
          "Name": "Tier-Based Earning Node",
          "Children": [],
          "Rules": [{
            "Name": "Bronze Tier Earning",
            "RuleJsonElement": { "Kind": "NumericPropertyRule" },
            "OutcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
          }]
        }]
      }
    }
    """;

    [Fact]
    public void TryApplyUpsertOutcome_mutation_ack_synthesizes_journey_and_snapshot()
    {
        var digest = CampaignMutationDigester.Digest(MutationSourceJson);
        Assert.True(digest.Transformed);

        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Spendable","role":"spendable"}]}""";

        var outcome = CreationArtifactBridge.TryApplyUpsertOutcome(state, digest.Json);

        Assert.True(outcome.JourneyDigestApplied);
        Assert.False(string.IsNullOrWhiteSpace(state.Artifacts.JourneyDigestProposed));

        using var journeyDoc = JsonDocument.Parse(state.Artifacts.JourneyDigestProposed!);
        Assert.Equal(1, journeyDoc.RootElement.GetProperty("ruleSetCount").GetInt32());

        var snap = CreationSnapshotArtifact.Read(state);
        Assert.NotNull(snap);
        Assert.Equal("camp-full", snap!.CampaignId);
        Assert.Equal(1, snap.JourneyRuleSetCount);
        Assert.True(snap.CreationComplete);
    }

    [Fact]
    public void TryApplyUpsertOutcome_failure_passthrough_does_not_set_journey()
    {
        const string failure = """{"errors":{"journey.validation.0":"bad"}}""";
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");

        var outcome = CreationArtifactBridge.TryApplyUpsertOutcome(state, failure);

        Assert.False(outcome.JourneyDigestApplied);
        Assert.Null(state.Artifacts.JourneyDigestProposed);
    }
}
