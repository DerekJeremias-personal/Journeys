using System.Text.Json;
using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public class AgentConversationManifestParserTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void BuildManifest_UpsertCampaign_from_function_call_args()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var campaign = new CampaignDto
        {
            Id = "camp-1",
            Status = "Draft",
            Name = "Test",
            StartDate = start,
        };
        var campaignJson = JsonSerializer.Serialize(campaign, JsonOpts);
        var assistant = AssistantWithFunctionCall(
            "u1",
            "conv1",
            0,
            "call-1",
            "UpsertCampaign",
            new Dictionary<string, object?>
            {
                ["tenantId"] = "ten-1",
                ["campaignJson"] = campaignJson,
            });

        var tool = new AgentMessage(
            "ten-1",
            "u1",
            "conv1",
            1,
            "tool",
            string.Empty,
            null,
            "call-1",
            null,
            null,
            /* toolResultJson */ """{"id":"camp-1","status":"Draft"}""");

        var m = AgentConversationManifestParser.BuildManifest(new[] { assistant, tool });

        Assert.Single(m.Campaigns);
        Assert.Equal("camp-1", m.Campaigns[0].CampaignId);
        Assert.Equal("Draft", m.Campaigns[0].Status);
    }

    [Fact]
    public void BuildManifest_DeleteCampaign_from_function_call_args()
    {
        var assistant = AssistantWithFunctionCall(
            "u1",
            "conv1",
            0,
            "call-del-1",
            "DeleteCampaign",
            new Dictionary<string, object?>
            {
                ["tenantId"] = "ten-1",
                ["campaignId"] = "camp-del",
                ["status"] = "Draft",
            });

        var m = AgentConversationManifestParser.BuildManifest(new[] { assistant });

        Assert.Single(m.Campaigns);
        Assert.Equal("camp-del", m.Campaigns[0].CampaignId);
        Assert.Equal("Draft", m.Campaigns[0].Status);
    }

    [Fact]
    public void BuildManifest_UpsertPointAccountType_from_function_call_args()
    {
        var pat = new PointAccountTypeDto
        {
            Id = "pat-1",
            Name = "PAT",
            Status = "Active",
            LedgerType = "TQP",
            RoundingDecimalPlaces = 0,
        };
        var patJson = JsonSerializer.Serialize(pat, JsonOpts);
        var assistant = AssistantWithFunctionCall(
            "u1",
            "conv1",
            0,
            "call-2",
            "UpsertPointAccountType",
            new Dictionary<string, object?>
            {
                ["tenantId"] = "ten-1",
                ["pointAccountTypeJson"] = patJson,
            });

        var m = AgentConversationManifestParser.BuildManifest(new[] { assistant });

        Assert.Single(m.PointAccountTypeIds);
        Assert.Equal("pat-1", m.PointAccountTypeIds[0]);
    }

    [Fact]
    public void BuildManifest_SaveModel_from_args_and_result()
    {
        var assistant = AssistantWithFunctionCall(
            "u1",
            "conv1",
            0,
            "call-3",
            "SaveModel",
            new Dictionary<string, object?>
            {
                ["tenantId"] = "ten-1",
                ["modelName"] = "Order",
                ["entityJson"] = """{"id":"e-1","total":10}""",
            });

        var tool = new AgentMessage(
            "ten-1",
            "u1",
            "conv1",
            1,
            "tool",
            string.Empty,
            null,
            "call-3",
            null,
            null,
            """{"id":"e-1"}""");

        var m = AgentConversationManifestParser.BuildManifest(new[] { assistant, tool });

        Assert.Single(m.SaveModelEntities);
        Assert.Equal("Order", m.SaveModelEntities[0].ModelName);
        Assert.Equal("e-1", m.SaveModelEntities[0].EntityId);
    }

    [Fact]
    public void BuildManifest_empty_for_plain_text_assistant()
    {
        var m = new AgentMessage(
            "t1",
            "u1",
            "c1",
            0,
            "assistant",
            "Hello, no tools here.",
            null,
            null,
            null,
            null,
            null);

        var manifest = AgentConversationManifestParser.BuildManifest(new[] { m });
        Assert.Empty(manifest.Campaigns);
        Assert.Empty(manifest.PointAccountTypeIds);
        Assert.Empty(manifest.SaveModelEntities);
    }

    private static AgentMessage AssistantWithFunctionCall(
        string ownerUserId,
        string conversationId,
        long sequence,
        string callId,
        string toolName,
        Dictionary<string, object?> args)
    {
        var fc = new FunctionCallContent(callId, toolName, args);
        var msg = new ChatMessage(ChatRole.Assistant, [fc]);
        var content = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(msg);
        return new AgentMessage(
            "t1",
            ownerUserId,
            conversationId,
            sequence,
            "assistant",
            content,
            null,
            null,
            null,
            null,
            null);
    }
}
