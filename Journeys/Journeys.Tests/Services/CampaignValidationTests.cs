using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;

namespace Journeys.Tests.Services;

public class CampaignValidationErrorMapperTests
{
    [Fact]
    public void MapEntry_extracts_violation_code_and_path()
    {
        const string message =
            "[violation=JOURNEY_NAV_ROOT_ENTRY_REQUIRED] path=journey — multi-tier journeys require root Entry.";

        var finding = CampaignValidationErrorMapper.MapEntry("journey.navigation.0", message);

        Assert.Equal("JOURNEY_NAV_ROOT_ENTRY_REQUIRED", finding.Code);
        Assert.Equal("journey", finding.Path);
        Assert.Equal("error", finding.Severity);
    }

    [Fact]
    public void AddFromException_maps_all_dict_entries()
    {
        var ex = new APIErrorsException(new Dictionary<string, string>
        {
            { "status", "Status is required." }
        });

        var findings = new List<CampaignValidationFindingDto>();
        CampaignValidationErrorMapper.AddFromException(ex, findings);

        Assert.Single(findings);
        Assert.Equal("status", findings[0].Field);
    }
}

public class CampaignJourneyAdvisoryValidatorTests
{
    [Fact]
    public void Validate_emits_duplicate_name_warning()
    {
        var child = new JourneyNode("tier-b", id: "child-1");
        var root = new JourneyNode("tier-b", children: new List<JourneyNode> { child }, id: "root-1");

        var warnings = CampaignJourneyAdvisoryValidator.Validate(
            new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, root, "t", "c"),
            materialized: false);

        Assert.Contains(warnings, w => w.Code == "WARN_JOURNEY_DUPLICATE_NODE_NAME");
    }

    [Fact]
    public void Validate_emits_no_outcomes_warning_when_materialized()
    {
        const string ruleJson = """
            {
              "Kind": "SimpleRule",
              "LeftProvider": { "$type": "ConstantValueProvider", "Value": true },
              "RightProvider": { "$type": "ConstantValueProvider", "Value": true },
              "Evaluator": { "$type": "BoolEvaluation", "Comparison": "Equal" }
            }
            """;

        using var ruleDoc = System.Text.Json.JsonDocument.Parse(ruleJson);
        var ruleSet = new RuleSet(
            "rs",
            ruleDoc.RootElement.Clone(),
            "SimpleRule",
            System.Text.Json.JsonDocument.Parse("[]").RootElement.Clone(),
            null);
        var journey = new JourneyNode("root", new List<RuleSet> { ruleSet }, "j1", "j1", null, null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, journey, "t", "c");

        CampaignJourneyMaterializer.Materialize(campaign);

        var warnings = CampaignJourneyAdvisoryValidator.Validate(campaign, materialized: true);

        Assert.Contains(warnings, w => w.Code == "WARN_JOURNEY_RULESET_NO_OUTCOMES");
    }

    [Fact]
    public void Validate_emits_duplicate_signature_warning_for_identical_rule_nodes()
    {
        var nodeA = CreateMaterializedEarnNode("node-a", "Tier A");
        var nodeB = CreateMaterializedEarnNode("node-b", "Tier B");
        var root = new JourneyNode("root", children: new List<JourneyNode> { nodeA, nodeB }, id: "root-1");

        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            root,
            "t",
            "c");

        var warnings = CampaignJourneyAdvisoryValidator.Validate(campaign, materialized: true);

        Assert.Contains(warnings, w => w.Code == "WARN_JOURNEY_DUPLICATE_NODE_SIGNATURE");
    }

    private static JourneyNode CreateMaterializedEarnNode(string id, string name)
    {
        const string ruleJson = """
            {
              "Kind": "SimpleRule",
              "LeftProvider": { "$type": "ConstantValueProvider", "Value": true },
              "RightProvider": { "$type": "ConstantValueProvider", "Value": true },
              "Evaluator": { "$type": "BoolEvaluation", "Comparison": "Equal" }
            }
            """;

        const string outcomesJson = """
            [
              {
                "Kind": "TagOutcome",
                "Type": "account",
                "EntityId": "e",
                "Name": "engagement_reward",
                "Value": "active_member"
              }
            ]
            """;

        using var ruleDoc = System.Text.Json.JsonDocument.Parse(ruleJson);
        using var outcomesDoc = System.Text.Json.JsonDocument.Parse(outcomesJson);
        var ruleSet = new RuleSet(
            "Earn",
            ruleDoc.RootElement.Clone(),
            "SimpleRule",
            outcomesDoc.RootElement.Clone(),
            null);

        var node = new JourneyNode(name, new List<RuleSet> { ruleSet }, id, id, null, null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, node, "t", "c");
        CampaignJourneyMaterializer.Materialize(campaign);
        return node;
    }
}

public class JourneyNodeSignatureFingerprinterTests
{
    [Fact]
    public void ComputeSignature_same_rules_different_names_produce_same_signature()
    {
        var nodeA = CreateMaterializedEarnNode("node-a", "Earn A");
        var nodeB = CreateMaterializedEarnNode("node-b", "Earn B");

        var sigA = JourneyNodeSignatureFingerprinter.ComputeSignature(nodeA);
        var sigB = JourneyNodeSignatureFingerprinter.ComputeSignature(nodeB);

        Assert.False(string.IsNullOrEmpty(sigA));
        Assert.Equal(sigA, sigB);
    }

