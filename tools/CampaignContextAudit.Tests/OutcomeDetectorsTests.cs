using CampaignContextAudit.Analysis;
using CampaignContextAudit.Transcript;
using Xunit;

namespace CampaignContextAudit.Tests;

public class OutcomeDetectorsTests
{
    private static string ConversionSamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AgentMessageSamples", "conversion.json"));

    [Fact]
    public void Conversion_golden_reflects_journey_coach_and_persisted_outcomes()
    {
        var loaded = TranscriptLoader.Load(ConversionSamplePath);
        var workflow = loaded.WorkflowRows.OrderByDescending(w => w.Sequence).First();
        var snapshot = WorkflowSnapshotParser.Parse(workflow);
        var timeline = ToolCallTimeline.Build(loaded.ChatRows);

        var findings = OutcomeDetectors.DetectAll(snapshot, loaded.ChatRows, timeline, workflow.Sequence);

        Assert.Equal("CampaignJourney", snapshot!.WorkflowPhase);
        Assert.DoesNotContain(findings, f => f.Code == "CREATION_INCOMPLETE_AT_DONE");
        Assert.Contains(findings, f => f.Code == "JOURNEY_EMPTY_AT_SUCCESS");
        Assert.Contains(findings, f => f.Code == "MANIFEST_DRIFT");
        Assert.Contains(findings, f => f.Code == "VALIDATION_UPSERT_LOOP");
    }

