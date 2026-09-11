using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class ToolResultSuccessEvaluatorTests
{
    [Fact]
    public void LooksSuccessful_false_when_errors_object()
    {
        Assert.False(ToolResultSuccessEvaluator.LooksSuccessful("""{"errors":{"x":"y"}}"""));
    }

    [Fact]
    public void LooksSuccessful_true_when_plain_success_json()
    {
        Assert.True(ToolResultSuccessEvaluator.LooksSuccessful("""{"id":"m1","name":"Order"}"""));
    }

    [Fact]
    public void LooksSuccessful_false_when_error_true()
    {
        Assert.False(ToolResultSuccessEvaluator.LooksSuccessful("""{"error":true}"""));
    }

    [Fact]
    public void LooksSuccessful_false_when_mcp_isError_envelope()
    {
        var json =
            """{"content":[{"type":"text","text":"An error occurred invoking 'process_event'."}],"isError":true}""";
        Assert.False(ToolResultSuccessEvaluator.LooksSuccessful(json));
    }
}
