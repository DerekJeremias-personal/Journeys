using System;
using System.Collections.Generic;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;

namespace Journeys.Core.Utility;

public sealed record JourneyWalkResult(
    int JourneyNodeCount,
    int RuleSetCount,
    Dictionary<string, int> OutcomeKindCounts,
    Dictionary<string, HashSet<string>> ReferencedPatIds);

public static class JourneyDigestWalkUtility
{
    public static JourneyWalkResult Walk(JourneyNode? root)
    {
        var nodes = 0;
        var ruleSets = 0;
        var kinds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var patIds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        void Visit(JourneyNode? n)
        {
            if (n == null)
                return;

            nodes++;
            if (n.Rules != null)
            {
                foreach (var rs in n.Rules)
                {
                    if (rs == null)
                        continue;
                    ruleSets++;
                    if (rs.Outcomes == null)
                        continue;
                    foreach (var o in rs.Outcomes)
                    {
                        if (string.IsNullOrEmpty(o?.Kind))
                            continue;
                        kinds[o.Kind] = kinds.GetValueOrDefault(o.Kind) + 1;
                        CollectReferencedPatIds(o, o.Kind, patIds);
                    }
                }
            }

            if (n.Children == null)
                return;
            foreach (var c in n.Children)
                Visit(c);
        }

        Visit(root);
        return new JourneyWalkResult(nodes, ruleSets, kinds, patIds);
    }

    private static void CollectReferencedPatIds(
        OutcomeBase outcome,
        string kind,
        Dictionary<string, HashSet<string>> referencedPatIds)
    {
        if (outcome is PointOutcomeBase pointOutcome && pointOutcome.AffectedPointAccountTypeIds != null)
        {
            foreach (var patId in pointOutcome.AffectedPointAccountTypeIds)
                AddPatReference(referencedPatIds, patId, kind);
        }

        if (!string.IsNullOrEmpty(outcome.PointAccountTypeId))
            AddPatReference(referencedPatIds, outcome.PointAccountTypeId, kind);
    }

    private static void AddPatReference(
        Dictionary<string, HashSet<string>> referencedPatIds,
        string? patId,
        string kind)
    {
        if (string.IsNullOrEmpty(patId))
            return;

        if (!referencedPatIds.TryGetValue(patId, out var kinds))
        {
            kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            referencedPatIds[patId] = kinds;
        }

        kinds.Add(kind);
    }
}
