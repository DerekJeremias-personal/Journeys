using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentBackendMcpMutationWrapTests
{
    private sealed class NamedFunction : AIFunction
    {
        public NamedFunction(string name) => Name = name;
        public override string Name { get; }
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object?>("{}");
    }

    [Fact]
    public void WrapMutationDigest_wraps_only_upsert_campaign()
    {
        var tools = new List<AITool>
        {
            new NamedFunction("upsert_campaign"),
            new NamedFunction("get_all_models"),
            new NamedFunction("upsert_point_account_type"),
        };

        var result = CampaignAgentBackendMcp.WrapMutationDigest(tools, enabled: true, logger: null);

        Assert.IsType<CampaignMutationDigestAIFunction>(result[0]);
        Assert.IsType<NamedFunction>(result[1]);
        Assert.IsType<NamedFunction>(result[2]);
    }

    [Fact]
    public void WrapMutationDigest_passthrough_when_disabled()
    {
        var tools = new List<AITool> { new NamedFunction("upsert_campaign") };
        var result = CampaignAgentBackendMcp.WrapMutationDigest(tools, enabled: false, logger: null);
        Assert.IsType<NamedFunction>(result[0]);
    }

    [Fact]
    public void WrapMutationDigest_does_not_double_wrap()
    {
        var wrapped = new CampaignMutationDigestAIFunction(new NamedFunction("upsert_campaign"), enabled: true, logger: null);
        var tools = new List<AITool> { wrapped };
        var result = CampaignAgentBackendMcp.WrapMutationDigest(tools, enabled: true, logger: null);
        Assert.Same(wrapped, result[0]);
    }

    [Fact]
    public void WrapMutationDigest_matches_pascalcase_name()
    {
        var tools = new List<AITool> { new NamedFunction("UpsertCampaign") };
        var result = CampaignAgentBackendMcp.WrapMutationDigest(tools, enabled: true, logger: null);
        Assert.IsType<CampaignMutationDigestAIFunction>(result[0]);
    }
}
