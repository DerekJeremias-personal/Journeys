using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public class OllamaToolRequireTests
{
    [Fact]
    public void ShouldRequire_true_for_ollama_with_tools_and_not_done()
    {
        Assert.True(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.DataAnalysis));
    }

    [Theory]
    [InlineData(CampaignWorkflowPhase.EventModels)]
    [InlineData(CampaignWorkflowPhase.CampaignBuild)]
    [InlineData(CampaignWorkflowPhase.Verification)]
    public void ShouldRequire_true_for_other_non_done_phases(CampaignWorkflowPhase phase)
    {
        Assert.True(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 1, phase));
    }

    [Fact]
    public void ShouldRequire_false_for_anthropic()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.Anthropic, 3, CampaignWorkflowPhase.DataAnalysis));
    }

    [Fact]
    public void ShouldRequire_false_when_no_tools()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 0, CampaignWorkflowPhase.DataAnalysis));
    }

    [Fact]
    public void ShouldRequire_false_when_done()
    {
        Assert.False(OllamaToolRequire.ShouldRequire(
            CampaignAgentLlmProviderKind.OpenAICompatible, 2, CampaignWorkflowPhase.Done));
    }

    [Fact]
    public void Apply_sets_RequireAny_when_should_require()
    {
        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
        OllamaToolRequire.Apply(
            options, CampaignAgentLlmProviderKind.OpenAICompatible, CampaignWorkflowPhase.DataAnalysis);
        Assert.Equal(ChatToolMode.RequireAny, options.ToolMode);
    }

    [Fact]
    public void Apply_leaves_ToolMode_unset_for_anthropic()
    {
        var options = new ChatOptions { Tools = [new TestTool("list_campaigns")] };
        OllamaToolRequire.Apply(
            options, CampaignAgentLlmProviderKind.Anthropic, CampaignWorkflowPhase.DataAnalysis);
        Assert.Null(options.ToolMode);
    }

    private sealed class TestTool(string name) : AITool
    {
        public override string Name => name;
        public override string Description => name;
    }
}
