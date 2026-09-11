using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;

namespace Journeys.Tests.CampaignAgent;

public class CampaignWorkflowEngineTests
{
    [Fact]
    public void ApplyUserMessage_TagFirstPhrase_MovesToCampaignBuild()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyUserMessage(s, "Please use a tag-first campaign, no event model");
        Assert.Equal(CampaignWorkflowKind.TagFirst, s.CampaignKind);
        Assert.True(s.UserSkippedEventModels);
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_WarehouseTool_CapturesBriefWithoutApprovalPause()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("GetProgramPerformanceSummary", """{"activeMembers":100}""")
        });
        Assert.True(s.Artifacts.DataAnalysisWarehouseSucceeded);
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.False(string.IsNullOrEmpty(s.Artifacts.CampaignDesignBriefProposed));
        Assert.Equal(s.Artifacts.CampaignDesignBriefProposed, s.Artifacts.CampaignDesignBriefApproved);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_ProposeBrief_WhenWarehouseDisabled_CapturesBriefWithoutApprovalPause()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyToolResults(
            s,
            new List<(string, string)>
            {
                ("ProposeCampaignDesignBrief", """{"captured":true,"objective":"Grow adoption","campaignClass":"event-driven"}""")
            },
            dataWarehouseEnabled: false);

        Assert.True(s.Artifacts.DataAnalysisObjectiveCaptured);
        Assert.False(s.Artifacts.DataAnalysisWarehouseSucceeded);
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.False(string.IsNullOrEmpty(s.Artifacts.CampaignDesignBriefProposed));
        Assert.Equal(s.Artifacts.CampaignDesignBriefProposed, s.Artifacts.CampaignDesignBriefApproved);
        Assert.Equal(CampaignWorkflowKind.EventDriven, s.CampaignKind);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_ProposeBrief_TagFirst_SetsTagFirstKind()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyToolResults(
            s,
            new List<(string, string)>
            {
                ("ProposeCampaignDesignBrief", """{"objective":"Reward strategic accounts","campaignClass":"tag-first"}""")
            },
            dataWarehouseEnabled: false);

        Assert.True(s.Artifacts.DataAnalysisObjectiveCaptured);
        Assert.Equal(CampaignWorkflowKind.TagFirst, s.CampaignKind);
        Assert.True(s.UserSkippedEventModels);
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_ProposeBrief_Approve_AdvancesToEventModels()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyToolResults(
            s,
            new List<(string, string)> { ("ProposeCampaignDesignBrief", """{"objective":"X"}""") },
            dataWarehouseEnabled: false);
        CampaignWorkflowEngine.ApplyUserMessage(s, "approve", dataWarehouseEnabled: false);

        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
    }

    [Fact]
    public void ApplyUserMessage_Proceed_WhenWarehouseDisabledNoBrief_BuildsBriefAndAdvances()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyUserMessage(s, "ok to continue", dataWarehouseEnabled: false);

        Assert.True(s.Artifacts.DataAnalysisObjectiveCaptured);
        Assert.False(string.IsNullOrEmpty(s.Artifacts.CampaignDesignBriefProposed));
        Assert.Equal(s.Artifacts.CampaignDesignBriefProposed, s.Artifacts.CampaignDesignBriefApproved);
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
    }

    [Fact]
    public void ApplyUserMessage_Proceed_WhenWarehouseEnabled_DoesNotAutoAdvanceDataAnalysis()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyUserMessage(s, "continue", dataWarehouseEnabled: true);

        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
        Assert.False(s.Artifacts.DataAnalysisObjectiveCaptured);
    }

    [Fact]
    public void ApplyToolResults_GetModelSuccess_OnEventPath_DoesNotAdvanceToCampaignSetup()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("GetModel", """{"id":"x","modelType":"event"}""")
        });
        Assert.False(s.ModelGatePassed);
        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_GetModelSuccess_StoresEventProcessingContractDigest()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        var modelJson = """
            {
              "id": "m1",
              "name": "Order",
              "modelType": "event",
              "modelMetaData": {
                "Wrapper": "w1",
                "NaturalKeySymbols": "[\"orderid\"]",
                "AccountXIdSymbol": "orderid"
              },
              "attributes": [{ "symbol": "orderid", "type": "Primitive", "dataType": "string", "displayName": "Id" }]
            }
            """;
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)> { ("GetModel", modelJson) });
        Assert.False(s.ModelGatePassed);
        var discovered = EventModelContractsAccumulator.ReadDiscovered(s);
        Assert.Single(discovered);
        Assert.Equal("m1", discovered[0].EventModelId);
        Assert.Equal(1, discovered[0].SchemaVersion);
        Assert.Equal("orderid", discovered[0].AccountLink.SymbolPath);
    }

    [Fact]
    public void ApplyToolResults_UpsertCampaign_StoresJourneyDigest()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignSetup;
        const string json = """
            {
              "id": "camp-1",
              "name": "Summer Promo",
              "status": "Draft",
              "startDate": "2025-06-01T00:00:00Z",
              "events": ["evt-a", "evt-b"]
            }
            """;
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)> { ("UpsertCampaign", json) });
        Assert.False(string.IsNullOrWhiteSpace(s.Artifacts.JourneyDigestProposed));
        Assert.Contains("camp-1", s.Artifacts.JourneyDigestProposed!, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyToolResults_TwoModelMergesWithPlannedIds_AccumulatesBeforeAdvance()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        s.Artifacts.CampaignDesignBriefApproved = BriefWithPlannedIds(["idA", "idB"]);
        s.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","wrapperModelName":"ReviewAndRuleState","errors":[],"warnings":[]}]""";

        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("SaveModel", EventModelToolJson("idA", "OrderA"))
        });
        Assert.False(s.ModelGatePassed);
        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
        Assert.Single(EventModelContractsAccumulator.ReadResolved(s));

        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("SaveModel", EventModelToolJson("idB", "OrderB"))
        });
        Assert.True(s.ModelGatePassed);
        Assert.True(CampaignWorkflowChecklist.IsItemComplete(s, CampaignWorkflowPhase.EventModels));
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, s.Phase);

        Assert.Equal(2, EventModelContractsAccumulator.ReadResolved(s).Count);
    }

    [Fact]
    public void ApplyToolResults_GetModel_DoubleEncodedStringResult_ParsesAndPromotesCompleteContract()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        // Simulate an MCP transport that returns the model JSON wrapped as a JSON string.
        var doubleEncoded = JsonSerializer.Serialize(EventModelToolJson("idA", "OrderA"));

        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("GetModel", doubleEncoded)
        });

        Assert.True(s.ModelGatePassed);
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, s.Phase);
        Assert.Single(EventModelContractsAccumulator.ReadResolved(s));
    }

    [Fact]
    public void ApplyToolResults_UpsertPat_DoubleEncodedStringResult_ParsesManifestButDoesNotForceLinearAdvance()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.PointAccountTypes;
        var patJson = """{"id":"pat1","name":"Spendable","status":"Active","ledgerType":"Spendable","isSpendable":true}""";
        var doubleEncoded = JsonSerializer.Serialize(patJson);

        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("UpsertPointAccountType", doubleEncoded)
        });

        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
        var manifest = PointAccountManifestBuilder.Parse(s.Artifacts.PointAccountManifest);
        Assert.Single(manifest.Items);
    }

    [Fact]
    public void ApplyToolResults_GetModel_WithErrors_DoesNotAdvance()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("GetModel", """{"errors":{"x":"y"}}""")
        });
        Assert.False(s.ModelGatePassed);
        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_UpsertCampaign_DoesNotForceLinearAdvance()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignSetup;
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("UpsertCampaign", """{"status":"draft","id":"c1"}""")
        });
        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
        Assert.False(string.IsNullOrWhiteSpace(s.Artifacts.JourneyDigestProposed));
    }

    [Fact]
    public void ApplyToolResults_UpsertPat_DoesNotForceLinearAdvance()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.PointAccountTypes;
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("UpsertPointAccountType", """{"id":"pat1","name":"Spendable"}""")
        });
        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
    }

    [Fact]
    public void ApplyToolResults_GetPointAccountType_ExistingPats_BuildsManifestWithoutLinearAdvance()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.PointAccountTypes;
        s.Artifacts.CampaignDesignBriefApproved =
            """{"mechanic":"Spend threshold milestones - members earn rewards at cumulative spend levels"}""";

        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("get_point_account_type",
                """{"id":"829eff38-6591-4c39-bfbd-db52c3256b53","name":"user_spendable","ledgerType":"Spendable","isSpendable":true,"status":"Active"}"""),
            ("get_point_account_type",
                """{"id":"f0ec0c6d-1282-497b-8bc5-e6be5fbe83ec","name":"user_tier_qualification","ledgerType":"NonSpendable","isSpendable":false,"status":"Active"}""")
        });

        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
        var manifest = PointAccountManifestBuilder.Parse(s.Artifacts.PointAccountManifest);
        Assert.Equal(2, manifest.Items.Count);
    }

    [Fact]
    public void ApplyToolResults_MilestoneBrief_SinglePat_DoesNotAdvance()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.PointAccountTypes;
        s.Artifacts.CampaignDesignBriefApproved =
            """{"mechanic":"Spend threshold milestones"}""";

        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("get_point_account_type",
                """{"id":"829eff38-6591-4c39-bfbd-db52c3256b53","name":"user_spendable","ledgerType":"Spendable","isSpendable":true}""")
        });

        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
        Assert.False(CampaignWorkflowChecklist.IsItemComplete(s, CampaignWorkflowPhase.PointAccountTypes));
    }

    [Fact]
    public void ApplyToolResults_UpsertCampaignJourney_AutoApprovesDigest()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        CampaignWorkflowEngine.ApplyToolResults(s, new List<(string, string)>
        {
            ("UpsertCampaign", """{"status":"draft","id":"c1","journey":{}}""")
        });
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.False(string.IsNullOrWhiteSpace(s.Artifacts.JourneyDigestProposed));
        Assert.Equal(s.Artifacts.JourneyDigestProposed, s.Artifacts.JourneyDigestApproved);
    }

    [Fact]
    public void ApplyUserMessage_Approve_AfterDesignBrief_DoesNotClearExplicitApprovalFlag()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.DesignBrief;
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        CampaignWorkflowEngine.ApplyUserMessage(s, "looks good, continue");
        Assert.Equal(CampaignWorkflowApprovalKind.DesignBrief, s.Artifacts.AwaitingApproval);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
    }

    [Fact]
    public void ApplyUserMessage_Approve_AfterJourneyEntryCheckpoint_ClearsGateAndDoesNotForceVerification()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
        s.Artifacts.JourneyDigestProposed = """{"version":1}""";
        CampaignWorkflowEngine.ApplyUserMessage(s, "approve");
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, s.Phase);
    }

    [Fact]
    public void ApplyUserMessage_TestIntent_AfterJourneyEntryCheckpoint_ClearsGateAndShiftsFocusToVerification()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
        s.Artifacts.JourneyDigestProposed = """{"version":1}""";
        CampaignWorkflowEngine.ApplyUserMessage(s, "Awesome! Please test with a sample payload, you can use the test_001 user");
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.Equal(CampaignWorkflowPhase.Verification, s.Phase);
    }

    [Fact]
    public void GetCoachHint_PostBriefPendingModel_DoesNotUseBlockingPrefix()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        var hint = CampaignWorkflowStepManager.GetCoachHint(s);
        Assert.NotNull(hint);
        Assert.Contains("Coach", hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BLOCKING:", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapPhase_LegacyCampaignAndPat_MapsToCampaignBuild()
    {
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, CampaignWorkflowState.MapPhase("CampaignAndPat"));
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, CampaignWorkflowState.MapPhase("CampaignJourney"));
        Assert.Equal(CampaignWorkflowPhase.Verification, CampaignWorkflowState.MapPhase("Verify"));
    }

    [Fact]
    public void ShouldContinueAfterSegment_BriefGateCleared_WithPendingEventModel_Continues()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.BriefGateJustCleared = true;
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_BriefGateCleared_UserRequestedNewEventModel_Continues()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.BriefGateJustCleared = true;
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.UserRequestedNewEventModel = true;
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_BriefGateNotCleared_DoesNotContinue()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.UserRequestedNewEventModel = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_BriefGateCleared_EventModelsIncomplete_Continues()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.BriefGateJustCleared = true;
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_BriefGateCleared_EventModelsComplete_DoesNotContinue()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.BriefGateJustCleared = true;
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.UserSkippedEventModels = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_PhaseDone_DoesNotContinue()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.Done;
        s.Artifacts.BriefGateJustCleared = true;
        s.Artifacts.UserRequestedNewEventModel = true;
        s.Artifacts.VerificationRecord = """{"evaluatedCampaigns":["c1"],"status":"processed"}""";
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_true_for_verification_nudge()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"camp-1","journeyRuleSetCount":3}
            """;
        s.Artifacts.VerificationUserTestIntentThisTurn = true;
        s.Artifacts.TenantTestAccountAllowlistJson = """["test_exp_01"]""";

        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_SecondHop_BriefOnly_DoesNotContinue()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.BriefGateJustCleared = true;
        s.Artifacts.UserRequestedNewEventModel = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 1));
    }

    [Fact]
    public void ShouldContinueAfterSegment_SecondHop_EventModelsGate_Continues()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.EventModelsGateJustCleared = true;
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 1));
    }

    [Fact]
    public void ShouldContinueAfterSegment_ThirdHop_DoesNotContinue()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.EventModelsGateJustCleared = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 2));
    }

    [Fact]
    public void ShouldContinueAfterSegment_EventModelsGateJustCleared_Continues()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.EventModelsGateJustCleared = true;
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_EventModelsGateJustCleared_WithoutBrief_DoesNotContinue()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.EventModelsGateJustCleared = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_JourneyCheckpoint_DoesNotContinue()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.BriefGateJustCleared = true;
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_ValidationStalled_DoesNotContinue()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.EventModelsGateJustCleared = true;
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.ValidationStalled = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_DeferredMutatorRetry_with_open_gate_Continues()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.CampaignDesignBriefApproved = "{}";
        s.UserSkippedEventModels = true;
        s.Artifacts.DeferredMutatorRetry = true;
        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void BuildContinuationUserDirective_DeferredMutatorRetry_empty_manifest_pat_first()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.EventModelsGateJustCleared = true;
        s.Artifacts.DeferredMutatorRetry = true;
        s.UserSkippedEventModels = true;
        var directive = CampaignWorkflowEngine.BuildContinuationUserDirective(s);
        Assert.Contains("upsert_point_account_type", directive, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expired sink", directive, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildContinuationUserDirective_DeferredMutatorRetry_with_manifest_mentions_prior_upsert()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.EventModelsGateJustCleared = true;
        s.Artifacts.DeferredMutatorRetry = true;
        s.Artifacts.PointAccountManifest = """{"items":[{"id":"pat-1","displayLabel":"Earn","ledgerType":"Spendable"}]}""";
        var directive = CampaignWorkflowEngine.BuildContinuationUserDirective(s);
        Assert.Contains("prior upsert", directive, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildContinuationUserDirective_EventModelsGate_empty_manifest_pat_first()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.EventModelsGateJustCleared = true;
        s.UserSkippedEventModels = true;
        var directive = CampaignWorkflowEngine.BuildContinuationUserDirective(s);
        Assert.Contains("upsert_point_account_type", directive, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expired sink", directive, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryPrepareTurnStartPatCreation_returns_directive_when_pending_and_gate_open()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.CampaignDesignBriefApproved = "{}";
        s.UserSkippedEventModels = true;
        s.Artifacts.PatUpsertPending = true;
        var d = CampaignWorkflowEngine.TryPrepareTurnStartPatCreation(s);
        Assert.Contains("upsert_point_account_type", d!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildContinuationUserDirective_BriefGate_StrongMatch_mentions_get_model()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.BriefGateJustCleared = true;
        EventModelCandidatesArtifact.Write(s, new EventModelCandidateSet
        {
            RecommendedDefaultId = "o1",
            Candidates =
            [
                new EventModelCandidate { EventModelId = "o1", Name = "Order", IsStrongMatch = true }
            ]
        });
        var directive = CampaignWorkflowEngine.BuildContinuationUserDirective(s);
        Assert.Contains("get_model", directive, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Order", directive, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one-line", directive, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryPrepareTurnStartJourneyAuthoring_mentions_JourneyAuthoringTemplate_and_children_only()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefApproved = "{}";
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.JourneyPatternPrepComplete = true;
        s.Artifacts.JourneyPatternId = "tier-navigation-point-balance";
        s.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"TQP","role":"tierQualification"}]}""";
        s.Artifacts.JourneyAuthoringTemplateJson = """{"journey":{"children":[]}}""";

        var d = CampaignWorkflowEngine.TryPrepareTurnStartJourneyAuthoring(s);

        Assert.NotNull(d);
        Assert.Contains("JourneyAuthoringTemplate", d!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("children[]", d!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AggregateValueProvider", d!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validate_campaign", d!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryPrepareTurnStartJourneyAuthoring_returns_directive_when_prep_complete()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefApproved = "{}";
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.JourneyPatternPrepComplete = true;
        s.Artifacts.JourneyPatternId = "tier-navigation-point-balance";
        s.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"TQP","role":"tierQualification"}]}""";

        var d = CampaignWorkflowEngine.TryPrepareTurnStartJourneyAuthoring(s);
        Assert.Contains("tier-navigation-point-balance", d!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validate_campaign", d!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldRunEventModelPrep_BriefCaptured_EventsIncomplete_ReturnsTrue()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        Assert.True(CampaignWorkflowEngine.ShouldRunEventModelPrep(s));
    }

    [Fact]
    public void ShouldRunEventModelPrep_FetchFailed_still_returns_true()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        EventModelCandidatesArtifact.Write(s, new EventModelCandidateSet { FetchFailed = true });
        Assert.True(CampaignWorkflowEngine.ShouldRunEventModelPrep(s));
    }

    [Fact]
    public void ShouldRunEventModelPrep_RankedCandidatesPresent_returns_false()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        EventModelCandidatesArtifact.Write(s, new EventModelCandidateSet
        {
            Candidates = [new EventModelCandidate { EventModelId = "o1", Name = "Order" }]
        });
        Assert.False(CampaignWorkflowEngine.ShouldRunEventModelPrep(s));
    }

    [Fact]
    public void ShouldRunEventModelPrep_false_when_pat_boundary_active()
    {
        var s = PatBoundaryState();
        s.Artifacts.BriefGateJustCleared = true;
        Assert.False(CampaignWorkflowEngine.ShouldRunEventModelPrep(s));
    }

    [Fact]
    public void ShouldContinueAfterSegment_briefGate_blocked_when_pat_boundary_active()
    {
        var s = PatBoundaryState();
        s.Artifacts.BriefGateJustCleared = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueAfterSegment_eventModelsGateJustCleared_true_when_pat_boundary_active()
    {
        var s = PatBoundaryState();
        s.Artifacts.EventModelsGateJustCleared = true;
        Assert.True(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void BuildContinuationUserDirective_pat_boundary_returns_pat_first()
    {
        var s = PatBoundaryState();
        s.Artifacts.PatUpsertPending = true;
        var directive = CampaignWorkflowEngine.BuildContinuationUserDirective(s);
        Assert.Contains("upsert_point_account_type", directive, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expired sink", directive, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsPatBoundaryActive_true_when_patRequired_and_gate_open()
    {
        var s = PatBoundaryState();
        Assert.True(CampaignWorkflowEngine.IsPatBoundaryActive(s));
    }

    [Fact]
    public void ApplyToolResults_pins_phase_CampaignBuild_when_pat_boundary_active()
    {
        var s = PatBoundaryState();
        s.Phase = CampaignWorkflowPhase.EventModels;
        CampaignWorkflowStepManager.ApplyToolResults(s, []);
        Assert.Equal(CampaignWorkflowPhase.CampaignBuild, s.Phase);
    }

    [Fact]
    public void ShouldContinueAfterSegment_eventModelsGate_false_when_pat_boundary_and_empty_manifest_second_hop()
    {
        var s = PatBoundaryState();
        s.Artifacts.EventModelsGateJustCleared = true;
        Assert.False(CampaignWorkflowEngine.ShouldContinueAfterSegment(s, continuationHopsUsed: 1));
    }

    [Fact]
    public void IsJourneyBoundaryActive_true_when_manifest_populated_no_rule_sets()
    {
        var s = JourneyBoundaryState();
        Assert.True(CampaignWorkflowEngine.IsJourneyBoundaryActive(s));
    }

    [Fact]
    public void TryPrepareTurnStartPatCreation_active_boundary_without_patUpsertPending_returns_directive()
    {
        var s = PatBoundaryState();
        s.Artifacts.PatUpsertPending = false;
        var directive = CampaignWorkflowEngine.TryPrepareTurnStartPatCreation(s);
        Assert.NotNull(directive);
        Assert.Contains("upsert_point_account_type", directive, StringComparison.OrdinalIgnoreCase);
        Assert.True(s.Artifacts.PatUpsertPending);
    }

    private static CampaignWorkflowState JourneyBoundaryState()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier program"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        s.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Points","isSpendable":true}]}""";
        s.UserSkippedEventModels = true;
        return s;
    }

    private static CampaignWorkflowState PatBoundaryState()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier program"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        s.Artifacts.PointAccountManifest = """{"schemaVersion":1,"items":[]}""";
        return s;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string BriefWithPlannedIds(IEnumerable<string> ids) =>
        JsonSerializer.Serialize(new { version = 1, plannedEventModelIds = ids }, JsonOpts);

    private static string EventModelToolJson(string id, string name) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            tag = "eventable",
            modelType = "event",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            attributes = new[] { new { symbol = "orderid", type = "Primitive", dataType = "string", displayName = "Id" } }
        }, JsonOpts);
}
