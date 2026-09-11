using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public class CampaignWorkflowToolFilterTests
{
    private static AITool Tool(string name) => new TestTool(name);

    [Fact]
    public void PreBrief_WarehouseEnabled_AllowsWarehouseAndReads_BlocksMutators()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var tools = new List<AITool>
        {
            Tool("GetProgramPerformanceSummary"),
            Tool("ListCampaigns"),
            Tool("UpsertCampaign"),
            Tool("SaveModel")
        };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state, dataWarehouseEnabled: true);
        Assert.Contains(filtered, t => t.Name == "GetProgramPerformanceSummary");
        Assert.Contains(filtered, t => t.Name == "ListCampaigns");
        Assert.DoesNotContain(filtered, t => t.Name == "UpsertCampaign");
        Assert.DoesNotContain(filtered, t => t.Name == "SaveModel");
    }

    [Fact]
    public void PreBrief_WarehouseDisabled_AllowsObjectiveTool_BlocksMutators()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var tools = new List<AITool>
        {
            Tool("ProposeCampaignDesignBrief"),
            Tool("UpsertCampaign"),
            Tool("GetModel")
        };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state, dataWarehouseEnabled: false);
        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, t => t.Name == "ProposeCampaignDesignBrief");
        Assert.Contains(filtered, t => t.Name == "GetModel");
        Assert.DoesNotContain(filtered, t => t.Name == "UpsertCampaign");
    }

    [Fact]
    public void PostBrief_AllowsAllTools_WhenEventModelsComplete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        PendingEventModelSpecArtifact.Write(state, new PendingEventModelSpecDto { Name = "Review" });
        CampaignWorkflowStepManager.ApplyToolResults(state,
        [
            ("save_model",
                """{"id":"r1","name":"Review","modelType":"loyalty","tag":"eventable","modelMetaData":{"Wrapper":"w1","NaturalKeySymbols":"[\"reviewid\"]","AccountXIdSymbol":"customerid"},"attributes":[]}""")
        ]);
        state.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","wrapperModelName":"ReviewAndRuleState","errors":[],"warnings":[]}]""";
        CampaignWorkflowStepManager.TryUpdateChecklist(state);
        Assert.True(state.ModelGatePassed);

        var tools = new List<AITool>
        {
            Tool("UpsertCampaign"),
            Tool("SaveModel"),
            Tool("UpsertPointAccountType"),
            Tool("ProcessEvent")
        };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);
        Assert.Equal(4, filtered.Count);
    }

    [Fact]
    public void PostBrief_EventModelsIncomplete_BlocksCampaignMutators()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.UserRequestedNewEventModel = true;
        state.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","errors":["Missing required wrapper attribute 'accountid'."]}]""";

        var tools = new List<AITool>
        {
            Tool("UpsertCampaign"),
            Tool("SaveModel"),
            Tool("GetModel"),
            Tool("ProcessEvent"),
            Tool("ListCampaigns")
        };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);
        Assert.Contains(filtered, t => t.Name == "SaveModel");
        Assert.Contains(filtered, t => t.Name == "GetModel");
        Assert.Contains(filtered, t => t.Name == "ListCampaigns");
        Assert.DoesNotContain(filtered, t => t.Name == "UpsertCampaign");
        Assert.DoesNotContain(filtered, t => t.Name == "ProcessEvent");
    }

    [Fact]
    public void build_event_wrapper_allowed_during_event_models_gate()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.UserRequestedNewEventModel = true;
        state.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","errors":["Missing required wrapper attribute 'providerstates'."]}]""";

        var tools = new List<AITool>
        {
            Tool("build_event_wrapper"),
            Tool("BuildEventWrapper"),
            Tool("UpsertCampaign")
        };

        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);

        Assert.Contains(filtered, t => t.Name == "build_event_wrapper");
        Assert.Contains(filtered, t => t.Name == "BuildEventWrapper");
        Assert.DoesNotContain(filtered, t => t.Name == "UpsertCampaign");
    }

    [Fact]
    public void PostBrief_AllowsAllTools()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.UserSkippedEventModels = true;
        var tools = new List<AITool>
        {
            Tool("UpsertCampaign"),
            Tool("SaveModel"),
            Tool("UpsertPointAccountType"),
            Tool("ProcessEvent")
        };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);
        Assert.Equal(4, filtered.Count);
    }

    [Fact]
    public void PostBrief_JourneyCheckpoint_RemovesValidateAndUpsert()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.UserSkippedEventModels = true;
        state.ModelGatePassed = true;
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;

        var tools = new List<AITool>
        {
            Tool("validate_campaign"),
            Tool("upsert_campaign"),
            Tool("get_rules_engine_contract_summary"),
            Tool("get_campaign_assistant_context")
        };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);
        Assert.DoesNotContain(filtered, t => t.Name == "validate_campaign");
        Assert.DoesNotContain(filtered, t => t.Name == "upsert_campaign");
        Assert.Contains(filtered, t => t.Name == "get_rules_engine_contract_summary");
    }

    [Fact]
    public void PostBrief_ValidationStalled_RemovesValidateAndUpsert()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.UserSkippedEventModels = true;
        state.ModelGatePassed = true;
        state.Artifacts.ValidationStalled = true;

        var tools = new List<AITool> { Tool("validate_campaign"), Tool("UpsertCampaign") };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);
        Assert.Empty(filtered);
    }

    [Fact]
    public void PostBrief_HidesListCampaigns_when_shell_campaign_id_known()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.UserSkippedEventModels = true;
        state.ModelGatePassed = true;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-thread-1","name":"My campaign"}""";

        var tools = new List<AITool> { Tool("list_campaigns"), Tool("get_campaign") };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);

        Assert.DoesNotContain(filtered, t => t.Name == "list_campaigns");
        Assert.Contains(filtered, t => t.Name == "get_campaign");
    }

    [Fact]
    public void PreBrief_StillAllowsListCampaigns()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        var tools = new List<AITool> { Tool("ListCampaigns"), Tool("UpsertCampaign") };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state, dataWarehouseEnabled: true);
        Assert.Contains(filtered, t => t.Name == "ListCampaigns");
    }

    [Fact]
    public void PatRequired_gate_open_blocks_validate_and_post_shell_upsert()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.UserSkippedEventModels = true;
        state.ModelGatePassed = true;
        state.Artifacts.CampaignShellRef = """{"campaignId":"shell-1","name":"Tier campaign"}""";

        var tools = new List<AITool>
        {
            Tool("validate_campaign"),
            Tool("upsert_campaign"),
            Tool("upsert_point_account_type"),
            Tool("get_model")
        };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);

        Assert.DoesNotContain(filtered, t => t.Name == "validate_campaign");
        Assert.DoesNotContain(filtered, t => t.Name == "upsert_campaign");
        Assert.Contains(filtered, t => t.Name == "upsert_point_account_type");
        Assert.Contains(filtered, t => t.Name == "get_model");
    }

    [Fact]
    public void PatRequired_gate_open_allows_shell_upsert_before_shell_ref()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.UserSkippedEventModels = true;
        state.ModelGatePassed = true;

        var tools = new List<AITool> { Tool("upsert_campaign"), Tool("upsert_point_account_type") };
        var filtered = CampaignWorkflowToolFilter.Apply(tools, state);

        Assert.Contains(filtered, t => t.Name == "upsert_campaign");
        Assert.Contains(filtered, t => t.Name == "upsert_point_account_type");
    }

    private sealed class TestTool(string name) : AITool
    {
        public override string Name => name;
        public override string Description => name;
    }
}
