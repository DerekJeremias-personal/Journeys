using System.Collections;
using System.Reflection;
using Backend.Dto.Structures.Model;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.DTO.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Journeys.Core.Services;

/// <summary>
/// Rejects campaign journeys where a <see cref="PathValueProvider"/> on a non-taxonomic <see cref="SimpleRule"/>
/// targets a <c>ModelTaxonomy</c> attribute path (must use <see cref="TaxonomicRule"/>).
/// </summary>
public sealed class CampaignTaxonomyRuleValidator
{
    private readonly IModelAdapter _modelAdapter;
    private readonly IOptions<CampaignUpsertValidationOptions> _options;
    private readonly ILogger<CampaignTaxonomyRuleValidator> _logger;

    public CampaignTaxonomyRuleValidator(
        IModelAdapter modelAdapter,
        IOptions<CampaignUpsertValidationOptions> options,
        ILogger<CampaignTaxonomyRuleValidator> logger)
    {
        _modelAdapter = modelAdapter;
        _options = options;
        _logger = logger;
    }

    public async Task ValidateAsync(string tenantId, Campaign campaign, CancellationToken cancellationToken = default)
    {
        if (campaign?.Journey == null)
            return;
        if (campaign.Events == null || campaign.Events.Count == 0)
            return;

        var eventModels = new Dictionary<string, ModelDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in campaign.Events)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            ModelDto? loaded = null;
            foreach (var modelType in _options.Value.EventModelTypeCandidates ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(modelType))
                    continue;
                loaded = await _modelAdapter.GetModelAsync(tenantId, id, modelType, false, cancellationToken).ConfigureAwait(false);
                if (loaded != null)
                    break;
            }

            if (loaded != null)
                eventModels[id] = loaded;
            else
                _logger.LogWarning("Campaign taxonomy validation: could not load event model {ModelId} for tenant {TenantId}", id, tenantId);
        }

        if (eventModels.Count == 0)
            return;

        var pathOwners = new List<(string Path, RuleBase Owner)>();
        var visitedRules = new HashSet<RuleBase>(ReferenceEqualityComparer.Instance);
        var visitedProviders = new HashSet<object>(ReferenceEqualityComparer.Instance);

        VisitJourneyNode(campaign.Journey, pathOwners, visitedRules, visitedProviders);

        var errors = new List<string>();
        foreach (var (path, owner) in pathOwners)
        {
            if (owner.Kind != RuleKindDiscriminators.SimpleRule)
                continue;

            var violation = false;
            string? prefix = null;
            foreach (var em in eventModels.Values)
            {
                var (isTaxonomy, p) = await ModelDtoPropertyPathResolver.TryResolveAsync(
                    path,
                    em,
                    (mid, mtype, ct) => _modelAdapter.GetModelAsync(tenantId, mid, mtype, false, ct),
                    cancellationToken).ConfigureAwait(false);

                if (isTaxonomy)
                {
                    violation = true;
                    prefix = p;
                    break;
                }
            }

            if (!violation)
                continue;

            var hint = string.IsNullOrEmpty(prefix)
                ? "Use Kind TaxonomicRule and BuildTaxonomicRule (see campaign governance)."
                : $"Use Kind TaxonomicRule with LeftProvider collection path and taxonomy metadata (e.g. left collection path '{prefix}'). Use BuildTaxonomicRule.";

            errors.Add(
                $"PropertyPath '{path}' resolves to a ModelTaxonomy attribute; SimpleRule is invalid for taxonomy membership. {hint}");
        }

        if (errors.Count == 0)
            return;

        var dict = new Dictionary<string, string>();
        for (var i = 0; i < errors.Count; i++)
            dict[$"journey.taxonomy.{i}"] = errors[i];

        throw new APIErrorsException(dict);
    }

    private void VisitJourneyNode(
        JourneyNode node,
        List<(string Path, RuleBase Owner)> pathOwners,
        HashSet<RuleBase> visitedRules,
        HashSet<object> visitedProviders)
    {
        if (node.Rules != null)
        {
            foreach (var ruleSet in node.Rules)
            {
                VisitRule(ruleSet?.RuleTree, pathOwners, visitedRules, visitedProviders);
                if (ruleSet?.Outcomes != null)
                {
                    foreach (var outcome in ruleSet.Outcomes)
                        VisitOutcome(outcome, pathOwners, visitedRules, visitedProviders);
                }
            }
        }

        if (node.Children == null)
            return;

        foreach (var child in node.Children)
            VisitJourneyNode(child, pathOwners, visitedRules, visitedProviders);
    }

    private void VisitOutcome(
        OutcomeBase? outcome,
        List<(string Path, RuleBase Owner)> pathOwners,
        HashSet<RuleBase> visitedRules,
        HashSet<object> visitedProviders)
    {
        if (outcome == null)
            return;

        foreach (var prop in outcome.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.GetIndexParameters().Length != 0)
                continue;
            if (!typeof(IValueProvider).IsAssignableFrom(prop.PropertyType))
                continue;
            var value = prop.GetValue(outcome) as IValueProvider;
            VisitValueProvider(value, pathOwners, visitedRules, visitedProviders, owningRule: null);
        }
    }

    private void VisitRule(
        RuleBase? rule,
        List<(string Path, RuleBase Owner)> pathOwners,
        HashSet<RuleBase> visitedRules,
        HashSet<object> visitedProviders)
    {
        if (rule == null)
            return;

        if (rule is TaxonomicRule)
            return;

        if (!visitedRules.Add(rule))
            return;

        if (rule is CompositeRuleBase composite)
        {
            if (composite.Children != null)
            {
                foreach (var child in composite.Children)
                    VisitRule(child, pathOwners, visitedRules, visitedProviders);
            }
            return;
        }

        foreach (var prop in rule.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.GetIndexParameters().Length != 0)
                continue;

            var value = prop.GetValue(rule);
            if (value == null)
                continue;

            if (typeof(IValueProvider).IsAssignableFrom(prop.PropertyType))
                VisitValueProvider((IValueProvider)value, pathOwners, visitedRules, visitedProviders, rule);
            else if (typeof(RuleBase).IsAssignableFrom(prop.PropertyType))
                VisitRule((RuleBase)value, pathOwners, visitedRules, visitedProviders);
        }
    }

    private void VisitValueProvider(
        IValueProvider? provider,
        List<(string Path, RuleBase Owner)> pathOwners,
        HashSet<RuleBase> visitedRules,
        HashSet<object> visitedProviders,
        RuleBase? owningRule)
    {
        if (provider == null)
            return;

        if (!visitedProviders.Add(provider))
            return;

        switch (provider)
        {
            case PathValueProvider path:
                if (owningRule != null && owningRule.Kind == RuleKindDiscriminators.SimpleRule && !string.IsNullOrWhiteSpace(path.PropertyPath))
                    pathOwners.Add((path.PropertyPath, owningRule));
                return;
            case AggregateValueProvider agg:
                VisitValueProvider(agg.RowProvider, pathOwners, visitedRules, visitedProviders, owningRule);
                VisitValueProvider(agg.RowPropertyProvider, pathOwners, visitedRules, visitedProviders, owningRule);
                VisitRule(agg.Constraint, pathOwners, visitedRules, visitedProviders);
                return;
            case SimpleCalculationProvider calc:
                VisitValueProvider(calc.InstanceValueProvider, pathOwners, visitedRules, visitedProviders, owningRule);
                VisitRule(calc.CalculationGatingConstraint, pathOwners, visitedRules, visitedProviders);
                VisitRule(calc.TemporalConstraint, pathOwners, visitedRules, visitedProviders);
                return;
            default:
                VisitNestedValueProvidersByReflection(provider, pathOwners, visitedRules, visitedProviders, owningRule);
                return;
        }
    }

    private void VisitNestedValueProvidersByReflection(
        IValueProvider provider,
        List<(string Path, RuleBase Owner)> pathOwners,
        HashSet<RuleBase> visitedRules,
        HashSet<object> visitedProviders,
        RuleBase? owningRule)
    {
        foreach (var prop in provider.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.GetIndexParameters().Length != 0)
                continue;
            if (!typeof(IValueProvider).IsAssignableFrom(prop.PropertyType))
                continue;
            var nested = prop.GetValue(provider) as IValueProvider;
            VisitValueProvider(nested, pathOwners, visitedRules, visitedProviders, owningRule);
        }
    }
}