    [Fact]
    public void CreationIncompleteAtDone_requires_done_phase_and_incomplete_snapshot()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "Done",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);

        var findings = OutcomeDetectors.CreationIncompleteAtDone(snapshot, workflowSequence: 0);

        Assert.Single(findings);
        Assert.Equal("blocking", findings[0].Severity);
    }

    [Fact]
    public void FalseToolUnavailable_flags_claim_then_successful_invoke()
    {
        var unavailable = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"text","text":"no `upsert_point_account_type` mutating tool — doesn't appear to be enabled in this session."}]}
            """;
        var invoke = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"c1","name":"upsert_point_account_type","arguments":{}}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 1, Role = "assistant", Content = unavailable },
            new() { Sequence = 2, Role = "assistant", Content = invoke },
        };

        var findings = OutcomeDetectors.FalseToolUnavailable(rows);

        Assert.Single(findings);
        Assert.Equal("FALSE_TOOL_UNAVAILABLE", findings[0].Code);
    }

    [Fact]
    public void ValidationLoop_blockingWhenJourneyEmpty()
    {
        var validateFail = """{"errors":["JOURNEY_NAV_001"]}""";
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 10, Role = "tool", ToolName = "validate_campaign", ToolResultJson = validateFail },
            new() { Sequence = 20, Role = "tool", ToolName = "validate_campaign", ToolResultJson = validateFail },
        };
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 3,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var findings = OutcomeDetectors.DetectAll(snapshot, rows, [], 0);
        findings = OutcomeDetectors.ApplyValidationLoopSeverity(findings, snapshot);

        var loop = Assert.Single(findings, f => f.Code == "VALIDATION_UPSERT_LOOP");
        Assert.Equal("blocking", loop.Severity);
    }

    [Fact]
    public void CreationAborted_singleTurnLongWall()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 3,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 1, Role = "user" },
            new() { Sequence = 41, Role = "tool", ToolName = "validate_campaign", ToolResultJson = """{"errors":["x"]}""" },
        };
        var findings = OutcomeDetectors.DetectStallAndAbort(snapshot, rows, effectiveWallMs: 480_000, stallMs: 120_000, abortMs: 300_000);

        Assert.Contains(findings, f => f.Code == "CREATION_ABORTED" && f.Severity == "blocking");
        Assert.DoesNotContain(findings, f => f.Code == "SESSION_STALLED_NO_DELIVERY");
    }

    [Fact]
    public void StallPrecedence_emitsAbortOnly()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc> { new() { Sequence = 1, Role = "user" } };
        var findings = OutcomeDetectors.DetectStallAndAbort(snapshot, rows, 400_000, 120_000, 300_000);

        Assert.Single(findings);
        Assert.Equal("CREATION_ABORTED", findings[0].Code);
    }

    [Fact]
    public void EventModelsGateBlocksMutators_flags_validate_while_gate_closed()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 10, Role = "tool", ToolName = "validate_campaign", ToolResultJson = """{"errors":["JOURNEY_NAV_001"]}""" },
        };
        var timeline = ToolCallTimeline.Build(rows);

        var findings = OutcomeDetectors.DetectEventModelsGateBlocksMutators(snapshot, rows, timeline, modelGatePassed: false, 99);

        var finding = Assert.Single(findings);
        Assert.Equal("EVENT_MODELS_GATE_BLOCKS_MUTATORS", finding.Code);
        Assert.Equal("blocking", finding.Severity);
    }

    [Fact]
    public void EventModelsGateBlocksMutators_absent_when_gate_open()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "PointAccountTypes",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 10, Role = "tool", ToolName = "validate_campaign", ToolResultJson = """{"errors":["x"]}""" },
        };
        var timeline = ToolCallTimeline.Build(rows);

        var findings = OutcomeDetectors.DetectEventModelsGateBlocksMutators(snapshot, rows, timeline, modelGatePassed: true, 99);

        Assert.Empty(findings);
    }

    [Fact]
    public void EventModelsGateBlocksMutators_flags_http_pat_deferral()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var deferral = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"text","text":"Create PATs via POST /api/pointaccounttype/upsert and paste back the 4 GUIDs."}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 5, Role = "assistant", Content = deferral },
        };

        var findings = OutcomeDetectors.DetectEventModelsGateBlocksMutators(snapshot, rows, [], modelGatePassed: false, 99);

        Assert.Contains(findings, f => f.Code == "EVENT_MODELS_GATE_BLOCKS_MUTATORS");
    }

    [Fact]
    public void PatMutatorDeferredGateOpen_flags_http_deferral()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "CampaignSetup",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var deferral = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"text","text":"Create PATs via POST /api/pointaccounttype/upsert and paste back the 4 GUIDs."}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 24, Role = "assistant", Content = deferral },
        };

        var findings = OutcomeDetectors.DetectPatMutatorDeferredGateOpen(snapshot, rows, [], modelGatePassed: true, 99);

        Assert.Contains(findings, f => f.Code == "PAT_MUTATOR_DEFERRED_GATE_OPEN");
    }

    [Fact]
    public void PatMutatorDeferredGateOpen_absent_when_gate_closed()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var deferral = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"text","text":"Create PATs via POST /api/pointaccounttype/upsert."}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 24, Role = "assistant", Content = deferral },
        };

        var findings = OutcomeDetectors.DetectPatMutatorDeferredGateOpen(snapshot, rows, [], modelGatePassed: false, 99);

        Assert.Empty(findings);
    }

    [Fact]
    public void GateClosedPatStall_flags_stall_then_user_nudge_then_pat_upsert()
    {
        var stall = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"text","text":"I need upsert_point_account_type but I don't see that tool on this session's tool surface."}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 1, Role = "user", Content = "build tiers" },
            new() { Sequence = 17, Role = "assistant", Content = stall },
            new() { Sequence = 23, Role = "user", Content = "use the order event model. proceed to create the campaign" },
            new()
            {
                Sequence = 24,
                Role = "assistant",
                Content = """{"v":1,"meai":true,"contents":[{"kind":"functionCall","callId":"c1","name":"upsert_point_account_type","arguments":{}}]}"""
            },
            new()
            {
                Sequence = 25,
                Role = "tool",
                ToolCallId = "c1",
                ToolName = "upsert_point_account_type",
                ToolResultJson = """{"name":"Primo Points Expired","status":"Active"}"""
            },
        };
        var timeline = ToolCallTimeline.Build(rows);

        var findings = OutcomeDetectors.DetectGateClosedPatStall(rows);

        var finding = Assert.Single(findings);
        Assert.Equal("GATE_CLOSED_PAT_STALL", finding.Code);
        Assert.Equal("blocking", finding.Severity);
        Assert.Contains("17", finding.CitedSequences);
        Assert.Contains("23", finding.CitedSequences);
        Assert.Contains("25", finding.CitedSequences);
    }

    [Fact]
    public void GateClosedPatStall_absent_when_pat_follows_stall_without_user()
    {
        var stall = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"text","text":"I need upsert_point_account_type but I don't see that tool on this session's tool surface."}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 17, Role = "assistant", Content = stall },
            new()
            {
                Sequence = 18,
                Role = "assistant",
                Content = """{"v":1,"meai":true,"contents":[{"kind":"functionCall","callId":"c1","name":"upsert_point_account_type","arguments":{}}]}"""
            },
            new()
            {
                Sequence = 19,
                Role = "tool",
                ToolCallId = "c1",
                ToolName = "upsert_point_account_type",
                ToolResultJson = """{"name":"Primo Points Expired"}"""
            },
        };

        var findings = OutcomeDetectors.DetectGateClosedPatStall(rows);

        Assert.Empty(findings);
    }

    [Fact]
    public void PatSkippedInlineManifest_flags_validate_with_fake_pat_ids()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "CampaignJourney",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var validateCall = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"c1","name":"validate_campaign","arguments":{"campaignJson":"{\"journey\":{\"rules\":[{\"name\":\"Tier\"}],\"pointAccountManifest\":[{\"id\":\"pat-spendable\"}]}}"}}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 10, Role = "assistant", Content = validateCall },
        };

        var findings = OutcomeDetectors.DetectPatSkippedInlineManifest(snapshot, rows, modelGatePassed: true, 99);

        Assert.Single(findings);
        Assert.Equal("PAT_SKIPPED_INLINE_MANIFEST", findings[0].Code);
    }

    [Fact]
    public void FalseValidationPassZeroRuleSets_flags_validate_then_upsert()
    {
        var validateOkZero = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"v1","name":"validate_campaign","arguments":{"campaignJson":"{\"journey\":{\"rules\":[]}}"}}]}
            """;
        var toolValidate = """
            {"IsValid":true,"Summary":{"RuleSetCount":0,"HasJourney":true,"ErrorCount":0}}
            """;
        var upsert = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"u1","name":"upsert_campaign","arguments":{"campaignJson":"{\"journey\":{\"nodes\":[{\"id\":\"n1\"}]}}"}}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 10, Role = "assistant", Content = validateOkZero },
            new() { Sequence = 11, Role = "tool", ToolName = "validate_campaign", ToolCallId = "v1", ToolResultJson = toolValidate },
            new() { Sequence = 12, Role = "assistant", Content = upsert },
        };

        var findings = OutcomeDetectors.DetectFalseValidationPassZeroRuleSets(rows);

        Assert.Single(findings);
        Assert.Equal("FALSE_VALIDATION_PASS_ZERO_RULESETS", findings[0].Code);
    }

    [Fact]
    public void JourneyShapeNodesNotChildren_flags_nodes_in_upsert_args()
    {
        var upsert = """
            {"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"u1","name":"upsert_campaign","arguments":{"campaignJson":"{\"journey\":{\"nodes\":[{\"id\":\"n1\"}]}}"}}]}
            """;
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 12, Role = "assistant", Content = upsert },
        };

        var findings = OutcomeDetectors.DetectJourneyShapeNodesNotChildren(rows);

        Assert.Single(findings);
        Assert.Equal("JOURNEY_SHAPE_NODES_NOT_CHILDREN", findings[0].Code);
    }

    [Fact]
    public void VerificationModelRebuildDrift_flags_get_model_after_failed_process_event()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: true,
            JourneyRuleSetCount: 3,
            WorkflowPhase: "Verification",
            PatManifestCount: 2,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);

        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new()
            {
                Sequence = 10,
                Role = "assistant",
                Content = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"pe1","name":"process_event","arguments":{}}]}"""
            },
            new() { Sequence = 11, Role = "tool", ToolCallId = "pe1", ToolResultJson = """{"isError":true,"errors":["cast"]}""" },
            new()
            {
                Sequence = 12,
                Role = "assistant",
                Content = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"gm1","name":"get_model","arguments":{}}]}"""
            },
        };

        var timeline = ToolCallTimeline.Build(rows);
        var findings = OutcomeDetectors.VerificationModelRebuildDrift(snapshot, rows, timeline);

        Assert.Single(findings);
        Assert.Equal("VERIFICATION_MODEL_REBUILD_DRIFT", findings[0].Code);
        Assert.Contains("get_model", findings[0].Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationModelRebuildDrift_suppressed_when_user_requests_catalog_reload()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: true,
            JourneyRuleSetCount: 3,
            WorkflowPhase: "Verification",
            PatManifestCount: 2,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);

        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new()
            {
                Sequence = 10,
                Role = "assistant",
                Content = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"pe1","name":"process_event","arguments":{}}]}"""
            },
            new() { Sequence = 11, Role = "tool", ToolCallId = "pe1", ToolResultJson = """{"isError":true}""" },
            new() { Sequence = 12, Role = "user", Content = """{"v":1,"meai":true,"role":"user","contents":[{"kind":"text","text":"please reload the model"}]}""" },
            new()
            {
                Sequence = 13,
                Role = "assistant",
                Content = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"gm1","name":"get_model","arguments":{}}]}"""
            },
        };

        var timeline = ToolCallTimeline.Build(rows);
        var findings = OutcomeDetectors.VerificationModelRebuildDrift(snapshot, rows, timeline);

        Assert.Empty(findings);
    }

    [Fact]
    public void DetectEventPayloadSchemaLoop_flags_three_same_cast_failures()
    {
        var failJson = """{"isError":true,"errors":["InvalidCastException: discounts expected List"]}""";
        var rows = new List<CampaignContextAudit.Models.AgentMessageDoc>
        {
            new() { Sequence = 1, Role = "assistant", Content = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"c1","name":"process_event","arguments":{}}]}""" },
            new() { Sequence = 2, Role = "tool", ToolCallId = "c1", ToolResultJson = failJson },
            new() { Sequence = 3, Role = "assistant", Content = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"c2","name":"process_event","arguments":{}}]}""" },
            new() { Sequence = 4, Role = "tool", ToolCallId = "c2", ToolResultJson = failJson },
            new() { Sequence = 5, Role = "assistant", Content = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"c3","name":"process_event","arguments":{}}]}""" },
            new() { Sequence = 6, Role = "tool", ToolCallId = "c3", ToolResultJson = failJson },
        };
        var snapshot = new WorkflowSnapshot(
            CreationComplete: true,
            JourneyRuleSetCount: 3,
            WorkflowPhase: "Verification",
            PatManifestCount: 2,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var timeline = ToolCallTimeline.Build(rows);
        var findings = OutcomeDetectors.DetectEventPayloadSchemaLoop(snapshot, rows, timeline);

        Assert.Single(findings);
        Assert.Equal("EVENT_PAYLOAD_SCHEMA_LOOP", findings[0].Code);
    }
}
