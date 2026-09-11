using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentTranscriptPersistenceTests
{
    [Fact]
    public void Assistant_embedded_result_materialized_as_tool_row_slim_envelope()
    {
        var big = new string('q', 3000);
        var assistant = new ChatMessage(ChatRole.Assistant, new List<AIContent>
        {
            new TextContent("Done validating."),
            new FunctionCallContent("c1", "validate_campaign", new Dictionary<string, object?>()),
            new FunctionResultContent("c1", new { body = big })
        });

        var satisfied = new HashSet<string>(StringComparer.Ordinal);
        long seq = 10;
        var rows = CampaignAgentTranscriptPersistence.BuildPersistenceRowsFromCompletionMessage(
            "t", "u", "conv", ref seq, null, satisfied, assistant);

        Assert.Equal(2, rows.Count);
        var tool = Assert.Single(rows, r => r.Role == "tool");
        Assert.Equal("c1", tool.ToolCallId);
        Assert.Equal("validate_campaign", tool.ToolName);
        Assert.Contains("body", tool.ToolResultJson!, StringComparison.Ordinal);

        var asst = Assert.Single(rows, r => r.Role == "assistant");
        Assert.DoesNotContain("functionResult", asst.Content!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("functionCall", asst.Content!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Done validating", asst.Content!, StringComparison.Ordinal);
    }

    [Fact]
    public void EventModels_phase_trims_long_assistant_narrative_at_persist()
    {
        var longText = new string('n', 10_000);
        var assistant = new ChatMessage(ChatRole.Assistant, new List<AIContent> { new TextContent(longText) });
        var satisfied = new HashSet<string>(StringComparer.Ordinal);
        long seq = 1;

        var rows = CampaignAgentTranscriptPersistence.BuildPersistenceRowsFromCompletionMessage(
            "t", "u", "conv", ref seq, null, satisfied, assistant,
            workflowPhase: CampaignWorkflowPhase.EventModels,
            maxPersistedAssistantNarrativeChars: 8192);

        var asst = Assert.Single(rows);
        Assert.Contains("[historyNarrativeTrimmed", asst.Content!, StringComparison.Ordinal);
        Assert.True(asst.Content!.Length < longText.Length);
    }

    [Fact]
    public void Verification_phase_digests_get_model_tool_result_at_persist()
    {
        var attrs = string.Join(',', Enumerable.Range(0, 30).Select(i =>
            $$"""{"symbol":"a{{i}}","dataType":"string","displayName":"Verbose {{i}}"}"""));
        var modelJson = $$"""{"id":"m1","name":"order","attributes":[{{attrs}}]}""";
        using var doc = JsonDocument.Parse(modelJson);
        var assistant = new ChatMessage(ChatRole.Assistant, new List<AIContent>
        {
            new FunctionCallContent("c1", "get_model", new Dictionary<string, object?>()),
            new FunctionResultContent("c1", doc.RootElement.Clone())
        });

        var satisfied = new HashSet<string>(StringComparer.Ordinal);
        long seq = 1;
        var rows = CampaignAgentTranscriptPersistence.BuildPersistenceRowsFromCompletionMessage(
            "t", "u", "conv", ref seq, null, satisfied, assistant,
            workflowPhase: CampaignWorkflowPhase.Verification);

        var tool = Assert.Single(rows, r => r.Role == "tool");
        Assert.Equal("get_model", tool.ToolName);
        Assert.Contains("attributesTruncated", tool.ToolResultJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("displayName", tool.ToolResultJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventModels_phase_digests_bloated_get_model_tool_result_at_persist()
    {
        var attrs = string.Join(',', Enumerable.Range(0, 30).Select(i =>
            $$"""{"symbol":"a{{i}}","dataType":"string","displayName":"Verbose {{i}}"}"""));
        var modelJson = $$"""{"id":"m1","name":"order","attributes":[{{attrs}}]}""";
        using var doc = JsonDocument.Parse(modelJson);
        var assistant = new ChatMessage(ChatRole.Assistant, new List<AIContent>
        {
            new FunctionCallContent("c1", "get_model", new Dictionary<string, object?>()),
            new FunctionResultContent("c1", doc.RootElement.Clone())
        });

        var satisfied = new HashSet<string>(StringComparer.Ordinal);
        long seq = 1;
        var rows = CampaignAgentTranscriptPersistence.BuildPersistenceRowsFromCompletionMessage(
            "t", "u", "conv", ref seq, null, satisfied, assistant,
            workflowPhase: CampaignWorkflowPhase.EventModels);

        var tool = Assert.Single(rows, r => r.Role == "tool");
        Assert.Equal("get_model", tool.ToolName);
        Assert.True(tool.ToolResultJson!.Length < modelJson.Length);
        Assert.Contains("attributesTruncated", tool.ToolResultJson!, StringComparison.Ordinal);
        Assert.True(tool.ToolResultJson!.Length <= ModelReadDigester.CreationMaxChars + 200);
    }

    [Fact]
    public void EventModels_phase_digests_small_get_model_with_creation_slim()
    {
        var modelJson = """{"id":"m1","name":"order","modelType":"loyalty","attributes":[{"symbol":"ordertotal","dataType":"decimal","displayName":"Total"}]}""";
        using var doc = JsonDocument.Parse(modelJson);
        var assistant = new ChatMessage(ChatRole.Assistant, new List<AIContent>
        {
            new FunctionCallContent("c1", "get_model", new Dictionary<string, object?>()),
            new FunctionResultContent("c1", doc.RootElement.Clone())
        });

        var satisfied = new HashSet<string>(StringComparer.Ordinal);
        long seq = 1;
        var rows = CampaignAgentTranscriptPersistence.BuildPersistenceRowsFromCompletionMessage(
            "t", "u", "conv", ref seq, null, satisfied, assistant,
            workflowPhase: CampaignWorkflowPhase.EventModels);

        var tool = Assert.Single(rows, r => r.Role == "tool");
        Assert.Contains("Model digested for context efficiency", tool.ToolResultJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("displayName", tool.ToolResultJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProposeBrief_slimmed_when_bloated_at_persist()
    {
        var brief = new
        {
            objective = "Grow tier adoption",
            campaignClass = "event-driven",
            captured = true,
            narrative = new string('x', 2000)
        };
        var assistant = new ChatMessage(ChatRole.Assistant, new List<AIContent>
        {
            new FunctionCallContent("c1", "ProposeCampaignDesignBrief", new Dictionary<string, object?>()),
            new FunctionResultContent("c1", brief)
        });

        var satisfied = new HashSet<string>(StringComparer.Ordinal);
        long seq = 1;
        var rows = CampaignAgentTranscriptPersistence.BuildPersistenceRowsFromCompletionMessage(
            "t", "u", "conv", ref seq, null, satisfied, assistant,
            workflowPhase: CampaignWorkflowPhase.DataAnalysis);

        var tool = Assert.Single(rows, r => r.Role == "tool");
        Assert.Contains("Grow tier adoption", tool.ToolResultJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("narrative", tool.ToolResultJson!, StringComparison.OrdinalIgnoreCase);
    }
}
