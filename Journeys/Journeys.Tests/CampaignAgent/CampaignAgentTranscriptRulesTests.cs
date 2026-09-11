using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public sealed class CampaignAgentTranscriptRulesTests
{
    [Fact]
    public void SanitizeChatMessages_StripsAssistantEmbeddedResult_WhenToolRowExists()
    {
        const string callId = "toolu_dup1";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "SaveModel", new Dictionary<string, object?>()),
                new FunctionResultContent(callId, new { error = "validation" })
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent(callId, new { error = "validation" })
            })
        };

        var sanitized = CampaignAgentTranscriptRules.SanitizeChatMessages(messages);

        var assistant = Assert.Single(sanitized, m => m.Role == ChatRole.Assistant);
        Assert.Contains(assistant.Contents, c => c is FunctionCallContent);
        Assert.DoesNotContain(assistant.Contents, c => c is FunctionResultContent);

        var tool = Assert.Single(sanitized, m => m.Role == ChatRole.Tool);
        var result = Assert.IsType<FunctionResultContent>(Assert.Single(tool.Contents));
        Assert.Equal(callId, result.CallId);
    }

    [Fact]
    public void SanitizeChatMessages_KeepsAssistantEmbeddedResult_WhenNoToolRow()
    {
        const string callId = "toolu_assist_only";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "SaveModel", new Dictionary<string, object?>()),
                new FunctionResultContent(callId, new { ok = false })
            })
        };

        var sanitized = CampaignAgentTranscriptRules.SanitizeChatMessages(messages);

        var assistant = Assert.Single(sanitized);
        Assert.Contains(assistant.Contents, c => c is FunctionResultContent);
    }

    [Fact]
    public void SanitizeChatMessages_PreservesCallOnlyAssistantPlusToolResult()
    {
        const string callId = "toolu_ok1";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "SaveModel", new Dictionary<string, object?>())
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent(callId, new { ok = true })
            })
        };

        var sanitized = CampaignAgentTranscriptRules.SanitizeChatMessages(messages);

        Assert.Equal(2, sanitized.Count);
        Assert.Contains(sanitized, m => m.Role == ChatRole.Assistant);
        Assert.Contains(sanitized, m => m.Role == ChatRole.Tool);
    }

    [Fact]
    public void SanitizeChatMessages_ResetsAssistantResultTrackingOnUserMessage()
    {
        const string callId = "toolu_seg";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "SaveModel", new Dictionary<string, object?>()),
                new FunctionResultContent(callId, new { pass = 1 })
            }),
            new(ChatRole.User, "next turn"),
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent(callId, new { pass = 2 })
            })
        };

        var sanitized = CampaignAgentTranscriptRules.SanitizeChatMessages(messages);

        var tool = Assert.Single(sanitized, m => m.Role == ChatRole.Tool);
        Assert.IsType<FunctionResultContent>(Assert.Single(tool.Contents));
    }

    [Fact]
    public void SanitizeChatMessages_StripsSecondToolRow_ForSameCallId()
    {
        const string callId = "toolu_dup_tool_rows";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "list_campaigns", new Dictionary<string, object?>())
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent(callId, new { count = 0 })
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent(callId, new { count = 0 })
            })
        };

        var sanitized = CampaignAgentTranscriptRules.SanitizeChatMessages(messages);

        Assert.Single(sanitized, m => m.Role == ChatRole.Tool);
    }

    [Fact]
    public void CallIdAlreadyHasFunctionResult_ReturnsTrueWhenPriorToolRowHasResult()
    {
        const string callId = "toolu_replay_tool";
        var prior = new List<ChatMessage>
        {
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent(callId, new { ok = true })
            })
        };

        Assert.True(CampaignAgentTranscriptRules.CallIdAlreadyHasFunctionResult(prior, callId));
    }

    [Fact]
    public void AssistantAlreadyHasFunctionResult_ReturnsTrueWhenPriorAssistantHasResult()
    {
        const string callId = "toolu_replay";
        var prior = new List<ChatMessage>
        {
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent(callId, "SaveModel", new Dictionary<string, object?>()),
                new FunctionResultContent(callId, new { error = "x" })
            })
        };

        Assert.True(CampaignAgentTranscriptRules.AssistantAlreadyHasFunctionResult(prior, callId));
    }
}
