namespace CampaignContextAudit.Governance;

public static class StaticGovernanceAnalyzer
{
    public static StaticGovernanceReport Analyze(StaticGovernanceOptions opts)
    {
        var corpus = GovernanceCorpusLoader.Load(opts.GovernanceDir, opts.DataWarehouseEnabled);
        var findings = new List<GovernanceFinding>();
        findings.AddRange(GovernanceCorpusLoader.InventoryFindings(corpus, opts.GovernanceDir));

        var txtRules = GovernanceCorpusLoader.ExtractTxtRules(corpus, opts.GovernanceDir);
        var codeRules = CodeGuidanceExtractor.Extract(opts.SourceRoot);
        var allRules = txtRules.Concat(codeRules).ToList();

        var duplicationClusters = CrossLayerDuplicationAnalyzer.Analyze(allRules);
        findings.AddRange(CrossLayerDuplicationAnalyzer.ToFindings(duplicationClusters));
        findings.AddRange(ContradictionDetector.DetectLayers(allRules, opts.GovernanceDir));
        findings.AddRange(DocGovernanceDriftChecker.Check(opts.SourceRoot));
        findings.AddRange(CoachOverlapFindings(allRules));

        var profiles = SkillCompetencyProfiler.Build(
            opts.GovernanceDir, allRules, findings, opts.DataWarehouseEnabled);

        var traceRun = false;
        if (!string.IsNullOrWhiteSpace(opts.CorrelateFindingsDir))
        {
            findings = TraceCorrelationLinker.ApplyBoosts(findings, opts.CorrelateFindingsDir, profiles).ToList();
            profiles = TraceCorrelationLinker.ApplyBandCaps(profiles, opts.CorrelateFindingsDir);
            traceRun = true;
        }

        var remediation = BuildRemediationTable(findings, duplicationClusters);
        var counts = corpus
            .GroupBy(c => c.Classification.ToString())
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return new StaticGovernanceReport(corpus, findings, profiles, remediation, counts, duplicationClusters, traceRun);
    }

    private static IEnumerable<GovernanceFinding> CoachOverlapFindings(IReadOnlyList<NormalizedRule> allRules)
    {
        var txt = string.Join('\n', allRules
            .Where(r => r.Layer is GuidanceLayer.Phase or GuidanceLayer.Supplement or GuidanceLayer.Core)
            .Select(r => RuleNormalizer.Normalize(r.Excerpt)));

        foreach (var coach in allRules.Where(r => r.Layer == GuidanceLayer.Coach))
        {
            var norm = RuleNormalizer.Normalize(coach.Excerpt);
            if (norm.Length < 30)
                continue;

            if (txt.Contains(norm, StringComparison.OrdinalIgnoreCase)
                || ContainmentRatio(txt, norm) >= 0.80)
            {
                yield return new GovernanceFinding(
                    "COACH_GOVERNANCE_OVERLAP",
                    GovernanceFindingSeverity.Wasteful,
                    [coach.Source],
                    "Coach guidance substantially overlaps loaded governance prose.",
                    CodeGuidanceExtractor.SkillForCoachSource(coach.Source));
            }
        }
    }

    private static double ContainmentRatio(string haystack, string needle)
    {
        var tokens = RuleNormalizer.Tokenize(needle);
        if (tokens.Count == 0)
            return 0;

        var hayTokens = RuleNormalizer.Tokenize(RuleNormalizer.Normalize(haystack));
        return (double)tokens.Count(t => hayTokens.Contains(t)) / tokens.Count;
    }

    private static IReadOnlyList<RemediationItem> BuildRemediationTable(
        IReadOnlyList<GovernanceFinding> findings,
        IReadOnlyList<DuplicationClusterSummary> clusters)
    {
        var items = new List<RemediationItem>();
        var rank = 1;

        foreach (var cluster in clusters.Take(8))
        {
            items.Add(new RemediationItem(
                rank++,
                $"[{cluster.Theme}] {cluster.RemediationAction}",
                ["DUPLICATE_RULE", cluster.ClusterId]));
        }

        var severityRank = new Dictionary<GovernanceFindingSeverity, int>
        {
            [GovernanceFindingSeverity.Blocking] = 0,
            [GovernanceFindingSeverity.Degrading] = 1,
            [GovernanceFindingSeverity.Wasteful] = 2,
            [GovernanceFindingSeverity.Informational] = 3
        };

        var skillRank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CampaignBuild"] = 0,
            ["EventModels"] = 1,
            ["Verification"] = 2,
            ["Brief"] = 3
        };

        var grouped = findings
            .Where(f => f.Code != "DUPLICATE_RULE")
            .OrderBy(f => severityRank[f.Severity])
            .ThenBy(f => skillRank.GetValueOrDefault(f.Skill ?? "", 99))
            .ThenBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
            .Select(f => new RemediationItem(0, RemediationAction(f), [f.Code]))
            .ToList();

        foreach (var item in grouped.Take(Math.Max(0, 15 - items.Count)))
            items.Add(item with { Rank = rank++ });

        return items;
    }

    private static string RemediationAction(GovernanceFinding finding) =>
        finding.Code switch
        {
            "ORPHAN_GOVERNANCE_FILE" => $"Remove or archive orphan file(s): {string.Join(", ", finding.Sources)}; migrate any still-valid rules to loaded files.",
            "DEPRECATED_FULL_CONTENT" => $"Truncate deprecated content in {string.Join(", ", finding.Sources)} to stub pointer only.",
            "DUPLICATE_RULE" => "Consolidate duplicate rule to single layer (prefer WorkflowSkillRegistry for tool surface; phase file for prose).",
            "CONTRADICTORY_GUIDANCE" or "MANIFEST_PHASE_CONFLICT" => "Align phase governance with SKILL MANIFEST authoritative lines.",
            "DOC_GOVERNANCE_DRIFT" => "Update ELP-Campaign-Agent-HTTP-API.md governance file list to match composer.",
            "COACH_GOVERNANCE_OVERLAP" => "Remove redundant coach string or drop overlapping governance bullet.",
            "LEGACY_STUB" => "Delete legacy stub after confirming no references.",
            _ => finding.Summary
        };
}
