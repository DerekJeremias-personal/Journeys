using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class McpHostFailureParserTests
{
    [Fact]
    public void TryParse_mcp_isError_content_envelope()
    {
        var json =
            """{"content":[{"type":"text","text":"An error occurred invoking 'process_event'."}],"isError":true}""";

        var ok = McpHostFailureParser.TryParse("process_event", json, out var failure);

        Assert.True(ok);
        Assert.NotNull(failure);
        Assert.Equal(BackendToolFailureParseStatus.Success, failure!.ParseStatus);
        Assert.Contains("process_event", failure.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_plain_invocation_error_text()
    {
        var ok = McpHostFailureParser.TryParse("SaveModel", "An error occurred invoking 'save_model'.", out var failure);

        Assert.True(ok);
        Assert.NotNull(failure);
        Assert.Contains("save_model", failure!.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_false_for_success_json()
    {
        var ok = McpHostFailureParser.TryParse("ProcessEvent", """{"id":"evt1"}""", out _);
        Assert.False(ok);
    }
}
