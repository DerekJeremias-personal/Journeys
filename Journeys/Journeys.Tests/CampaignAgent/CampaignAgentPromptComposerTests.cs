using Journeys.API.CampaignAgent;
using Journeys.API.CampaignAgent.DataWarehouse;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentPromptComposerTests
{
    private static readonly string ApiContentRoot = JourneysApiContentPaths.ContentRoot;

    [Theory]
    [InlineData(CampaignWorkflowPhase.DataAnalysis, "DATA ANALYSIS", "WAREHOUSE TOOL PLAYBOOK")]
    [InlineData(CampaignWorkflowPhase.EventModels, "EVENT MODELS", "EVENT MODEL PHASE CHECKLIST")]
    [InlineData(CampaignWorkflowPhase.CampaignSetup, "CAMPAIGN BUILD", "CAMPAIGN BUILD GATE")]
    [InlineData(CampaignWorkflowPhase.PointAccountTypes, "CAMPAIGN BUILD", "CAMPAIGN BUILD GATE")]
    [InlineData(CampaignWorkflowPhase.CampaignJourney, "CAMPAIGN BUILD", "CAMPAIGN BUILD GATE")]
    [InlineData(CampaignWorkflowPhase.CampaignBuild, "CAMPAIGN BUILD", "CAMPAIGN BUILD GATE")]
    [InlineData(CampaignWorkflowPhase.Verification, "VERIFICATION", "VERIFICATION STEPS (ordered)")]
    public async Task BuildAsync_LoadsCoreSharedAndSinglePhaseSection(
        CampaignWorkflowPhase phase,
        string expectedPhaseHeading,
        string phaseSpecificMarker)
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("tenant-a", "user-1", "conv-1");
        state.Phase = phase;
        if (phase != CampaignWorkflowPhase.DataAnalysis)
            state.Artifacts.CampaignDesignBriefProposed = "Test brief for phase governance.";

        var ctx = await composer.BuildAsync("tenant-a", null, null, null, state);

        Assert.Contains("SHARED TOOLING GOVERNANCE", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("CAMPAIGN GOVERNANCE — CORE", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains(expectedPhaseHeading, ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains(phaseSpecificMarker, ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_DataAnalysis_DoesNotLoadJourneyOrRulesEngineSections()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.DataAnalysis;

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.DoesNotContain("RULES ENGINE — CAMPAIGN CONTRACT", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNEYS AND MEMBER PROGRESSION", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_JourneyPhase_ExcludesEventModelsPhase1Block()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignDesignBriefProposed = "Test brief for journey phase.";
        state.Artifacts.PointAccountManifest = """
            {"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Spendable","isSpendable":true}]}
            """;

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("RULES ENGINE", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("EVENT MODEL PHASE CHECKLIST", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_SessionContainsTenantAndThreadContextWhenMessagesPresent()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("tenant-x", "user-1", "conv-1");
        var callId = "call-1";
        var campaignId = Guid.NewGuid().ToString();
        var envelope = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(callId, "UpsertCampaign", new Dictionary<string, object?>
                    {
                        ["tenantId"] = "tenant-x",
                        ["campaignJson"] = $$"""{"id":"{{campaignId}}","name":"Test","status":"Draft"}"""
                    })
                ]));

        var messages = new List<AgentMessage>
        {
            new("tenant-x", "user-1", "conv-1", 1, "user", "create", null, null, null, null, null),
            new("tenant-x", "user-1", "conv-1", 2, "assistant", envelope, null, null, null, null, null),
            new("tenant-x", "user-1", "conv-1", 3, "tool", "", null, callId, null, null,
                $$"""{"id":"{{campaignId}}","name":"Test","status":"draft"}""")
        };

        var ctx = await composer.BuildAsync("tenant-x", "linked-99", messages, "WORKFLOW: DataAnalysis", state);

        Assert.Contains("authenticated tenantId is \"tenant-x\"", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("linkedCampaignId \"linked-99\"", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("THREAD CONTEXT", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("WORKFLOW: DataAnalysis", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_IncludesWorkflowCoachGovernance()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.DataAnalysis;

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("WORKFLOW COACH (mandatory)", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("COACH MODE", ctx.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("checklist is advisory", ctx.Instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_PreBrief_ForcesDataAnalysisPhaseGovernance()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("DATA ANALYSIS", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("WAREHOUSE TOOL PLAYBOOK", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("TIER A PRE-FLIGHT (journey upsert)", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNEY AUTHORING (after manifest populated)", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_DataAnalysis_WarehouseDisabled_LoadsObjectiveGovernance()
    {
        var composer = CreateComposer(dataWarehouseEnabled: false);
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.DataAnalysis;

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("objective mode", ctx.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ProposeCampaignDesignBrief", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("WAREHOUSE TOOL PLAYBOOK", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_PointAccountTypesPhase_IncludesFullPatModelSection()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.PointAccountTypes;
        state.Artifacts.CampaignDesignBriefProposed = "Tier program with expiration.";

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("POINT ACCOUNT MODEL", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("Golden bundle A", ctx.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expiresToPointAccountTypeId", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_JourneyPhase_IncludesPatJourneyCheatSheet_NotFullGoldenBundles()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignDesignBriefProposed = "Tier journey.";
        state.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("PAT→JOURNEY CHEAT SHEET", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("PointBalanceProvider", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("CAMPAIGN DTO CONTAINER SHAPE", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("Golden bundle A", ctx.Instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_JourneyPhase_includes_pattern_governance()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignDesignBriefProposed = "Tier journey with rolling spend thresholds.";
        state.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("RULES ENGINE PATTERNS", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("PointBalanceProvider", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("Scope discipline", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("get_rule_pattern_recipes once per journey episode when ruleEpisode is Generic",
            ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_VerificationPhase_includes_pattern_verification_cheat_sheet()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.Verification;
        state.Artifacts.CampaignDesignBriefProposed = "Verify tier campaign.";

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("RULES ENGINE VERIFICATION", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("AppliedRuleSetIds", ctx.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("Scope discipline", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_EventModelsPhase_ExcludesPatModelSection()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.Artifacts.CampaignDesignBriefProposed = "Event models.";

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.DoesNotContain("Golden bundle A", ctx.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PAT→JOURNEY CHEAT SHEET", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_EventModelsPhase_PatRequired_includesPointAccountModelSection()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.UserSkippedEventModels = true;
        state.ModelGatePassed = true;
        state.Artifacts.CampaignDesignBriefProposed = "Tier program with PATs.";
        state.Artifacts.PointAccountManifest = """{"schemaVersion":1,"items":[]}""";

        var ctx = await composer.BuildAsync("t", null, null, null, state, ["upsert_point_account_type"]);

        Assert.Contains("POINT ACCOUNT MODEL", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_IncludesJsonCasingContractSection()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.CampaignBuild;
        state.Artifacts.CampaignDesignBriefProposed = "Brief";

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("JSON CASING CONTRACT", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("AffectedPointAccountTypeIds", ctx.Instructions, StringComparison.Ordinal);
        Assert.Contains("Never guess casing", ctx.Instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_AppendsOllamaAddendum_WhenProviderOllama()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CampaignAgent:Provider"] = "Ollama"
            })
            .Build();
        var composer = CreateComposer(configuration: config);
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.DataAnalysis;

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.Contains("OLLAMA / SMALL MODEL", ctx.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_DoesNotAppendOllamaAddendum_WhenProviderDefault()
    {
        var composer = CreateComposer();
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.DataAnalysis;

        var ctx = await composer.BuildAsync("t", null, null, null, state);

        Assert.DoesNotContain("OLLAMA / SMALL MODEL", ctx.Instructions, StringComparison.Ordinal);
    }

    private static CampaignAgentPromptComposer CreateComposer(
        bool dataWarehouseEnabled = true,
        IConfiguration? configuration = null) =>
        new(
            new TestHostEnvironment(ApiContentRoot),
            new MemoryCache(new MemoryCacheOptions()),
            new NullTenantContextProvider(),
            Options.Create(new DataWarehouseProxyOptions { Enabled = dataWarehouseEnabled }),
            NullLogger<CampaignAgentPromptComposer>.Instance,
            configuration ?? new ConfigurationBuilder().Build());

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Journeys.API";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
    }

    private sealed class NullTenantContextProvider : ICampaignAgentTenantContextProvider
    {
        public Task<CampaignAgentTenantLlmSlice?> GetSliceAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CampaignAgentTenantLlmSlice?>(null);
    }
}
