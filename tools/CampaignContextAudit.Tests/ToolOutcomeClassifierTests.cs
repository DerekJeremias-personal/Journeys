using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class ToolOutcomeClassifierTests
{
    [Fact]
    public void Classifies_mcp_invocation_error_from_isError_flag()
    {
        var json = """{"content":[{"type":"text","text":"An error occurred invoking 'upsert_campaign'."}],"isError":true}""";
        Assert.Equal(ToolOutcomeKind.McpInvocationError, ToolOutcomeClassifier.Classify("upsert_campaign", json));
    }

    [Fact]
    public void Classifies_validation_errors_from_non_empty_errors_array()
    {
        var json = """{"errors":[{"code":"JOURNEY_NAV_MISSING","message":"bad nav"}]}""";
        Assert.Equal(ToolOutcomeKind.ValidationErrors, ToolOutcomeClassifier.Classify("validate_campaign", json));
    }

    [Fact]
    public void Classifies_mutation_digest_when_campaign_id_present()
    {
        var json = """{"$type":"text","text":"{\"campaignId\":\"abc\",\"ruleSetCount\":0}"}""";
        Assert.Equal(ToolOutcomeKind.MutationDigest, ToolOutcomeClassifier.Classify("upsert_campaign", json));
    }

    [Fact]
    public void Classifies_validate_ack_for_clean_short_validate_result()
    {
        var json = """{"$type":"text","text":"{\"IsValid\":true,\"Validation\":{\"Errors\":[]}}"}""";
        Assert.Equal(ToolOutcomeKind.ValidateAck, ToolOutcomeClassifier.Classify("validate_campaign", json));
    }

    [Fact]
    public void Classifies_unknown_for_empty_tool_row()
    {
        var row = new AgentMessageDoc { Role = "tool", ToolName = "list_models" };
        Assert.Equal(ToolOutcomeKind.Unknown, ToolOutcomeClassifier.Classify(row));
    }

    [Fact]
    public void Classifies_unknown_for_json_string_tool_error_body()
    {
        const string json = "\"Error: Requested function \\\"upsert_campaign\\\" not found.\"";
        Assert.Equal(ToolOutcomeKind.Unknown, ToolOutcomeClassifier.Classify("upsert_campaign", json));
    }

    [Fact]
    public void Classifies_unknown_when_validation_property_is_string_not_object()
    {
        var json = """{"IsValid":false,"Validation":"unexpected string"}""";
        Assert.Equal(ToolOutcomeKind.Unknown, ToolOutcomeClassifier.Classify("validate_campaign", json));
    }
}
