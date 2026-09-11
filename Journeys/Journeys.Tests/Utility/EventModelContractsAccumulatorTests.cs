using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelContractsAccumulatorTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void TryMergeFromToolResult_two_merges_yields_array_length_two()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");

        Assert.True(EventModelContractsAccumulator.TryMergeFromToolResult(state, ModelToolJson("idA", "OrderA")));
        Assert.True(EventModelContractsAccumulator.TryMergeFromToolResult(state, ModelToolJson("idB", "OrderB")));

        var digests = EventModelContractsAccumulator.Read(state);
        Assert.Equal(2, digests.Count);
        Assert.Contains(digests, d => d.EventModelId == "idA");
        Assert.Contains(digests, d => d.EventModelId == "idB");
    }

    [Fact]
    public void TryMergeFromToolResult_get_model_writes_discovered_not_resolved()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var json = IncompleteModelToolJson("idA", "OrderA");

        Assert.True(EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", json));

        Assert.Empty(EventModelContractsAccumulator.ReadResolved(state));
        var discovered = EventModelContractsAccumulator.ReadDiscovered(state);
        Assert.Single(discovered);
        Assert.Equal("idA", discovered[0].EventModelId);
    }

    [Fact]
    public void TryMergeFromToolResult_get_model_promotes_complete_contract_to_resolved()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var json = ModelToolJson("idA", "OrderA");

        Assert.True(EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", json));

        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));
        Assert.Equal("idA", EventModelContractsAccumulator.ReadResolved(state)[0].EventModelId);
    }

    [Fact]
    public void TryMergeFromToolResult_get_model_promotes_to_resolved_when_pending_spec_matches()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        PendingEventModelSpecArtifact.Write(state, new PendingEventModelSpecDto { Name = "Review" });
        var json = ModelToolJson("fbfe5201-d2e2-4ea4-9fc9-e79343adcfc1", "Review", tag: "eventable");

        Assert.True(EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", json));

        Assert.True(PendingEventModelSpecArtifact.IsSatisfied(state));
        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));
        Assert.Equal("Review", EventModelContractsAccumulator.ReadResolved(state)[0].EventModelName);
    }

    [Fact]
    public void TryMergeFromToolResult_get_model_does_not_promote_when_name_mismatches_pending_spec()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        PendingEventModelSpecArtifact.Write(state, new PendingEventModelSpecDto { Name = "Review" });
        var json = ModelToolJson("d1", "LoyaltyAccountDetails", tag: "eventable");

        Assert.True(EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", json));

        Assert.False(PendingEventModelSpecArtifact.IsSatisfied(state));
        Assert.Empty(EventModelContractsAccumulator.ReadResolved(state));
    }

    [Fact]
    public void TryMergeFromToolResult_save_model_non_eventable_does_not_write_resolved()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var json = ModelToolJson("idA", "OrderA", tag: null);

        Assert.False(EventModelContractsAccumulator.TryMergeFromToolResult(state, "save_model", json));

        Assert.Empty(EventModelContractsAccumulator.ReadResolved(state));
    }

    [Fact]
    public void TryMergeFromToolResult_save_model_writes_resolved()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var json = ModelToolJson("idA", "OrderA");

        Assert.True(EventModelContractsAccumulator.TryMergeFromToolResult(state, "save_model", json));

        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));
        Assert.Empty(EventModelContractsAccumulator.ReadDiscovered(state));
    }

    [Fact]
    public void ShouldAdvanceToCampaignSetup_planned_two_only_one_merged_returns_false()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = BriefWithPlannedIds(["idA", "idB"]);

        EventModelContractsAccumulator.TryMergeFromToolResult(state, ModelToolJson("idA", "OrderA"));

        Assert.False(EventModelContractsAccumulator.ShouldAdvanceToCampaignSetup(state, userMessage: null));
    }

    [Fact]
    public void ShouldAdvanceToCampaignSetup_planned_two_both_merged_returns_true()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = BriefWithPlannedIds(["idA", "idB"]);

        EventModelContractsAccumulator.TryMergeFromToolResult(state, ModelToolJson("idA", "OrderA"));
        EventModelContractsAccumulator.TryMergeFromToolResult(state, ModelToolJson("idB", "OrderB"));

        Assert.True(EventModelContractsAccumulator.ShouldAdvanceToCampaignSetup(state, userMessage: null));
    }

    [Fact]
    public void ShouldAdvanceToCampaignSetup_returns_false_for_discovery_only()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = BriefWithPlannedIds(["idA"]);

        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", IncompleteModelToolJson("idA", "OrderA"));

        Assert.False(EventModelContractsAccumulator.ShouldAdvanceToCampaignSetup(state, userMessage: null));
    }

    [Fact]
    public void Read_legacy_single_event_model_contract_returns_one_item()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var digest = new EventProcessingContractDigest
        {
            EventModelId = "legacy-1",
            EventModelName = "LegacyOrder"
        };
        state.Artifacts.EventModelContract = JsonSerializer.Serialize(digest, JsonOpts);

        var digests = EventModelContractsAccumulator.Read(state);
        Assert.Single(digests);
        Assert.Equal("legacy-1", digests[0].EventModelId);
    }

    private static string BriefWithPlannedIds(IEnumerable<string> ids) =>
        JsonSerializer.Serialize(new { version = 1, plannedEventModelIds = ids }, JsonOpts);

    private static string IncompleteModelToolJson(string id, string name) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            tag = "eventable",
            modelType = "event"
        }, JsonOpts);

    private static string ModelToolJson(string id, string name, string? tag = "eventable") =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            tag,
            modelType = "event",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            attributes = new[] { new { symbol = "orderid", type = "Primitive", dataType = "string", displayName = "Id" } }
        }, JsonOpts);
}
