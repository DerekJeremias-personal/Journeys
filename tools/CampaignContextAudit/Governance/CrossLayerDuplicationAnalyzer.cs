namespace CampaignContextAudit.Governance;

public sealed record DuplicationClusterSummary(
    string ClusterId,
    string Theme,
    string Excerpt,
    int MemberCount,
    int LayerCount,
    int EstimatedRedundantChars,
    IReadOnlyList<string> Layers,
    IReadOnlyList<string> Sources,
    string CanonicalOwner,
    string RemediationAction);

public static class CrossLayerDuplicationAnalyzer
{
    public static IReadOnlyList<DuplicationClusterSummary> Analyze(IReadOnlyList<NormalizedRule> rules)
    {
        var jaccardClusters = DetectJaccardClusters(rules);
        var themeClusters = DetectThemeGroups(rules);
        return jaccardClusters
            .Concat(themeClusters.Where(t => !jaccardClusters.Any(j =>
                j.Theme.Equals(t.Theme, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(c => c.LayerCount)
            .ThenByDescending(c => c.MemberCount)
            .ThenByDescending(c => c.EstimatedRedundantChars)
            .ToList();
    }

    private static IReadOnlyList<DuplicationClusterSummary> DetectJaccardClusters(IReadOnlyList<NormalizedRule> rules) =>
        DuplicationDetector.DetectClusters(rules).Select(BuildSummary).ToList();

    private static IReadOnlyList<DuplicationClusterSummary> DetectThemeGroups(IReadOnlyList<NormalizedRule> rules)
    {
        var groups = rules
            .Select(r => (Theme: ClassifyTheme(r.Excerpt), Rule: r))
            .Where(x => !x.Theme.Equals("General coaching", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Theme, StringComparer.OrdinalIgnoreCase);

        var summaries = new List<DuplicationClusterSummary>();
        foreach (var group in groups)
        {
            var members = group.Select(x => x.Rule).ToList();
            var layerNames = members.Select(r => r.Layer.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
            if (layerNames.Count < 2)
                continue;

            var excerpt = members.OrderByDescending(r => r.Excerpt.Length).First().Excerpt;
            var sources = members.Select(r => $"{r.Layer}:{r.Source}").Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
            var redundantChars = Math.Max(0, members.Sum(r => r.Excerpt.Length) - members.Max(r => r.Excerpt.Length));

            summaries.Add(new DuplicationClusterSummary(
                $"theme-{RuleNormalizer.Hash(group.Key)[..8]}",
                group.Key,
                excerpt,
                members.Count,
                layerNames.Count,
                redundantChars,
                layerNames,
                sources,
                CanonicalOwner(group.Key),
                RemediationAction(group.Key)));
        }

        return summaries
            .OrderByDescending(c => c.EstimatedRedundantChars)
            .ToList();
    }

    public static IReadOnlyList<GovernanceFinding> ToFindings(IReadOnlyList<DuplicationClusterSummary> clusters) =>
        clusters.Select(c => new GovernanceFinding(
            "DUPLICATE_RULE",
            c.ClusterId.StartsWith("theme-", StringComparison.OrdinalIgnoreCase)
                ? ThemeSeverity(c.Theme)
                : c.LayerCount >= 3 || c.MemberCount >= 4
                    ? GovernanceFindingSeverity.Degrading
                    : GovernanceFindingSeverity.Wasteful,
            c.Sources,
            $"[{c.Theme}] Cluster {c.ClusterId}: {c.MemberCount} members across {c.LayerCount} layers (~{c.EstimatedRedundantChars} redundant chars).",
            InferSkill(c.Theme))).ToList();

    private static GovernanceFindingSeverity ThemeSeverity(string theme) =>
        theme switch
        {
            "Journey navigation" => GovernanceFindingSeverity.Degrading,
            "Journey validate/upsert" => GovernanceFindingSeverity.Degrading,
            _ => GovernanceFindingSeverity.Wasteful
        };

    private static DuplicationClusterSummary BuildSummary(DuplicationCluster cluster)
    {
        var excerpt = cluster.Rules
            .OrderByDescending(r => r.Excerpt.Length)
            .First()
            .Excerpt;

        var theme = ClassifyTheme(excerpt);
        var layers = cluster.Rules.Select(r => r.Layer.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        var sources = cluster.Rules.Select(r => $"{r.Layer}:{r.Source}").Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        var redundantChars = Math.Max(0, cluster.Rules.Sum(r => r.Excerpt.Length) - cluster.Rules.Max(r => r.Excerpt.Length));

        return new DuplicationClusterSummary(
            cluster.ClusterId,
            theme,
            excerpt,
            cluster.Rules.Count,
            layers.Count,
            redundantChars,
            layers,
            sources,
            CanonicalOwner(theme),
            RemediationAction(theme));
    }

    private static string ClassifyTheme(string excerpt)
    {
        var text = excerpt.ToLowerInvariant();
        if (text.Contains("upsert_point_account") || text.Contains("pointaccountmanifest") || text.Contains("pat manifest")
            || text.Contains("patrequired") || text.Contains("buildgate: pat") || text.Contains("http-defer"))
            return "PAT manifest gate";
        if (text.Contains("rulesetcount") || text.Contains("rules[]") || text.Contains("validate_campaign")
            || text.Contains("journeygate") || text.Contains("rulejsonelement"))
            return "Journey validate/upsert";
        if (text.Contains("navigation") || text.Contains("journey_nav_") || text.Contains("pointbalanceprovider")
            || text.Contains("appliedrulesetids"))
            return "Journey navigation";
        if (text.Contains("process_event") || text.Contains("draft test") || text.Contains("verification")
            || text.Contains("draftverification") || text.Contains("live promotion"))
            return "Draft verification";
        if (text.Contains("event model") || text.Contains("eventsgate") || text.Contains("events gate")
            || text.Contains("filtered from the tool surface") || text.Contains("mutators filtered"))
            return "Event models gate";
        if (text.Contains("skill manifest") || text.Contains("nextstep") || text.Contains("blockeduntil"))
            return "Skill manifest / next step";
        return "General coaching";
    }

    private static string CanonicalOwner(string theme) =>
        theme switch
        {
            "Skill manifest / next step" => "WorkflowSkillRegistry (SESSION manifest)",
            "PAT manifest gate" => "WorkflowSkillRegistry + SalientFacts (gate state only)",
            "Journey validate/upsert" => "Governance .txt + Remediation catalog (code-specific hints)",
            "Journey navigation" => "Remediation catalog + RulesEnginePatternGovernance supplement",
            "Draft verification" => "SalientFacts (state) + WorkflowPhaseVerificationGovernance.txt",
            "Event models gate" => "SalientFacts (gate state) + WorkflowPhaseEventModelsGovernance.txt",
            _ => "Single layer per theme — prefer governance or remediation, not both coach + salient facts"
        };

    private static string RemediationAction(string theme) =>
        theme switch
        {
            "Skill manifest / next step" =>
                "Remove next-step prose from coaches and SalientFacts; keep only in WorkflowSkillRegistry manifest lines.",
            "PAT manifest gate" =>
                "SalientFacts: one-line gate flag only. Coaches: pointer to SKILL MANIFEST. Remove duplicate sentences from remediation unless tied to a specific error code.",
            "Journey validate/upsert" =>
                "Keep validate/upsert discipline in governance .txt once. Remediation catalog owns code-specific fixes. Coaches emit hints only when artifact state requires action — no full rule paragraphs.",
            "Journey navigation" =>
                "Consolidate navigation remediation in BackendModelValidationRemediationCatalog / ElpToolRemediationCatalog; delete mirrored BuildNavigationRemediation from coach returns.",
            "Draft verification" =>
                "SalientFacts owns verification state lines. VerificationCoach returns action-only hints when blocked — do not repeat salient fact prose.",
            "Event models gate" =>
                "SalientFacts: eventsGate + expectedEventModelId only. EventModelsGateClosedCoach: single-line pointer when gate closed.",
            _ => "Pick one layer; delete or shorten duplicates in SalientFacts and coaches first."
        };

    private static string? InferSkill(string theme) =>
        theme switch
        {
            "Event models gate" => "EventModels",
            "PAT manifest gate" or "Journey validate/upsert" or "Journey navigation" or "Skill manifest / next step" =>
                "CampaignBuild",
            "Draft verification" => "Verification",
            _ => null
        };
}

public sealed record DuplicationCluster(string ClusterId, IReadOnlyList<NormalizedRule> Rules);
