using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class PendingEventModelSpecArtifactTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void TryCapture_from_review_message_writes_spec()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var msg = "create a model called Review and give it String CustomerId, String Title";
        Assert.True(PendingEventModelSpecArtifact.TryCaptureFromUserMessage(state, msg));
        var spec = PendingEventModelSpecArtifact.Read(state);
        Assert.Equal("Review", spec!.Name);
    }

    [Fact]
    public void IsSatisfied_when_resolved_contract_matches_name()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        PendingEventModelSpecArtifact.Write(state, new PendingEventModelSpecDto { Name = "Review" });
        var digest = new EventProcessingContractDigest
        {
            EventModelId = "review-1",
            EventModelName = "Review"
        };
        state.Artifacts.ResolvedEventModelContracts =
            JsonSerializer.Serialize(new[] { digest }, JsonOpts);
        Assert.True(PendingEventModelSpecArtifact.IsSatisfied(state));
    }
}
