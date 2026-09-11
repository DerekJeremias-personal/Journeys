using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class CampaignValidationDigesterTests
{
    private const string CleanValidJson = """
    {
      "isValid": true,
      "validation": { "errors": [], "warnings": [] },
      "summary": { "errorCount": 0, "warningCount": 0, "ruleSetCount": 1, "hasJourney": true },
      "nextSteps": [ "Save as Draft via upsert_campaign when ready." ]
    }
    """;

    private const string WarningsValidJson = """
    {
      "isValid": true,
      "validation": {
        "errors": [],
        "warnings": [
          { "code": "WARN_JOURNEY_RULESET_NO_OUTCOMES", "message": "advisory" },
          { "code": "WARN_JOURNEY_DUPLICATE_NODE_NAME", "message": "advisory" }
        ]
      },
      "summary": { "errorCount": 0, "warningCount": 2, "ruleSetCount": 1, "hasJourney": true },
      "nextSteps": [
        "Review 2 advisory item(s), then re-validate before saving as Draft.",
        "Save as Draft via upsert_campaign when warnings are acceptable."
      ]
    }
    """;

    private const string HardErrorJson = """
    {
      "isValid": false,
      "validation": {
        "errors": [
          { "field": "journey.shape.0", "code": "JOURNEY_SHAPE", "message": "rules[] must be RuleSet wrappers." }
        ],
        "warnings": []
      },
      "summary": { "errorCount": 1, "warningCount": 0, "ruleSetCount": 0, "hasJourney": true },
      "nextSteps": [ "Fix journey.shape errors, then re-validate." ]
    }
    """;

    [Fact]
    public void Digest_clean_valid_builds_compact_ack()
    {
        var result = CampaignValidationDigester.Digest(CleanValidJson, "a1b2c3d4");

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("validateAck").GetBoolean());
        Assert.True(root.GetProperty("isValid").GetBoolean());
        Assert.Equal("a1b2c3d4", root.GetProperty("payloadFingerprint").GetString());
        Assert.Equal(0, root.GetProperty("summary").GetProperty("warningCount").GetInt32());
    }

    [Fact]
    public void Digest_pascal_case_production_shape_builds_compact_ack()
    {
        const string pascalJson = """
        {
          "IsValid": true,
          "Validation": { "Errors": [], "Warnings": [] },
          "Summary": { "ErrorCount": 0, "WarningCount": 0, "RuleSetCount": 0, "HasJourney": true },
          "NextSteps": [ "Add journey rules before upsert." ]
        }
        """;

        var result = CampaignValidationDigester.Digest(pascalJson);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < 500, $"digest was {result.DigestChars} chars");

        using var doc = JsonDocument.Parse(result.Json);
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("summary").GetProperty("RuleSetCount").GetInt32());
    }

    [Fact]
    public void Digest_warnings_valid_includes_top_warnings()
    {
        var result = CampaignValidationDigester.Digest(WarningsValidJson);

        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        var top = doc.RootElement.GetProperty("topWarnings");
        Assert.Equal(2, top.GetArrayLength());
        Assert.Equal("WARN_JOURNEY_RULESET_NO_OUTCOMES", top[0].GetString());
    }

    [Fact]
    public void Digest_hard_errors_builds_compact_error_ack()
    {
        var result = CampaignValidationDigester.Digest(HardErrorJson);

        Assert.True(result.Transformed);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("validateAck").GetBoolean());
        Assert.False(root.GetProperty("isValid").GetBoolean());
        Assert.Equal(1, root.GetProperty("topErrors").GetArrayLength());
        Assert.Equal("JOURNEY_SHAPE", root.GetProperty("topErrors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void Digest_many_hard_errors_caps_top_errors()
    {
        var errors = string.Join(",", Enumerable.Range(0, 12).Select(i =>
            $$"""{"code":"TIER_A_SIMPLE_RULE_MISSING_LEFT_PROVIDER","field":"journey.validation.{{i}}","message":"[violation=TIER_A_SIMPLE_RULE_MISSING_LEFT_PROVIDER] ruleSet=navigation.Entry nodeId=n1 rulePath=navigation.Entry kind=SimpleRule field=LeftProvider extra detail text to inflate payload size for history budget testing purposes."}"""));

        var json = $$"""
        {
          "isValid": false,
          "validation": { "errors": [{{errors}}], "warnings": [] },
          "summary": { "errorCount": 12, "warningCount": 0, "ruleSetCount": 0, "hasJourney": true },
          "nextSteps": [ "Fix navigation Entry navConstraint.", "Re-validate before upsert." ]
        }
        """;

        var result = CampaignValidationDigester.Digest(json);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars / 2);

        using var doc = JsonDocument.Parse(result.Json);
        Assert.Equal(5, doc.RootElement.GetProperty("topErrors").GetArrayLength());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"unexpected\": true }")]
    [InlineData("")]
    public void Digest_non_validation_passes_through(string input)
    {
        var result = CampaignValidationDigester.Digest(input);
        Assert.False(result.Transformed);
        Assert.Equal(input, result.Json);
    }
}