    [Fact]
    public void ComputeSignature_different_outcome_kinds_produce_different_signatures()
    {
        var tagNode = CreateMaterializedEarnNode("node-1", "Tag tier");
        var emptyOutcomeNode = CreateNodeWithEmptyOutcomes("node-2", "Empty outcomes");

        var sigTag = JourneyNodeSignatureFingerprinter.ComputeSignature(tagNode);
        var sigEmpty = JourneyNodeSignatureFingerprinter.ComputeSignature(emptyOutcomeNode);

        Assert.NotEqual(sigTag, sigEmpty);
    }

    private static JourneyNode CreateMaterializedEarnNode(string id, string name)
    {
        const string ruleJson = """
            {
              "Kind": "SimpleRule",
              "LeftProvider": { "$type": "ConstantValueProvider", "Value": true },
              "RightProvider": { "$type": "ConstantValueProvider", "Value": true },
              "Evaluator": { "$type": "BoolEvaluation", "Comparison": "Equal" }
            }
            """;

        const string outcomesJson = """
            [
              {
                "Kind": "TagOutcome",
                "Type": "account",
                "EntityId": "e",
                "Name": "engagement_reward",
                "Value": "active_member"
              }
            ]
            """;

        using var ruleDoc = System.Text.Json.JsonDocument.Parse(ruleJson);
        using var outcomesDoc = System.Text.Json.JsonDocument.Parse(outcomesJson);
        var ruleSet = new RuleSet(
            "Earn",
            ruleDoc.RootElement.Clone(),
            "SimpleRule",
            outcomesDoc.RootElement.Clone(),
            null);

        var node = new JourneyNode(name, new List<RuleSet> { ruleSet }, id, id, null, null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, node, "t", "c");
        CampaignJourneyMaterializer.Materialize(campaign);
        return node;
    }

    private static JourneyNode CreateNodeWithEmptyOutcomes(string id, string name)
    {
        const string ruleJson = """
            {
              "Kind": "SimpleRule",
              "LeftProvider": { "$type": "ConstantValueProvider", "Value": true },
              "RightProvider": { "$type": "ConstantValueProvider", "Value": true },
              "Evaluator": { "$type": "BoolEvaluation", "Comparison": "Equal" }
            }
            """;

        using var ruleDoc = System.Text.Json.JsonDocument.Parse(ruleJson);
        var ruleSet = new RuleSet(
            "Earn",
            ruleDoc.RootElement.Clone(),
            "SimpleRule",
            System.Text.Json.JsonDocument.Parse("[]").RootElement.Clone(),
            null);

        var node = new JourneyNode(name, new List<RuleSet> { ruleSet }, id, id, null, null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, node, "t", "c");
        CampaignJourneyMaterializer.Materialize(campaign);
        return node;
    }
}

public class CampaignValidationOrchestratorTests
{
    private static readonly CampaignValidationOrchestrator Sut =
        CampaignTestServices.CreateValidationOrchestrator();

    [Fact]
    public async Task ValidateAsync_returns_shell_errors_without_throwing()
    {
        var dto = new CampaignDto
        {
            Status = CampaignStatusStrings.Draft,
            Name = "",
            ExtCampaignId = "",
            StartDate = DateTimeOffset.MinValue
        };

        var result = await Sut.ValidateAsync("t1", dto);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Validation.Errors);
        Assert.NotEmpty(result.NextSteps);
        Assert.True(result.Summary.ErrorCount > 0);
    }

    [Fact]
    public async Task ValidateAsync_affirms_clean_minimal_campaign()
    {
        var dto = new CampaignDto
        {
            Status = CampaignStatusStrings.Draft,
            Name = "test-campaign",
            ExtCampaignId = "test-campaign",
            StartDate = DateTimeOffset.UtcNow
        };

        var result = await Sut.ValidateAsync("t1", dto);

        Assert.True(result.IsValid);
        Assert.Empty(result.Validation.Errors);
        Assert.NotEmpty(result.NextSteps);
        Assert.Contains(result.NextSteps, s => s.Contains("validated successfully", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_array_ruleJsonElement_returns_shape_violation_not_materialize()
    {
        const string json = """
        {
          "status": "draft",
          "name": "test-campaign",
          "extCampaignId": "test-campaign",
          "startDate": "2026-01-01T00:00:00Z",
          "journey": {
            "id": "j1",
            "rules": [{
              "name": "Earn",
              "ruleJsonElement": [ { "Kind": "NumericPropertyRule" } ],
              "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
            }]
          }
        }
        """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<CampaignDto>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var result = await Sut.ValidateAsync("t1", dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Validation.Errors, e =>
            e.Code == "JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT"
            || (e.Field?.StartsWith("journey.shape.", StringComparison.Ordinal) ?? false));
        Assert.DoesNotContain(result.Validation.Errors, e =>
            e.Field == "journey.materialize");
    }

    [Fact]
    public async Task ValidateAsync_rejects_deposit_outcome_without_pat_ids()
    {
        const string json = """
        {
          "status": "draft",
          "name": "test-campaign",
          "extCampaignId": "test-campaign",
          "startDate": "2026-01-01T00:00:00Z",
          "journey": {
            "id": "j1",
            "rules": [{
              "name": "Earn",
              "ruleJsonElement": { "Kind": "SimpleRule" },
              "outcomesJsonElement": [{
                "Kind": "DepositPointsOutcome",
                "dollarAmountProvider": { "$type": "ConstantValueProvider", "Value": 10 },
                "pointsPerDollar": 1
              }]
            }]
          }
        }
        """;

        var dto = System.Text.Json.JsonSerializer.Deserialize<CampaignDto>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        var result = await Sut.ValidateAsync("t1", dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Validation.Errors, e =>
            (e.Code?.Contains("MISSING_AFFECTED_PAT", StringComparison.Ordinal) ?? false)
            || e.Message.Contains("MISSING_AFFECTED_PAT", StringComparison.Ordinal));
    }
}
