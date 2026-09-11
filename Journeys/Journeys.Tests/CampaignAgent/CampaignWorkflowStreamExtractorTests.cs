using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignWorkflowStreamExtractorTests
{
    [Fact]
    public void ExtractFromStreamedUpdates_StringResult_YieldsRawJsonObject()
    {
        const string callId = "call-1";
        const string modelJson = """{"id":"m1","name":"Order"}""";

        var updates = new List<ChatResponseUpdate>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "GetModel", new Dictionary<string, object?>())
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                // MCP transports commonly deliver the tool result as a JSON string.
                new FunctionResultContent(callId, modelJson)
            })
        };

        var results = CampaignWorkflowStreamExtractor.ExtractFromStreamedUpdates(updates);

        var (name, json) = Assert.Single(results);
        Assert.Equal("GetModel", name);

        // Must be the real object JSON, not a re-encoded/escaped quoted string.
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("m1", doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void ExtractFromStreamedUpdates_ObjectResult_YieldsJsonObject()
    {
        const string callId = "call-2";

        var updates = new List<ChatResponseUpdate>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "UpsertPointAccountType", new Dictionary<string, object?>())
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent(callId, new { id = "pat1", name = "Spendable" })
            })
        };

        var results = CampaignWorkflowStreamExtractor.ExtractFromStreamedUpdates(updates);

        var (name, json) = Assert.Single(results);
        Assert.Equal("UpsertPointAccountType", name);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("pat1", doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void ExtractFromStreamedUpdates_TextContentResult_IsUnwrappedToObject()
    {
        const string callId = "call-3";
        const string modelJson = """{"id":"a17daa79","name":"Order"}""";

        var updates = new List<ChatResponseUpdate>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "get_model", new Dictionary<string, object?>())
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                // MCP tool results commonly surface as a MEAI TextContent, which serializes to a
                // {"$type":"text","text":"<json>"} envelope that the workflow gates must unwrap.
                new FunctionResultContent(callId, new TextContent(modelJson))
            })
        };

        var results = CampaignWorkflowStreamExtractor.ExtractFromStreamedUpdates(updates);

        var (name, json) = Assert.Single(results);
        Assert.Equal("get_model", name);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("a17daa79", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("Order", doc.RootElement.GetProperty("name").GetString());
    }
}
