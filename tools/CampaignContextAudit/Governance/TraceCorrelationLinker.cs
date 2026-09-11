using CampaignContextAudit.Reporting;

namespace CampaignContextAudit.Governance;

public static class TraceCorrelationLinker
{
    private static readonly Dictionary<string, HashSet<string>> BoostMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DUPLICATE_RULE"] = ["VALIDATION_UPSERT_LOOP", "PAT_SKIPPED_INLINE_MANIFEST"],
        ["CONTRADICTORY_GUIDANCE"] = ["VALIDATION_UPSERT_LOOP", "PAT_SKIPPED_INLINE_MANIFEST", "FALSE_TOOL_UNAVAILABLE"],
        ["MANIFEST_PHASE_CONFLICT"] = ["FALSE_TOOL_UNAVAILABLE"],
        ["ORPHAN_GOVERNANCE_FILE"] = ["VALIDATION_UPSERT_LOOP", "CREATION_ABORTED", "SESSION_STALLED_NO_DELIVERY"]
    };

    public static IReadOnlyList<GovernanceFinding> ApplyBoosts(
        IReadOnlyList<GovernanceFinding> findings,
        string traceFindingsDir,
        IReadOnlyList<SkillCompetencyProfile> profiles)
    {
        var traceCodes = LoadTraceFindingCodes(traceFindingsDir);
        if (traceCodes.Count == 0)
            return findings;

        return findings.Select(f =>
        {
            if (!BoostMap.TryGetValue(f.Code, out var triggers))
                return f;

            if (!traceCodes.Overlaps(triggers))
                return f;

            return f with
            {
                Severity = BoostSeverity(f.Severity),
                TraceBoosted = true
            };
        }).ToList();
    }

    public static IReadOnlyList<SkillCompetencyProfile> ApplyBandCaps(
        IReadOnlyList<SkillCompetencyProfile> profiles,
        string traceFindingsDir)
    {
        if (!HasBuildDeliveryFailure(traceFindingsDir))
            return profiles;

        return profiles.Select(p =>
            p.Skill == "CampaignBuild" && p.CompetencyBand is "A" or "B" or "C"
                ? p with { CompetencyBand = "D" }
                : p).ToList();
    }

    private static HashSet<string> LoadTraceFindingCodes(string dir)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dir))
            return codes;

        foreach (var path in Directory.EnumerateFiles(dir, "*-findings.json"))
        {
            if (path.Contains("governance-static-findings", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var report = FindingsJsonWriter.Read(path);
                foreach (var f in report.Findings)
                    codes.Add(f.Code);
            }
            catch (InvalidOperationException)
            {
                // skip malformed
            }
        }

        return codes;
    }

    private static bool HasBuildDeliveryFailure(string dir)
    {
        if (!Directory.Exists(dir))
            return false;

        foreach (var path in Directory.EnumerateFiles(dir, "*-findings.json"))
        {
            if (path.Contains("governance-static-findings", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var report = FindingsJsonWriter.Read(path);
                var delivery = report.Delivery?.Grade;
                var journeyCount = report.OutcomeSummary?.JourneyRuleSetCount ?? -1;
                if (delivery is "D" or "F" && journeyCount == 0)
                    return true;
            }
            catch (InvalidOperationException)
            {
                // skip
            }
        }

        return false;
    }

    private static GovernanceFindingSeverity BoostSeverity(GovernanceFindingSeverity severity) =>
        severity switch
        {
            GovernanceFindingSeverity.Informational => GovernanceFindingSeverity.Wasteful,
            GovernanceFindingSeverity.Wasteful => GovernanceFindingSeverity.Degrading,
            GovernanceFindingSeverity.Degrading => GovernanceFindingSeverity.Blocking,
            _ => GovernanceFindingSeverity.Blocking
        };
}
