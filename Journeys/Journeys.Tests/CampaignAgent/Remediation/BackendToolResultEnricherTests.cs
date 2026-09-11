using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class BackendToolResultEnricherTests
{
    [Fact]
    public void TryEnrich_appends_agentRemediation_without_removing_errors()
    {
        var input = """{"errors":{"attributes[0].symbol":"max 100"},"message":"Validation failed"}""";
        var failure = new BackendToolFailure
        {
            ToolName = "SaveModel",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string> { ["attributes[0].symbol"] = "max 100" },
            Message = "Validation failed",
            RawJson = input
        };
        var payload = BackendModelValidationRemediationCatalog.Build("SaveModel", failure)!;
        Assert.True(BackendToolResultEnricher.TryEnrich(input, payload, out var enriched));
        Assert.Contains("\"errors\"", enriched, StringComparison.Ordinal);
        Assert.Contains("_agentRemediation", enriched, StringComparison.Ordinal);
    }

    [Fact]
    public void TryEnrich_idempotent_when_already_present()
    {
        var input = """{"errors":{"x":"y"},"_agentRemediation":{"version":1}}""";
        var payload = new AgentRemediationPayload { Tool = "SaveModel", RetryRecommended = true };
        Assert.False(BackendToolResultEnricher.TryEnrich(input, payload, out _));
    }
}
