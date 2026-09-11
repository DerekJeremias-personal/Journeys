using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class SalientFactsPromptBuilderTests
{
    [Fact]
    public void Build_patRequired_includes_buildSubStep_and_patToolOnSurface_hint()
    {
        var state = new CampaignWorkflowState
        {
            CampaignKind = CampaignWorkflowKind.EventDriven,
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefProposed = """{"objective":"Bronze Silver Gold tiers"}""",
                CampaignDesignBriefApproved = """{"objective":"Bronze Silver Gold tiers"}""",
                ResolvedEventModelContracts =
                    """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]"""
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("buildSubStep: patrequired", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("patToolOnSurface: yes when listed in SKILL MANIFEST toolsThisTurn", block);
        Assert.Contains("ruleEpisode: tierladder", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_lastValidateFix_emitted_when_violation_code_set()
    {
        var state = new CampaignWorkflowState
        {
            CampaignKind = CampaignWorkflowKind.EventDriven,
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefApproved = """{"objective":"tier program"}""",
                ResolvedEventModelContracts =
                    """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""",
                LastValidateViolationCode = "EVENTS_ARRAY_STRING_IDS"
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("lastValidateFix: events[] string ids", block);
    }

    [Fact]
    public void Build_journeyRequired_includes_events_shape_and_scaffold_pins()
    {
        var state = new CampaignWorkflowState
        {
            CampaignKind = CampaignWorkflowKind.EventDriven,
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefProposed = """{"objective":"Bronze Silver Gold tiers"}""",
                CampaignDesignBriefApproved = """{"objective":"Bronze Silver Gold tiers"}""",
                ResolvedEventModelContracts =
                    """[{"schemaVersion":1,"eventModelId":"evt-guid-1","eventModelName":"Order","isProcessEventEligible":true}]""",
                PointAccountManifest = """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"TQP"}]}""",
                JourneyScaffold = "JOURNEY SCAFFOLD\n- pattern: tier-navigation-point-balance"
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("buildSubStep: journeyrequired", block!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("campaignDtoEventsShape: stringGuidArray", block!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eventModelIdForEventsArray: evt-guid-1", block!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("journeyScaffoldPinned: yes", block!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("depositPointsOutcomeTemplate:", block!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("processEventPreReqs:", block!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_journeyRequired_emits_authoring_template_pin_when_populated()
    {
        var state = new CampaignWorkflowState
        {
            CampaignKind = CampaignWorkflowKind.EventDriven,
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefApproved = """{"objective":"Bronze Silver Gold tiers"}""",
                ResolvedEventModelContracts =
                    """[{"schemaVersion":1,"eventModelId":"evt-guid-1","eventModelName":"Order","isProcessEventEligible":true}]""",
                PointAccountManifest = """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"TQP"}]}""",
                JourneyAuthoringTemplateJson = """{"journey":{"children":[]}}"""
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("journeyAuthoringTemplatePinned: yes", block!);
        Assert.Contains("never nodes[]", block!);
        Assert.Contains("earnAmountPath: event.ordertotal", block!);
    }

    [Fact]
    public void Build_patRequired_cross_phase_pin_when_eventModels_and_gate_open()
    {
        var state = new CampaignWorkflowState
        {
            Phase = CampaignWorkflowPhase.EventModels,
            CampaignKind = CampaignWorkflowKind.EventDriven,
            UserSkippedEventModels = true,
            ModelGatePassed = true,
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefApproved = "tier program",
                PointAccountManifest = """{"schemaVersion":1,"items":[]}"""
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("buildSubStepGovernanceCrossPhase", block!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MergeFromJourneyUpsert_stores_external_id()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CreationSnapshotArtifact.MergeFromJourneyUpsert(
            state, "entity-guid", "Draft", 3, 6, externalCampaignId: "ext-ref-1");
        var snap = CreationSnapshotArtifact.Read(state);
        Assert.Equal("entity-guid", snap!.CampaignId);
        Assert.Equal("ext-ref-1", snap.CampaignExternalId);
    }

    [Fact]
    public void Build_extracts_brief_fields()
    {
        var state = new CampaignWorkflowState
        {
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefApproved = """
                    {
                      "objective": "Drive higher order values",
                      "mechanic": "Spend milestones at $500/$1000/$2000",
                      "successCriteria": "Revenue lift vs prior year",
                      "recommendedProgramShape": { "campaignClass": "event-driven" }
                    }
                    """
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("SALIENT FACTS", block);
        Assert.Contains("Drive higher order values", block);
        Assert.Contains("Spend milestones", block);
        Assert.Contains("event-driven", block);
    }

    [Fact]
    public void Build_emits_creation_lines_when_complete()
    {
        var state = new CampaignWorkflowState
        {
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefApproved = """{"objective":"x"}""",
                CreationSnapshot = """
                    {
                      "schemaVersion": 1,
                      "creationComplete": true,
                      "campaignId": "camp-99",
                      "campaignStatus": "Draft",
                      "journeyRuleSetCount": 3,
                      "pointAccountTypes": [
                        { "id": "pat-a", "name": "Spendable" },
                        { "id": "pat-b", "name": "Tier" }
                      ]
                    }
                    """
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("creationComplete: yes", block);
        Assert.Contains("camp-99", block);
        Assert.Contains("pat-a", block);
        Assert.Contains("journeyRuleSetCount: 3", block);
    }

    [Fact]
    public void Build_omits_creation_lines_when_incomplete()
    {
        var state = new CampaignWorkflowState
        {
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CreationSnapshot = """{"schemaVersion":1,"creationComplete":false,"campaignId":"camp-1"}"""
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.Null(block);
    }

    [Fact]
    public void Build_emits_verification_lines_when_creation_complete()
    {
        var state = new CampaignWorkflowState
        {
            Artifacts = new CampaignWorkflowArtifactsDocument
            {
                CampaignDesignBriefApproved = """{"objective":"x"}""",
                CreationSnapshot = """
                    {
                      "schemaVersion": 1,
                      "creationComplete": true,
                      "campaignId": "camp-99",
                      "campaignStatus": "Draft",
                      "journeyRuleSetCount": 3
                    }
                    """,
                TenantTestAccountAllowlistJson = """["test_exp_01","test_exp_02"]""",
                VerificationTestAccountId = "test_exp_01"
            }
        };

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("draftTestAccounts:", block);
        Assert.Contains("test_exp_01", block);
        Assert.Contains("verificationTestAccountId:", block);
        Assert.Contains("verificationComplete: no", block);
    }

    [Fact]
    public void Build_falls_back_to_opening_user_when_no_brief()
    {
        var state = new CampaignWorkflowState();
        var block = SalientFactsPromptBuilder.Build(state, "Create a B2B buyer rewards program with quarterly milestones.");

        Assert.NotNull(block);
        Assert.Contains("openingUserIntent", block);
        Assert.Contains("B2B buyer rewards", block);
    }

    [Fact]
    public void Build_includes_draft_verification_when_draft_and_creation_complete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.PointAccountManifest = """{"items":[{"id":"pat-1","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        CreationSnapshotArtifact.MergePatsFromWorkflowManifest(state);
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "c1", CampaignStatusStrings.Draft, 1, 1);
        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("draftVerification:", block!);
        Assert.Contains("Live promotion NOT required", block!);
        Assert.Contains("process_event(campaignId)", block!);
    }

    [Fact]
    public void Build_emits_events_gate_lines_when_gate_closed()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier loyalty"}""";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("eventsGate: closed", block!);
        Assert.Contains("mutators filtered not missing", block!);
        Assert.Contains("resolve event model before PAT/campaign upsert", block!);
    }

    [Fact]
    public void Build_emits_expectedEventModelId_when_gate_closed()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier loyalty"}""";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
            Candidates =
            [
                new EventModelCandidate
                {
                    EventModelId = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
                    Name = "order"
                }
            ]
        });

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("expectedEventModelId: a6edbbc5-bf43-4c57-b2f1-e015b9efaf03", block!);
    }

    [Fact]
    public void Build_omits_events_gate_lines_when_gate_open()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier loyalty"}""";
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","eventModelType":"loyalty","isProcessEventEligible":true}]""";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.DoesNotContain("eventsGate:", block!);
    }

    [Fact]
    public void Build_emits_build_gate_when_manifest_empty_and_events_ready()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier"}""";
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("buildGate: patRequired", block!);
    }

    [Fact]
    public void Build_emits_pat_creation_pending_when_http_deferral_shown()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier"}""";
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        state.Artifacts.PatHttpDeferralShown = true;

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("patCreation: pending", block!);
        Assert.Contains("do not HTTP-defer", block!);
    }

    [Fact]
    public void Build_emits_pat_upsert_pending_flag()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier"}""";
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        state.Artifacts.PatUpsertPending = true;

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("patUpsertPending: yes", block!);
    }

    [Fact]
    public void Build_emits_journey_pattern_when_prep_complete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier"}""";
        state.Artifacts.JourneyPatternPrepComplete = true;
        state.Artifacts.JourneyPatternId = "tier-navigation-point-balance";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("journeyPatternId: tier-navigation-point-balance", block!);
        Assert.Contains("journeySkeletonPinned: yes", block!);
    }

    [Fact]
    public void Build_emits_journey_pattern_recommended_before_prep()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved =
            """{"objective":"bronze silver gold tiers"}""";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("journeyPatternRecommended:", block!);
    }

    [Fact]
    public void Build_emits_resolved_event_model_ids_when_creation_complete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 3,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"a6edbbc5-bf43-4c57-b2f1-e015b9efaf03","eventModelName":"Order","isProcessEventEligible":true}]""";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("resolvedEventModelIds:", block!);
        Assert.Contains("a6edbbc5-bf43-4c57-b2f1-e015b9efaf03", block!);
    }

    [Fact]
    public void Build_emits_journey_contract_pin_when_fetched_this_episode()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier"}""";
        state.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        state.Artifacts.JourneyContractSummaryPinActive = true;
        state.Artifacts.JourneyContractSummaryFetchedThisEpisode = true;
        state.Artifacts.JourneyContractSummaryMatrixVersion = "2026-06-20";
        state.Artifacts.JourneyContractCriticalRowIds = "simple_rule_three_part, outcome_affected_pat_ids";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("journeyContractPinned: yes (matrixVersion: 2026-06-20)", block!);
        Assert.Contains("contractCriticalRows: simple_rule_three_part", block!);
    }

    [Fact]
    public void Build_emits_journey_gate_when_manifest_populated_and_zero_rules()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier"}""";
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        state.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("journeyGate: rulesRequired", block!);
        Assert.Contains("RuleSetCount > 0", block!);
    }

    [Fact]
    public void Build_emits_verificationDebug_line_when_active_debug()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.Verification;
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        state.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"camp-1","journeyRuleSetCount":3}
            """;
        state.Artifacts.VerificationRecord = """{"isError":true,"errors":["rule mismatch"]}""";

        var block = SalientFactsPromptBuilder.Build(state);

        Assert.NotNull(block);
        Assert.Contains("verificationDebug:", block!);
        Assert.Contains("do not rebuild model", block!, StringComparison.OrdinalIgnoreCase);
    }
}
