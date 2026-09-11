using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class BackendToolResultInterpreterValidateTests
{
    [Fact]
    public void TryParse_validate_campaign_isValid_true_returns_not_failure_without_throw()
    {
        const string json = """
        {
          "isValid": true,
          "validation": { "errors": [], "warnings": [] },
          "summary": { "errorCount": 0, "warningCount": 0 }
        }
        """;

        var ok = BackendToolResultInterpreter.TryParse("validate_campaign", json, out var failure);

        Assert.False(ok);
        Assert.NotNull(failure);
        Assert.Equal(BackendToolFailureParseStatus.NotFailure, failure!.ParseStatus);
    }

    [Fact]
    public void TryParse_validate_campaign_isValid_false_extracts_validation_errors()
    {
        const string json = """
        {
          "isValid": false,
          "validation": {
            "errors": [
              { "field": "journey.shape.0", "message": "rules[] must be RuleSet wrappers." }
            ],
            "warnings": []
          },
          "summary": { "errorCount": 1, "warningCount": 0 }
        }
        """;

        var ok = BackendToolResultInterpreter.TryParse("validate_campaign", json, out var failure);

        Assert.True(ok);
        Assert.NotNull(failure);
        Assert.Equal(BackendToolFailureParseStatus.Success, failure!.ParseStatus);
        Assert.Single(failure.ValidationErrors);
        Assert.Contains("RuleSet", failure.ValidationErrors["journey.shape.0"], StringComparison.OrdinalIgnoreCase);
    }
}
