using System.Linq;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal static class CampaignAgentJourneysMcp
{
    private static readonly string[] AssistantContextToolNameCandidates =
    {
        "GetCampaignAssistantContext",
        "get_campaign_assistant_context"
    };

    private static readonly string[] UpsertCampaignToolNameCandidates =
    {
        "UpsertCampaign",
        "upsert_campaign"
    };

    private static readonly string[] RulesContractToolNameCandidates =
    {
        "GetRulesEngineContractSummary",
        "get_rules_engine_contract_summary"
    };

    private static readonly string[] RulePatternRecipesToolNameCandidates =
    {
        "GetRulePatternRecipes",
        "get_rule_pattern_recipes"
    };

    private static readonly string[] ExampleCampaignToolNameCandidates =
    {
        "GetExampleCampaign",
        "get_example_campaign"
    };

    private static readonly string[] ListCampaignsToolNameCandidates =
    {
        "ListCampaigns",
        "list_campaigns"
    };

    private static readonly string[] ValidateCampaignToolNameCandidates =
    {
        "ValidateCampaign",
        "validate_campaign"
    };

    /// <summary>
    /// Applies all Journeys MCP digest wrappers (assistant context, upsert mutation, rules contract, example campaign).
    /// Already-wrapped tools are not double-wrapped.
    /// </summary>
    public static IReadOnlyList<AITool> WrapJourneysToolDigests(
        IEnumerable<AITool> tools,
        JourneysToolDigestOptions options,
        ILogger? logger,
        Func<CampaignWorkflowState>? getWorkflowState = null)
    {
        var list = tools.ToList();

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is not AIFunction fn)
                continue;

            if (options.AssistantContextDigestEnabled
                && fn is not CampaignAssistantContextDigestAIFunction
                && IsToolName(fn.Name, AssistantContextToolNameCandidates))
            {
                list[i] = new CampaignAssistantContextDigestAIFunction(fn, enabled: true, logger);
                continue;
            }

            if (options.MutationDigestEnabled
                && fn is not CampaignMutationDigestAIFunction
                && IsToolName(fn.Name, UpsertCampaignToolNameCandidates))
            {
                list[i] = new CampaignMutationDigestAIFunction(fn, enabled: true, logger);
                continue;
            }

            if (options.RulesContractDigestEnabled
                && fn is not RulesEngineContractDigestAIFunction
                && IsToolName(fn.Name, RulesContractToolNameCandidates))
            {
                list[i] = new RulesEngineContractDigestAIFunction(fn, enabled: true, logger);
                continue;
            }

            if (options.RulePatternRecipesDigestEnabled
                && fn is not RulePatternRecipesDigestAIFunction
                && IsToolName(fn.Name, RulePatternRecipesToolNameCandidates))
            {
                list[i] = new RulePatternRecipesDigestAIFunction(fn, enabled: true, logger, getWorkflowState);
                continue;
            }

            if (options.ExampleCampaignDigestEnabled
                && fn is not ExampleCampaignDigestAIFunction
                && IsToolName(fn.Name, ExampleCampaignToolNameCandidates))
            {
                list[i] = new ExampleCampaignDigestAIFunction(fn, enabled: true, logger, getWorkflowState);
                continue;
            }

            if (options.ListCampaignsDigestEnabled
                && fn is not ListCampaignsDigestAIFunction
                && IsToolName(fn.Name, ListCampaignsToolNameCandidates))
            {
                list[i] = new ListCampaignsDigestAIFunction(fn, enabled: true, logger);
                continue;
            }

            if (options.ValidationDigestEnabled
                && fn is not CampaignValidationDigestAIFunction
                && IsToolName(fn.Name, ValidateCampaignToolNameCandidates))
            {
                list[i] = new CampaignValidationDigestAIFunction(fn, enabled: true, logger);
            }
        }

        if (getWorkflowState is not null)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not AIFunction fn)
                    continue;

                if (IsToolName(fn.Name, UpsertCampaignToolNameCandidates))
                    list[i] = new LivePromotionGuardUpsertAIFunction(fn, getWorkflowState);
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not AIFunction fn)
                    continue;

                if (IsToolName(fn.Name, UpsertCampaignToolNameCandidates))
                {
                    if (fn is not JourneyAuthoringIntegrityAIFunction)
                        list[i] = new JourneyAuthoringIntegrityAIFunction(fn, getWorkflowState, isUpsert: true);
                }
                else if (IsToolName(fn.Name, ValidateCampaignToolNameCandidates))
                {
                    if (fn is not JourneyAuthoringIntegrityAIFunction)
                        list[i] = new JourneyAuthoringIntegrityAIFunction(fn, getWorkflowState, isUpsert: false);
                }
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not AIFunction fn)
                    continue;

                if (IsToolName(fn.Name, UpsertCampaignToolNameCandidates)
                    || IsToolName(fn.Name, ValidateCampaignToolNameCandidates))
                {
                    if (fn is not CampaignBuildGateAIFunction)
                        list[i] = new CampaignBuildGateAIFunction(fn, getWorkflowState);
                }
            }
        }

        return list;
    }

    /// <summary>Legacy entry point — wraps assistant context only. Prefer <see cref="WrapJourneysToolDigests"/>.</summary>
    public static IReadOnlyList<AITool> WrapReadDigests(
        IEnumerable<AITool> tools,
        bool enabled,
        ILogger? logger) =>
        WrapJourneysToolDigests(
            tools,
            new JourneysToolDigestOptions
            {
                AssistantContextDigestEnabled = enabled,
                MutationDigestEnabled = false,
                RulesContractDigestEnabled = false,
                ExampleCampaignDigestEnabled = false,
            },
            logger);

    private static bool IsToolName(string? name, IReadOnlyList<string> candidates) =>
        !string.IsNullOrEmpty(name)
        && candidates.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
}
