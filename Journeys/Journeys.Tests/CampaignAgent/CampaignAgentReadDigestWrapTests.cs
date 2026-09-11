using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentReadDigestWrapTests
{
    private sealed class NamedFunction : AIFunction
    {
        public NamedFunction(string name) => Name = name;
        public override string Name { get; }
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object?>("{}");
    }

    [Fact]
    public void WrapModelReadDigest_wraps_only_get_model()
    {
        var tools = new List<AITool>
        {
            new NamedFunction("get_model"),
            new NamedFunction("get_all_models"),
            new NamedFunction("get_model_attributes_for_rules"),
        };

        var result = CampaignAgentBackendMcp.WrapModelReadDigest(tools, enabled: true, logger: null);

        Assert.IsType<ModelReadDigestAIFunction>(result[0]);
        Assert.IsType<NamedFunction>(result[1]);
        Assert.IsType<NamedFunction>(result[2]);
    }

    [Fact]
    public void WrapReadDigests_wraps_assistant_context_on_journeys_tools()
    {
        var tools = new List<AITool>
        {
            new NamedFunction("get_campaign_assistant_context"),
            new NamedFunction("upsert_campaign"),
        };

        var result = CampaignAgentJourneysMcp.WrapReadDigests(tools, enabled: true, logger: null);

        Assert.IsType<CampaignAssistantContextDigestAIFunction>(result[0]);
        Assert.IsType<NamedFunction>(result[1]);
    }

    [Fact]
    public void WrapReadDigests_does_not_double_wrap()
    {
        var wrapped = new CampaignAssistantContextDigestAIFunction(new NamedFunction("get_campaign_assistant_context"), enabled: true, logger: null);
        var tools = new List<AITool> { wrapped };
        var result = CampaignAgentJourneysMcp.WrapReadDigests(tools, enabled: true, logger: null);
        Assert.Same(wrapped, result[0]);
    }
}
