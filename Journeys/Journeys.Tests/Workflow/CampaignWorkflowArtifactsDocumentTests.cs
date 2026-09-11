using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowArtifactsDocumentTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Round_trips_new_step_manager_fields()
    {
        var original = new CampaignWorkflowArtifactsDocument
        {
            PendingEventModelSpec = """{"schemaVersion":1,"name":"Review"}""",
            EventModelSaveFailureCount = 3,
            ResolvedEventModelContracts = """[{"eventModelId":"r1","eventModelName":"Review"}]""",
            DiscoveredEventModelContracts = """[{"eventModelId":"d1","eventModelName":"LoyaltyAccountDetails"}]"""
        };

        var json = JsonSerializer.Serialize(original, JsonOpts);
        var roundTripped = JsonSerializer.Deserialize<CampaignWorkflowArtifactsDocument>(json, JsonOpts);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.PendingEventModelSpec, roundTripped.PendingEventModelSpec);
        Assert.Equal(original.EventModelSaveFailureCount, roundTripped.EventModelSaveFailureCount);
        Assert.Equal(original.ResolvedEventModelContracts, roundTripped.ResolvedEventModelContracts);
        Assert.Equal(original.DiscoveredEventModelContracts, roundTripped.DiscoveredEventModelContracts);
    }
}
