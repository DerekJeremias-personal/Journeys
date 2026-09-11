using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;

namespace Journeys.Tests.CampaignAgent;

public sealed class CampaignAgentBackendMcpWrapTests
{
    [Fact]
    public void WrapCatalogDigest_wraps_list_example_models()
    {
        var tools = new List<AITool> { new NamedFunction("list_example_models") };

        var result = CampaignAgentBackendMcp.WrapCatalogDigest(
            tools, forcedTag: "eventable", enabled: true, logger: null);

        Assert.IsType<ModelCatalogDigestAIFunction>(result[0]);
    }

    [Fact]
    public void WrapCatalogDigest_wraps_only_catalog_tools()
    {
        var tools = new List<AITool>
        {
            new NamedFunction("get_all_models"),
            new NamedFunction("list_models"),
            new NamedFunction("get_model"),
            new NamedFunction("save_model"),
        };

        var result = CampaignAgentBackendMcp.WrapCatalogDigest(
            tools, forcedTag: "eventable", enabled: true, logger: null);

        Assert.IsType<ModelCatalogDigestAIFunction>(result[0]);
        Assert.IsType<ModelCatalogDigestAIFunction>(result[1]);
        Assert.IsType<NamedFunction>(result[2]);
        Assert.IsType<NamedFunction>(result[3]);
    }

    [Fact]
    public void WrapCatalogDigest_passthrough_when_disabled()
    {
        var tools = new List<AITool> { new NamedFunction("get_all_models") };

        var result = CampaignAgentBackendMcp.WrapCatalogDigest(
            tools, forcedTag: "eventable", enabled: false, logger: null);

        Assert.IsType<NamedFunction>(result[0]);
    }

    [Fact]
    public void WrapCatalogDigest_does_not_double_wrap()
    {
        var originalWrapper = new ModelCatalogDigestAIFunction(
            new NamedFunction("get_all_models"), forcedTag: "eventable", enabled: true, logger: null);
        var tools = new List<AITool> { originalWrapper };

        var result = CampaignAgentBackendMcp.WrapCatalogDigest(
            tools, forcedTag: "eventable", enabled: true, logger: null);

        Assert.Same(originalWrapper, result[0]);
    }

    private sealed class NamedFunction : AIFunction
    {
        public NamedFunction(string name) => Name = name;
        public override string Name { get; }
        public override string Description => Name;
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object?>(null);
    }
}
