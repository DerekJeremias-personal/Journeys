using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowStepManagerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void get_model_does_not_advance_phase()
    {
        var s = InEventModels();
        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("get_model", ModelJson("d1", "LoyaltyAccountDetails"))
        });

        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
        Assert.False(s.ModelGatePassed);
        Assert.Single(EventModelContractsAccumulator.ReadDiscovered(s));
    }

    [Fact]
    public void save_model_matching_pending_marks_events_checklist_complete()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("save_model", ModelJson("r1", "Review", tag: "eventable"))
        });
        s.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","wrapperModelName":"ReviewAndRuleState","errors":[],"warnings":[]}]""";
        CampaignWorkflowStepManager.TryUpdateChecklist(s);
        s.Phase = CampaignWorkflowFocusResolver.CoachDefaultFocus(s);

        Assert.True(s.ModelGatePassed);
        Assert.True(CampaignWorkflowChecklist.IsItemComplete(s, CampaignWorkflowPhase.EventModels));
        Assert.Equal(CampaignWorkflowFocusResolver.CoachDefaultFocus(s), s.Phase);
        Assert.Equal(0, s.Artifacts.EventModelSaveFailureCount);
        Assert.Single(EventModelContractsAccumulator.ReadResolved(s));
    }

    [Fact]
    public void ApplyToolResults_opening_event_models_gate_sets_continuation_flag()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","wrapperModelName":"ReviewAndRuleState","errors":[],"warnings":[]}]""";
        Assert.True(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("save_model", ModelJson("r1", "Review", tag: "eventable"))
        });

        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));
        Assert.True(s.Artifacts.EventModelsGateJustCleared);
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void get_model_matching_pending_eventable_review_after_failed_save_does_not_complete_events()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.UserRequestedNewEventModel = true;

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("save_model", """{"error":true,"message":"Value cannot be null. (Parameter 'key')"}"""),
            ("save_model", """{"error":true,"message":"Value cannot be null. (Parameter 'key')"}""")
        });
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
        Assert.Equal(2, s.Artifacts.EventModelSaveFailureCount);

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("get_model", ModelJson("fbfe5201-d2e2-4ea4-9fc9-e79343adcfc1", "Review", tag: "eventable"))
        });

        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
        Assert.False(s.ModelGatePassed);
        Assert.False(CampaignWorkflowChecklist.IsItemComplete(s, CampaignWorkflowPhase.EventModels));
        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));
        Assert.Single(EventModelContractsAccumulator.ReadDiscovered(s));
        Assert.Equal("Review", EventModelContractsAccumulator.ReadDiscovered(s)[0].EventModelName);
    }

    [Fact]
    public void ApplyUserMessage_test_intent_captures_test_account_id()
    {
        var s = InVerificationReady();
        CampaignWorkflowStepManager.ApplyUserMessage(s, "Please test with test_exp_01");
        Assert.Equal("test_exp_01", s.Artifacts.VerificationTestAccountId);
        Assert.Equal(CampaignWorkflowPhase.Verification, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_get_account_confirms_test_account()
    {
        var s = InVerificationReady();
        s.Artifacts.VerificationTestAccountId = "test_exp_01";

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("get_account", """{"id":"a1","extAccountId":"test_exp_01"}""")
        });

        Assert.True(s.Artifacts.VerificationAccountConfirmed);
    }

    [Fact]
    public void ApplyToolResults_failed_process_event_does_not_set_verification_record()
    {
        var s = InVerificationReady();
        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("process_event", """{"errors":{"processEvent":"EngineException: fail"}}""")
        });

        Assert.Null(s.Artifacts.VerificationRecord);
        Assert.NotNull(s.Artifacts.LastToolRemediationSummary);
    }

    [Fact]
    public void ApplyToolResults_successful_process_event_sets_verification_record()
    {
        var s = InVerificationReady();
        var success = """{"evaluatedCampaigns":["c1"]}""";
        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("process_event", success)
        });

        Assert.Equal(success, s.Artifacts.VerificationRecord);
    }

    [Fact]
    public void GetCoachHint_surfaces_wrapper_contract_errors()
    {
        var s = InEventModels();
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Review","eventModelType":"loyalty","wrapperModelId":"w1","isProcessEventEligible":true}]""";
        s.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","wrapperModelName":"ReviewAndRuleState","errors":["Missing required wrapper attribute 'accountid'.","Attribute 'outcomestates' must be type ModelList, found ModelObject."],"warnings":[]}]""";

        var hint = CampaignWorkflowStepManager.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("wrapper invalid", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accountid", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_verification_phase_prompts_get_account()
    {
        var s = InVerificationReady();
        s.Artifacts.VerificationTestAccountId = "test_exp_01";
        var hint = CampaignWorkflowStepManager.GetCoachHint(s);
        Assert.NotNull(hint);
        Assert.Contains("get_account", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void save_model_pat_bypass_sets_remediation_summary()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("save_model", PatShapedModelJson())
        });

        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));
        Assert.NotNull(s.Artifacts.LastToolRemediationSummary);
        Assert.Contains("upsert_point_account_type", s.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyUserMessage_reuse_confirm_promotes_discovered_review()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.UserRequestedNewEventModel = true;
        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("get_model", ModelJson("r1", "Review", tag: "eventable"))
        });

        CampaignWorkflowStepManager.ApplyUserMessage(s, "use existing model");

        Assert.Single(EventModelContractsAccumulator.ReadResolved(s));
        Assert.False(s.Artifacts.UserRequestedNewEventModel);
    }

    [Fact]
    public void ApplyToolResults_journey_boundary_pins_phase_to_CampaignJourney()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Phase = CampaignWorkflowPhase.EventModels;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier program"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        s.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Points","isSpendable":true}]}""";
        s.UserSkippedEventModels = true;

        CampaignWorkflowStepManager.ApplyToolResults(s, Array.Empty<(string, string)>());

        Assert.Equal(CampaignWorkflowPhase.CampaignJourney, s.Phase);
    }

    [Fact]
    public void ApplyUserMessage_sets_focus_to_journey_when_user_asks()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";

        CampaignWorkflowStepManager.ApplyUserMessage(s, "let's author the journey rules");

        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, s.Phase);
    }

    private static CampaignWorkflowState InVerificationReady()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.Phase = CampaignWorkflowPhase.Verification;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        s.Artifacts.JourneyDigestApproved = """{"version":1}""";
        s.ModelGatePassed = true;
        return s;
    }

    private static CampaignWorkflowState InEventModels()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        return s;
    }

    private static string ModelJson(string id, string name, string? tag = null) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            tag,
            modelType = "loyalty",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"reviewid\"]",
                ["AccountXIdSymbol"] = "customerid",
                ["TimeOfOccurrence"] = "reviewdate"
            },
            attributes = new[]
            {
                new { symbol = "customerid", type = "Primitive", dataType = "string", displayName = "Customer" },
                new { symbol = "reviewid", type = "Primitive", dataType = "string", displayName = "Review" }
            }
        }, JsonOpts);

    [Fact]
    public void ApplyToolResults_upsert_mutation_ack_updates_journey_digest_and_snapshot()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        s.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Spendable","role":"spendable"}]}""";

        const string full = """
            {
              "Id": "camp-ack",
              "Status": "draft",
              "Name": "tiered",
              "Events": ["evt-1"],
              "Journey": {
                "Name": "Main",
                "Children": [{
                  "Name": "Node",
                  "Children": [],
                  "Rules": [{
                    "Name": "R1",
                    "RuleJsonElement": { "Kind": "NumericPropertyRule" },
                    "OutcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
                  }]
                }]
              }
            }
            """;
        var ack = CampaignMutationDigester.Digest(full);

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("upsert_campaign", ack.Json)
        });

        Assert.False(string.IsNullOrWhiteSpace(s.Artifacts.JourneyDigestProposed));
        Assert.True(CreationSnapshotArtifact.IsCreationComplete(s));
        var snap = CreationSnapshotArtifact.Read(s);
        Assert.Equal("camp-ack", snap!.CampaignId);
    }

    [Fact]
    public void ApplyToolResults_validate_campaign_updates_artifact()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignSetup;
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        s.ModelGatePassed = true;

        const string ack = """
        {
          "validateAck": true,
          "isValid": true,
          "payloadFingerprint": "deadbeef",
          "summary": { "errorCount": 0, "warningCount": 0, "ruleSetCount": 0, "hasJourney": false }
        }
        """;

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("validate_campaign", ack)
        });

        var artifact = CampaignValidationCoach.Read(s);
        Assert.NotNull(artifact);
        Assert.True(artifact!.IsValid);
        Assert.Equal("deadbeef", artifact.PayloadFingerprint);
    }

    [Fact]
    public void ApplyToolResults_failed_upsert_invalidates_validation_artifact()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        s.ModelGatePassed = true;

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("validate_campaign", """{"validateAck":true,"isValid":true,"summary":{"errorCount":0,"warningCount":0}}"""),
            ("upsert_campaign", """{"errors":{"journey.shape.0":"bad shape"}}""")
        });

        var artifact = CampaignValidationCoach.Read(s);
        Assert.NotNull(artifact);
        Assert.False(artifact!.IsValid);
        Assert.True(artifact.UpsertFailedSinceValidate);
        Assert.Contains("validate_campaign", s.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyToolResults_mcp_invocation_error_on_upsert_sets_journey_coach_remediation()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        s.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";

        const string mcpError = """
        {
          "content": [{ "type": "text", "text": "An error occurred invoking 'upsert_campaign'." }],
          "isError": true
        }
        """;

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("upsert_campaign", mcpError)
        });

        Assert.Contains("MCP invocation error", s.Artifacts.LastToolRemediationSummary!, StringComparison.Ordinal);
        Assert.True(CampaignValidationCoach.Read(s)!.UpsertFailedSinceValidate);
    }

    [Fact]
    public void ApplyToolResults_assistant_context_updates_creation_snapshot()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";

        const string ctx = """
        {
          "campaignId": "camp-1",
          "status": "draft",
          "journey": { "schemaVersion": 1, "campaignId": "camp-1", "ruleSetCount": 1, "journeyNodeCount": 2 }
        }
        """;

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("get_campaign_assistant_context", ctx)
        });

        var snap = CreationSnapshotArtifact.Read(s);
        Assert.NotNull(snap);
        Assert.Equal(1, snap!.JourneyRuleSetCount);
    }

    [Fact]
    public void ApplyToolResults_deferred_mutator_not_found_sets_retry_flag()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        Assert.True(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("upsert_campaign", """{"errors":{"campaign":"Campaign not found"}}""")
        });

        Assert.True(s.Artifacts.DeferredMutatorRetry);
    }

    [Fact]
    public void ApplyUserMessage_clears_journey_gate_and_validation_stall()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
        s.Artifacts.ValidationStalled = true;
        s.Artifacts.ValidationStallCycleCount = 2;
        CampaignWorkflowStepManager.ApplyUserMessage(s, "continue with Bronze tier at 10k points");
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.False(s.Artifacts.ValidationStalled);
        Assert.Equal(0, s.Artifacts.ValidationStallCycleCount);
    }

    [Fact]
    public void save_model_pat_fabrication_sets_remediation_after_events_gate_open()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.Phase = CampaignWorkflowPhase.PointAccountTypes;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier loyalty"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        s.ModelGatePassed = true;
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","eventModelType":"loyalty","isProcessEventEligible":true}]""";

        CampaignWorkflowStepManager.ApplyToolResults(s, new List<(string, string)>
        {
            ("save_model", TierQualPointsFabricationJson())
        });

        Assert.NotNull(s.Artifacts.LastToolRemediationSummary);
        Assert.Contains("upsert_point_account_type", s.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
    }

    private static string TierQualPointsFabricationJson() =>
        """
        {
          "id": "e68d2c52-ee33-450e-8921-96e63c144b1e",
          "name": "TierQualPoints",
          "modelType": "loyalty",
          "isContainer": false,
          "modelMetaData": {
            "ledgerType": "NonSpendable",
            "isSpendable": "false",
            "pointAccountTypeName": "TierQualPoints"
          },
          "attributes": [
            { "symbol": "balance", "type": "Primitive", "dataType": "Number" }
          ]
        }
        """;

    private static string PatShapedModelJson() =>
        """
            {
              "id": "6983defa-b920-4a4f-a3fa-d4508083dddf",
              "name": "ReviewRewards",
              "modelType": "loyalty",
              "isContainer": true,
              "attributes": [
                { "symbol": "extaccountid", "type": "Primitive", "dataType": "String" },
                { "symbol": "pointsourceid", "type": "Primitive", "dataType": "String" },
                { "symbol": "ledgertype", "type": "Primitive", "dataType": "String" }
              ]
            }
            """;
}
