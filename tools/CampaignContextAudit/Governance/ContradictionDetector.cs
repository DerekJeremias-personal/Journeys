using System.Text.Json;

namespace CampaignContextAudit.Governance;

public sealed record ContradictionPattern(string Id, string PatternA, string PatternB, string? Skill);

public static class ContradictionDetector
{
    public static IReadOnlyList<GovernanceFinding> DetectLayers(
        IReadOnlyList<NormalizedRule> rules,
        string governanceDir)
    {
        var findings = new List<GovernanceFinding>();
        var patterns = LoadPatterns();

        var layerText = rules
            .GroupBy(r => $"{r.Layer}:{r.Source}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.Join('\n', g.Select(x => x.Excerpt)), StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrEmpty(pattern.PatternA) || string.IsNullOrEmpty(pattern.PatternB))
                continue;

            var sourcesWithA = layerText.Where(kv =>
                kv.Value.Contains(pattern.PatternA, StringComparison.OrdinalIgnoreCase)).ToList();
            var sourcesWithB = layerText.Where(kv =>
                kv.Value.Contains(pattern.PatternB, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var a in sourcesWithA)
                {
                    foreach (var b in sourcesWithB)
                    {
                        if (a.Key.Equals(b.Key, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (pattern.Id == "children-vs-nodes"
                            && (ContainsNotNodesShape(a.Value) || ContainsNotNodesShape(b.Value)))
                            continue;

                        findings.Add(new GovernanceFinding(
                        "CONTRADICTORY_GUIDANCE",
                        GovernanceFindingSeverity.Degrading,
                        [a.Key, b.Key],
                        $"Pattern '{pattern.Id}': '{pattern.PatternA}' vs '{pattern.PatternB}'.",
                        pattern.Skill));
                }
            }
        }

        findings.AddRange(DetectManifestConflicts(rules, governanceDir));
        return findings;
    }

    public static IReadOnlyList<GovernanceFinding> Detect(IReadOnlyDictionary<string, string> corpusText)
    {
        var findings = new List<GovernanceFinding>();
        foreach (var pattern in LoadPatterns())
        {
            if (string.IsNullOrEmpty(pattern.PatternA) || string.IsNullOrEmpty(pattern.PatternB))
                continue;

            var aSources = corpusText.Where(kv =>
                kv.Value.Contains(pattern.PatternA, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList();
            var bSources = corpusText.Where(kv =>
                kv.Value.Contains(pattern.PatternB, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList();

            if (aSources.Count == 0 || bSources.Count == 0)
                continue;

            foreach (var a in aSources)
            {
                foreach (var b in bSources)
                {
                    if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (pattern.Id == "children-vs-nodes"
                        && (ContainsNotNodesShape(corpusText[a]) || ContainsNotNodesShape(corpusText[b])))
                        continue;

                    findings.Add(new GovernanceFinding(
                        "CONTRADICTORY_GUIDANCE",
                        GovernanceFindingSeverity.Degrading,
                        [a, b],
                        $"Pattern '{pattern.Id}'.",
                        pattern.Skill));
                }
            }
        }

        return findings;
    }

    private static IEnumerable<GovernanceFinding> DetectManifestConflicts(
        IReadOnlyList<NormalizedRule> rules,
        string governanceDir)
    {
        var phaseText = string.Join('\n', rules
            .Where(r => r.Layer is GuidanceLayer.Phase or GuidanceLayer.Supplement)
            .Select(r => r.Excerpt));
        var registryText = string.Join('\n', rules
            .Where(r => r.Layer == GuidanceLayer.Registry)
            .Select(r => r.Excerpt));

        if (phaseText.Contains("mutating tools available", StringComparison.OrdinalIgnoreCase)
            && (registryText.Contains("blockedUntilPatManifest", StringComparison.OrdinalIgnoreCase)
                || registryText.Contains("blockedUntilJourneyPersisted", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new GovernanceFinding(
                "MANIFEST_PHASE_CONFLICT",
                GovernanceFindingSeverity.Degrading,
                ["phase-governance", "WorkflowSkillRegistry.cs"],
                "Phase prose claims mutating tools available while registry blocks journey/PAT tools.",
                "CampaignBuild");
        }

        var buildPath = Path.Combine(governanceDir, GovernanceLoadSet.PhaseFileForSkill("CampaignBuild", dataWarehouseEnabled: true));
        if (File.Exists(buildPath))
        {
            var buildText = File.ReadAllText(buildPath);
            if (buildText.Contains("CampaignJourney", StringComparison.OrdinalIgnoreCase)
                && registryText.Contains("activeSkill: CampaignBuild", StringComparison.OrdinalIgnoreCase))
            {
                yield return new GovernanceFinding(
                    "MANIFEST_PHASE_CONFLICT",
                    GovernanceFindingSeverity.Degrading,
                    [Path.GetFileName(buildPath), "WorkflowSkillRegistry.cs"],
                    "Phase file references CampaignJourney as distinct phase; registry uses CampaignBuild skill.",
                    "CampaignBuild");
            }
        }
    }

    private static IReadOnlyList<ContradictionPattern> LoadPatterns()
    {
        var asm = typeof(ContradictionDetector).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("ContradictionPatterns.json", StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return DefaultPatterns();

        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return JsonSerializer.Deserialize<List<ContradictionPattern>>(reader.ReadToEnd(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? DefaultPatterns();
    }

    private static IReadOnlyList<ContradictionPattern> DefaultPatterns() =>
    [
        new("tools-after-brief-vs-blocked", "mutating tools available after brief", "blockedUntilPatManifest", "CampaignBuild"),
        new("pat-http-vs-registry", "paste GUID", "nextStep: upsert_point_account_type", "CampaignBuild"),
        new("children-vs-nodes", "children[]", "nodes[]", "CampaignBuild"),
        new("journey-phase-vs-build-skill", "CampaignJourney", "activeSkill: CampaignBuild", "CampaignBuild")
    ];

    private static bool ContainsNotNodesShape(string text) =>
        text.Contains("not nodes[]", StringComparison.OrdinalIgnoreCase);

    private static bool IsNodesNegationContext(string textA, string textB)
    {
        foreach (var text in new[] { textA, textB })
        {
            var idx = text.IndexOf("nodes[]", StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                var windowStart = Math.Max(0, idx - 40);
                var window = text[windowStart..idx];
                if (window.Contains("do not", StringComparison.OrdinalIgnoreCase)
                    || window.Contains("not use", StringComparison.OrdinalIgnoreCase)
                    || window.Contains("legacy", StringComparison.OrdinalIgnoreCase)
                    || window.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                    return true;

                idx = text.IndexOf("nodes[]", idx + 1, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
