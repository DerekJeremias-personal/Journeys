using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public sealed class CampaignAgentThreadContextBuilderTests
{
    [Fact]
    public void Build_includes_successful_upsert_campaign_draft()
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
                        ["campaignJson"] = $$"""{"id":"{{campaignId}}","name":"B2B Tier Rewards Program","status":"Draft"}"""
                    })
                ]));

        var messages = new List<AgentMessage>
        {
            new("t1", "u1", "conv1", 1, "user", "create campaign", null, null, null, null, null),
            new("t1", "u1", "conv1", 2, "assistant", envelope, null, null, null, null, null),
            new("t1", "u1", "conv1", 3, "tool", "", null, callId, null, null,
                $$"""{"id":"{{campaignId}}","name":"B2B Tier Rewards Program","status":"draft"}""")
        };

        var ctx = CampaignAgentThreadContextBuilder.Build(messages);

        Assert.NotNull(ctx.PrimaryCampaign);
        Assert.Equal(campaignId, ctx.PrimaryCampaign!.CampaignId);
        Assert.Equal("Draft", ctx.PrimaryCampaign.Status, ignoreCase: true);
        Assert.Contains("B2B Tier Rewards Program", ctx.ToPromptBlock());
        Assert.Contains("THREAD CONTEXT", ctx.ToPromptBlock());
    }

    [Fact]
    public void Build_excludes_failed_upsert()
    {
        var callId = "call-fail";
        var envelope = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(callId, "UpsertCampaign", new Dictionary<string, object?>
                    {
                        ["campaignJson"] = """{"id":"x","name":"Bad","status":"Draft"}"""
                    })
                ]));

        var messages = new List<AgentMessage>
        {
            new("t1", "u1", "conv1", 1, "assistant", envelope, null, null, null, null, null),
            new("t1", "u1", "conv1", 2, "tool", "", null, callId, null, null, """{"errors":{"validation":"failed"}}""")
        };

        var ctx = CampaignAgentThreadContextBuilder.Build(messages);
        Assert.True(ctx.IsEmpty);
    }

    [Fact]
    public void Build_last_upsert_wins_for_same_campaign_id()
    {
        var callId1 = "call-a";
        var callId2 = "call-b";
        var campaignId = Guid.NewGuid().ToString();

        var envelope1 = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(callId1, "UpsertCampaign", new Dictionary<string, object?>
                    {
                        ["campaignJson"] = $$"""{"id":"{{campaignId}}","name":"First Name","status":"Draft"}"""
                    })
                ]));

        var envelope2 = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(callId2, "UpsertCampaign", new Dictionary<string, object?>
                    {
                        ["campaignJson"] = $$"""{"id":"{{campaignId}}","name":"B2B Tier Rewards Program","status":"Draft"}"""
                    })
                ]));

        var messages = new List<AgentMessage>
        {
            new("t1", "u1", "conv1", 1, "assistant", envelope1, null, null, null, null, null),
            new("t1", "u1", "conv1", 2, "tool", "", null, callId1, null, null,
                $$"""{"id":"{{campaignId}}","name":"First Name","status":"draft"}"""),
            new("t1", "u1", "conv1", 3, "assistant", envelope2, null, null, null, null, null),
            new("t1", "u1", "conv1", 4, "tool", "", null, callId2, null, null,
                $$"""{"id":"{{campaignId}}","name":"B2B Tier Rewards Program","status":"draft"}""")
        };

        var ctx = CampaignAgentThreadContextBuilder.Build(messages);

        Assert.Equal("B2B Tier Rewards Program", ctx.PrimaryCampaign!.Name);
        Assert.Single(ctx.Campaigns);
    }

    [Fact]
    public void Build_excludes_list_campaigns_only()
    {
        var callId = "call-list";
        var envelope = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(callId, "ListCampaigns", new Dictionary<string, object?>
                    {
                        ["tenantId"] = "t1",
                        ["status"] = "Live"
                    })
                ]));

        var messages = new List<AgentMessage>
        {
            new("t1", "u1", "conv1", 1, "assistant", envelope, null, null, null, null, null),
            new("t1", "u1", "conv1", 2, "tool", "", null, callId, null, null,
                """{"entities":[],"count":0}""")
        };

        var ctx = CampaignAgentThreadContextBuilder.Build(messages);
        Assert.True(ctx.IsEmpty);
    }

    [Fact]
    public void ToPromptBlock_contains_draft_listing_guidance()
    {
        var callId = "call-1";
        var campaignId = Guid.NewGuid().ToString();
        var envelope = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(callId, "UpsertCampaign", new Dictionary<string, object?>
                    {
                        ["campaignJson"] = $$"""{"id":"{{campaignId}}","name":"Test","status":"Draft"}"""
                    })
                ]));

        var messages = new List<AgentMessage>
        {
            new("t1", "u1", "conv1", 1, "assistant", envelope, null, null, null, null, null),
            new("t1", "u1", "conv1", 2, "tool", "", null, callId, null, null,
                $$"""{"id":"{{campaignId}}","name":"Test","status":"draft"}""")
        };

        var block = CampaignAgentThreadContextBuilder.Build(messages).ToPromptBlock();

        Assert.Contains("do not call list_campaigns", block);
        Assert.Contains("Primary campaign for edits", block);
    }
}
