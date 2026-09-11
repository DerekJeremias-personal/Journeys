using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Services;

/// <summary>
/// Resolves <see cref="PointOutcomeBase.AffectedPointAccountTypeIds"/> against tenant PAT cache during upsert/validate.
/// </summary>
public sealed class CampaignJourneyPointOutcomePatValidator
{
    private readonly IPointAccountTypeCache _cache;

    public CampaignJourneyPointOutcomePatValidator(IPointAccountTypeCache cache) =>
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public async Task ValidateAsync(string tenantId, Campaign campaign, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));
        if (campaign?.Journey == null)
            return;

        var errors = new List<string>();
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await VisitNodeAsync(tenantId, campaign.Journey, errors, resolved, cancellationToken).ConfigureAwait(false);

        if (errors.Count == 0)
            return;

        var dict = new Dictionary<string, string>();
        for (var i = 0; i < errors.Count; i++)
            dict[$"journey.validation.{i}"] = errors[i];

        throw new APIErrorsException(dict);
    }

    private async Task VisitNodeAsync(
        string tenantId,
        JourneyNode node,
        List<string> errors,
        HashSet<string> resolved,
        CancellationToken cancellationToken)
    {
        var nodePath = $"nodeId={node.Id}";

        if (node.Rules != null)
        {
            foreach (var ruleSet in node.Rules)
            {
                if (ruleSet?.Outcomes == null)
                    continue;

                var ruleSetLabel = RuleSetDisplayName(ruleSet);
                for (var i = 0; i < ruleSet.Outcomes.Count; i++)
                {
                    if (ruleSet.Outcomes[i] is PointOutcomeBase pointOutcome)
                    {
                        await ValidatePointOutcomeAsync(
                            tenantId,
                            pointOutcome,
                            ruleSetLabel,
                            nodePath,
                            i,
                            errors,
                            resolved,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        if (node.Children == null)
            return;

        for (var c = 0; c < node.Children.Count; c++)
        {
            if (node.Children[c] != null)
                await VisitNodeAsync(tenantId, node.Children[c], errors, resolved, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ValidatePointOutcomeAsync(
        string tenantId,
        PointOutcomeBase outcome,
        string ruleSetDisplayName,
        string journeyNodePath,
        int outcomeIndex,
        List<string> errors,
        HashSet<string> resolved,
        CancellationToken cancellationToken)
    {
        var ids = outcome.AffectedPointAccountTypeIds;
        if (ids == null)
            return;

        for (var patIndex = 0; patIndex < ids.Count; patIndex++)
        {
            var patId = ids[patIndex];
            if (string.IsNullOrWhiteSpace(patId))
                continue;

            var cacheKey = $"{tenantId}:{patId}";
            if (!resolved.Add(cacheKey))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            var pat = await _cache.GetPointAccountTypeAsync(tenantId, patId).ConfigureAwait(false);
            if (pat != null)
                continue;

            var n = outcomeIndex + 1;
            errors.Add(
                $"[violation=TIER_A_OUTCOME_PAT_NOT_FOUND] ruleSet={ruleSetDisplayName} {journeyNodePath} outcomeIndex={outcomeIndex} outcomeOrdinal={n} kind={outcome.Kind} field=AffectedPointAccountTypeIds[{patIndex}] patId={patId} — point account type not found for tenant. Use ListPointAccountTypes / PointAccountManifest; never invent ids.");
        }
    }

    private static string RuleSetDisplayName(RuleSet ruleSet)
    {
        if (!string.IsNullOrWhiteSpace(ruleSet.Name))
            return ruleSet.Name;
        if (!string.IsNullOrWhiteSpace(ruleSet.Id))
            return ruleSet.Id;
        return "unknown";
    }
}
