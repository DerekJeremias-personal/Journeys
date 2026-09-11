namespace CampaignContextAudit.Governance;

public static class DuplicationDetector
{
    private const double JaccardThreshold = 0.85;

    public static IReadOnlyList<GovernanceFinding> Detect(IReadOnlyList<NormalizedRule> rules) =>
        CrossLayerDuplicationAnalyzer.ToFindings(CrossLayerDuplicationAnalyzer.Analyze(rules));

    public static IReadOnlyList<DuplicationCluster> DetectClusters(IReadOnlyList<NormalizedRule> rules)
    {
        var clustersByHash = new Dictionary<string, List<NormalizedRule>>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<(int, int)>();

        for (var i = 0; i < rules.Count; i++)
        {
            for (var j = i + 1; j < rules.Count; j++)
            {
                if (visited.Contains((i, j)))
                    continue;

                var a = rules[i];
                var b = rules[j];
                if (a.Source.Equals(b.Source, StringComparison.OrdinalIgnoreCase) && a.Layer == b.Layer)
                    continue;

                var tokensA = RuleNormalizer.Tokenize(RuleNormalizer.Normalize(a.Excerpt));
                var tokensB = RuleNormalizer.Tokenize(RuleNormalizer.Normalize(b.Excerpt));
                if (RuleNormalizer.Jaccard(tokensA, tokensB) < JaccardThreshold)
                    continue;

                var cluster = ClusterRules(rules, i, j, visited);
                if (cluster.Count < 2)
                    continue;

                var layers = cluster.Select(r => r.Layer).Distinct().Count();
                if (layers < 2)
                    continue;

                var hash = cluster[0].Hash;
                if (!clustersByHash.TryGetValue(hash, out var existing))
                {
                    existing = [];
                    clustersByHash[hash] = existing;
                }

                foreach (var rule in cluster)
                {
                    if (!existing.Any(r => r.Hash == rule.Hash && r.Source == rule.Source && r.Layer == rule.Layer))
                        existing.Add(rule);
                }
            }
        }

        return clustersByHash
            .Select(kv => new DuplicationCluster(kv.Key, kv.Value))
            .ToList();
    }

    private static List<NormalizedRule> ClusterRules(
        IReadOnlyList<NormalizedRule> rules,
        int seedA,
        int seedB,
        HashSet<(int, int)> visited)
    {
        var cluster = new List<NormalizedRule> { rules[seedA], rules[seedB] };
        visited.Add((seedA, seedB));

        for (var i = 0; i < rules.Count; i++)
        {
            foreach (var member in cluster.ToList())
            {
                var idx = rules.ToList().IndexOf(member);
                if (idx < 0 || i == idx)
                    continue;

                var pair = idx < i ? (idx, i) : (i, idx);
                if (visited.Contains(pair))
                    continue;

                var tokensA = RuleNormalizer.Tokenize(RuleNormalizer.Normalize(rules[idx].Excerpt));
                var tokensB = RuleNormalizer.Tokenize(RuleNormalizer.Normalize(rules[i].Excerpt));
                if (RuleNormalizer.Jaccard(tokensA, tokensB) >= JaccardThreshold)
                {
                    visited.Add(pair);
                    if (!cluster.Contains(rules[i]))
                        cluster.Add(rules[i]);
                }
            }
        }

        return cluster;
    }
}
