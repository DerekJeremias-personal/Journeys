using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public sealed class ModelCatalogRetainSpecBuilderTests
{
    [Fact]
    public void Build_extracts_primary_campaign_event_model_ids()
    {
        var callId = "call-1";
        var campaignId = Guid.NewGuid().ToString();
        var envelope = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(callId, "UpsertCampaign", new Dictionary<string, object?>
                    {
                        ["tenantId"] = "t1",
                        ["campaignJson"] = $$"""{"id":"{{campaignId}}","name":"B2B Tier Rewards Program","status":"Draft","events":["order-1","lad-1"]}"""
                    })
                ]));

        var messages = new List<AgentMessage>
        {
            new("t1", "u1", "conv1", 1, "user", "create campaign", null, null, null, null, null),
            new("t1", "u1", "conv1", 2, "assistant", envelope, null, null, null, null, null),
            new("t1", "u1", "conv1", 3, "tool", "", null, callId, null, null,
                $$"""{"id":"{{campaignId}}","name":"B2B Tier Rewards Program","status":"draft","events":["order-1","lad-1"]}""")
        };

        var spec = ModelCatalogRetainSpecBuilder.Build(
            messages, ModelCatalogRetainSpec.DefaultCatalogOnlyModelNames);

        Assert.Contains("order-1", spec.RetainModelIds);
        Assert.Contains("lad-1", spec.RetainModelIds);
        Assert.Equal(ModelCatalogRetainSpec.DefaultCatalogOnlyModelNames, spec.CatalogOnlyModelNames);
    }

    [Fact]
    public void Build_returns_empty_retain_ids_when_no_campaign()
    {
        var messages = new List<AgentMessage>();

        var spec = ModelCatalogRetainSpecBuilder.Build(
            messages, ModelCatalogRetainSpec.DefaultCatalogOnlyModelNames);

        Assert.Empty(spec.RetainModelIds);
    }
}
