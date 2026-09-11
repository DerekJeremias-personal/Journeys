using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class DeferredMutatorRetryTests
{
    [Theory]
    [InlineData("proceed to create the campaign")]
    [InlineData("proceed to create campaign")]
    [InlineData("go ahead and create the campaign")]
    [InlineData("continue to create the campaign")]
    [InlineData("create the campaign")]
    [InlineData("build the campaign")]
    [InlineData("start the campaign")]
    [InlineData("ok, proceed to create the campaign")]
    [InlineData("looks good proceed to create campaign")]
    public void MutatorRetryIntent_proceed_phrases(string message)
    {
        Assert.True(MutatorRetryIntent.LooksLikeProceedToCreate(message));
    }

    [Fact]
    public void ApplyToolResults_blocked_upsert_sets_retry_flag()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        Assert.True(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));

        CampaignWorkflowStepManager.ApplyToolResults(s,
        [
            ("upsert_campaign", """"Error: Requested function \"upsert_campaign\" not found."""")
        ]);

        Assert.True(s.Artifacts.DeferredMutatorRetry);
    }

    [Fact]
    public void ApplyToolResults_pat_not_found_with_gate_open_and_empty_manifest_sets_retry_flag()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier loyalty"}""";
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));

        CampaignWorkflowStepManager.ApplyToolResults(s,
        [
            ("upsert_point_account_type", """"Error: Requested function \"upsert_point_account_type\" not found."""")
        ]);

        Assert.True(s.Artifacts.DeferredMutatorRetry);
    }

    [Theory]
    [InlineData("please create the PATs now")]
    [InlineData("create point account types")]
    [InlineData("use upsert_point_account_type")]
    public void MutatorRetryIntent_pat_phrases(string message) =>
        Assert.True(MutatorRetryIntent.LooksLikeProceedToCreate(message));

    [Fact]
    public void TryPrepareTurnStartMutatorRetry_proceed_intent_returns_directive_and_consumes_flag()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        s.UserSkippedEventModels = true;
        s.Artifacts.DeferredMutatorRetry = true;
        s.Artifacts.LastToolRemediationSummary =
            "Error: Requested function \"upsert_campaign\" not found. Workflow hint: Complete event model resolution first.";

        var directive = CampaignWorkflowEngine.TryPrepareTurnStartMutatorRetry(
            s, "ok, proceed to create the campaign");

        Assert.NotNull(directive);
        Assert.Contains("upsert_point_account_type", directive!, StringComparison.OrdinalIgnoreCase);
        Assert.False(s.Artifacts.DeferredMutatorRetry);
        Assert.True(s.Artifacts.EventModelsGateJustCleared);
        Assert.Null(s.Artifacts.LastToolRemediationSummary);
    }

    [Fact]
    public void TryPrepareTurnStartMutatorRetry_unrelated_message_returns_null_and_keeps_flag()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        s.UserSkippedEventModels = true;
        s.Artifacts.DeferredMutatorRetry = true;

        var directive = CampaignWorkflowEngine.TryPrepareTurnStartMutatorRetry(
            s, "list live campaigns");

        Assert.Null(directive);
        Assert.True(s.Artifacts.DeferredMutatorRetry);
    }

    [Fact]
    public void GetCoachHint_gate_open_with_deferred_retry_steers_to_mutators_not_remediation()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        s.UserSkippedEventModels = true;
        s.Artifacts.DeferredMutatorRetry = true;
        s.Artifacts.LastToolRemediationSummary =
            "Error: Requested function \"upsert_campaign\" not found.";

        var hint = CampaignWorkflowStepManager.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("upsert_point_account_type", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Last remediation", hint!);
    }

    [Fact]
    public void ApplyToolResults_successful_upsert_clears_retry_flag()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.UserSkippedEventModels = true;
        s.Artifacts.DeferredMutatorRetry = true;
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";

        var ack = """{"campaignId":"c-1","name":"Test","status":"Draft"}""";
        CampaignWorkflowStepManager.ApplyToolResults(s, [("upsert_campaign", ack)]);

        Assert.False(s.Artifacts.DeferredMutatorRetry);
    }

    [Fact]
    public void ApplyToolResults_gate_open_sets_pat_upsert_pending_when_manifest_empty()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        Assert.True(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));

        var json = """
            {"id":"evt-1","name":"Order","tag":"eventable","modelType":"event","modelMetaData":{"Wrapper":"w1","NaturalKeySymbols":"[\"orderid\"]","AccountXIdSymbol":"orderid"},"attributes":[{"symbol":"orderid","type":"Primitive","dataType":"string","displayName":"Id"}]}
            """;

        CampaignWorkflowStepManager.ApplyToolResults(s, [("get_model", json)]);

        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));
        Assert.True(s.Artifacts.PatUpsertPending);
    }

    [Fact]
    public void ApplyToolResults_wrong_get_model_sets_remediation_with_expected_id()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        EventModelCandidatesArtifact.Write(s, new EventModelCandidateSet
        {
            RecommendedDefaultId = "expected-1",
            Candidates = [new EventModelCandidate { EventModelId = "expected-1", Name = "Order" }]
        });

        var wrongJson = """{"id":"wrong-1","name":"LineItem","tag":"eventable","modelType":"event"}""";
        CampaignWorkflowStepManager.ApplyToolResults(s, [("get_model", wrongJson)]);

        Assert.NotNull(s.Artifacts.LastToolRemediationSummary);
        Assert.Contains("expected-1", s.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyToolResults_successful_pat_clears_pat_upsert_pending()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.UserSkippedEventModels = true;
        s.Artifacts.PatUpsertPending = true;
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        s.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";

        CampaignWorkflowStepManager.ApplyToolResults(s,
            [("upsert_point_account_type", """{"id":"pat-1","name":"Spendable"}""")]);

        Assert.False(s.Artifacts.PatUpsertPending);
    }
}
