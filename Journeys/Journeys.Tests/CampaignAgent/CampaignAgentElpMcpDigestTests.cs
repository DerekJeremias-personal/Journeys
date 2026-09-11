using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentJourneysMcpDigestTests
{
    private sealed class NamedFunction : AIFunction
    {
        public NamedFunction(string name) => Name = name;
        public override string Name { get; }
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object?>("{}");
    }

    [Fact]
    public void WrapJourneysToolDigests_wraps_all_digest_tools()
    {
        var tools = new List<AITool>
        {
            new NamedFunction("get_campaign_assistant_context"),
            new NamedFunction("upsert_campaign"),
            new NamedFunction("get_rules_engine_contract_summary"),
            new NamedFunction("get_rule_pattern_recipes"),
            new NamedFunction("get_example_campaign"),
            new NamedFunction("validate_campaign"),
            new NamedFunction("list_campaigns"),
            new NamedFunction("process_event"),
        };

        var result = CampaignAgentJourneysMcp.WrapJourneysToolDigests(
            tools,
            new JourneysToolDigestOptions
            {
                AssistantContextDigestEnabled = true,
                MutationDigestEnabled = true,
                RulesContractDigestEnabled = true,
                RulePatternRecipesDigestEnabled = true,
                ExampleCampaignDigestEnabled = true,
                ValidationDigestEnabled = true,
                ListCampaignsDigestEnabled = true,
            },
            logger: null);

        Assert.IsType<CampaignAssistantContextDigestAIFunction>(result[0]);
        Assert.IsType<CampaignMutationDigestAIFunction>(result[1]);
        Assert.IsType<RulesEngineContractDigestAIFunction>(result[2]);
        Assert.IsType<RulePatternRecipesDigestAIFunction>(result[3]);
        Assert.IsType<ExampleCampaignDigestAIFunction>(result[4]);
        Assert.IsType<CampaignValidationDigestAIFunction>(result[5]);
        Assert.IsType<ListCampaignsDigestAIFunction>(result[6]);
        Assert.IsType<NamedFunction>(result[7]);
    }

    [Fact]
    public void WrapJourneysToolDigests_does_not_double_wrap()
    {
        var wrapped = new CampaignValidationDigestAIFunction(new NamedFunction("validate_campaign"), enabled: true, logger: null);
        var tools = new List<AITool> { wrapped };
        var result = CampaignAgentJourneysMcp.WrapJourneysToolDigests(
            tools,
            new JourneysToolDigestOptions { ValidationDigestEnabled = true },
            logger: null);
        Assert.Same(wrapped, result[0]);
    }

    [Fact]
    public void WrapJourneysToolDigests_does_not_double_wrap_mutation()
    {
        var wrapped = new CampaignMutationDigestAIFunction(new NamedFunction("upsert_campaign"), enabled: true, logger: null);
        var tools = new List<AITool> { wrapped };
        var result = CampaignAgentJourneysMcp.WrapJourneysToolDigests(
            tools,
            new JourneysToolDigestOptions { MutationDigestEnabled = true },
            logger: null);
        Assert.Same(wrapped, result[0]);
    }

    [Fact]
    public void WrapJourneysToolDigests_disabled_flags_leave_tools_unwrapped()
    {
        var tools = new List<AITool>
        {
            new NamedFunction("upsert_campaign"),
            new NamedFunction("get_rules_engine_contract_summary"),
        };

        var result = CampaignAgentJourneysMcp.WrapJourneysToolDigests(
            tools,
            new JourneysToolDigestOptions
            {
                MutationDigestEnabled = false,
                RulesContractDigestEnabled = false,
            },
            logger: null);

        Assert.IsType<NamedFunction>(result[0]);
        Assert.IsType<NamedFunction>(result[1]);
    }

    [Fact]
    public void WrapReadDigests_still_wraps_assistant_context_only()
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
}
