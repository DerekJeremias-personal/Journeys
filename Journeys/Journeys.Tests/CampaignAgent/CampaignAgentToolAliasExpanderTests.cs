using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentToolAliasExpanderTests
{
    [Fact]
    public void Expand_adds_PascalCase_alias_for_snake_case_tool()
    {
        var tools = new List<AITool> { new RecordingFunction("upsert_campaign") };
        var result = CampaignAgentToolAliasExpander.Expand(tools, enabled: true, logger: null);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => string.Equals(t.Name, "UpsertCampaign", StringComparison.Ordinal));
    }

    [Fact]
    public void Expand_passthrough_when_disabled()
    {
        var tools = new List<AITool> { new RecordingFunction("upsert_campaign") };
        var result = CampaignAgentToolAliasExpander.Expand(tools, enabled: false, logger: null);
        Assert.Single(result);
    }

    [Fact]
    public async Task ToolAlias_invokes_inner_function()
    {
        var inner = new RecordingFunction("upsert_campaign");
        var alias = new ToolAliasAIFunction(inner, "UpsertCampaign");
        _ = await alias.InvokeAsync(new AIFunctionArguments());
        Assert.True(inner.Invoked);
    }

    private sealed class RecordingFunction : AIFunction
    {
        public RecordingFunction(string name) => Name = name;
        public override string Name { get; }
        public bool Invoked { get; private set; }
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            Invoked = true;
            return ValueTask.FromResult<object?>("ok");
        }
    }
}
