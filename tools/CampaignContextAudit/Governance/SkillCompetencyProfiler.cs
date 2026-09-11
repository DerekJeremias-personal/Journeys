using CampaignContextAudit.Budget;

namespace CampaignContextAudit.Governance;

public static class SkillCompetencyProfiler
{
    public static IReadOnlyList<SkillCompetencyProfile> Build(
        string governanceDir,
        IReadOnlyList<NormalizedRule> allRules,
        IReadOnlyList<GovernanceFinding> findings,
        bool dataWarehouseEnabled)
    {
        var sizer = new SegmentSizer(governanceDir, dataWarehouseEnabled);
        var profiles = GovernanceLoadSet.AllKnownSkills()
            .Select(skill => BuildSkillProfile(skill, governanceDir, sizer, allRules, findings, dataWarehouseEnabled))
            .ToList();

        var referenceStable = Average(profiles.Where(p => p.Skill is "EventModels" or "Verification").Select(p => (double)p.StableChars));
        var referenceRedundancy = Average(profiles.Where(p => p.Skill is "EventModels" or "Verification").Select(p => p.RedundancyIndex));
        var referenceSpecificity = Average(profiles.Where(p => p.Skill is "EventModels" or "Verification").Select(p => p.SpecificityScore));

        return profiles.Select(p => p with
        {
            CompetencyBand = GradeBand(p, referenceStable, referenceRedundancy, referenceSpecificity, findings)
        }).ToList();
    }

    public static IReadOnlyList<SkillCompetencyProfile> BuildSynthetic(
        IReadOnlyList<NormalizedRule> rules,
        IReadOnlyList<GovernanceFinding> findings)
    {
        var skillRules = rules.Where(r => r.Layer != GuidanceLayer.Doc).ToList();
        var total = Math.Max(1, skillRules.Count);
        var duplicate = CountDuplicateLines(skillRules);
        var imperative = skillRules.Count(r => RuleNormalizer.IsImperative(r.Excerpt));
        var coachOnly = skillRules.Count(r => r.Layer == GuidanceLayer.Coach);

        var profile = new SkillCompetencyProfile(
            "CampaignBuild",
            StableChars: 5000,
            LayerCount: skillRules.Select(r => r.Layer).Distinct().Count(),
            RedundancyIndex: (double)duplicate / total,
            SpecificityScore: (double)imperative / total,
            CoachDependency: (double)coachOnly / total,
            CompetencyBand: "C");

        return [profile with { CompetencyBand = GradeBand(profile, 4000, 0.1, 0.3, findings) }];
    }

    private static SkillCompetencyProfile BuildSkillProfile(
        string skill,
        string governanceDir,
        SegmentSizer sizer,
        IReadOnlyList<NormalizedRule> allRules,
        IReadOnlyList<GovernanceFinding> findings,
        bool dataWarehouseEnabled)
    {
        var phase = skill switch
        {
            "Brief" => "DataAnalysis",
            "EventModels" => "EventModels",
            "CampaignBuild" => "CampaignBuild",
            "Verification" => "Verification",
            _ => "DataAnalysis"
        };

        var stable = sizer.StableSizesForPhase(phase).TotalStableChars;
        foreach (var supplement in GovernanceLoadSet.SupplementsForSkill(skill))
        {
            var path = Path.Combine(governanceDir, supplement);
            if (File.Exists(path))
                stable += File.ReadAllText(path).Trim().Length;
        }

        var skillRules = RulesForSkill(skill, allRules).ToList();
        var total = Math.Max(1, skillRules.Count);
        var duplicate = CountDuplicateLines(skillRules);
        var imperative = skillRules.Count(r => RuleNormalizer.IsImperative(r.Excerpt));
        var coachRules = skillRules.Where(r => r.Layer == GuidanceLayer.Coach).ToList();
        var coachOnly = coachRules.Count(r => !IsMatchedInTxt(r, skillRules));

        return new SkillCompetencyProfile(
            skill,
            stable,
            skillRules.Select(r => r.Layer).Distinct().Count(),
            (double)duplicate / total,
            (double)imperative / total,
            coachRules.Count == 0 ? 0 : (double)coachOnly / coachRules.Count,
            "C");
    }

    private static IEnumerable<NormalizedRule> RulesForSkill(string skill, IReadOnlyList<NormalizedRule> allRules)
    {
        foreach (var rule in allRules)
        {
            if (rule.Layer is GuidanceLayer.Persona or GuidanceLayer.SharedTooling
                or GuidanceLayer.Core or GuidanceLayer.CoachChecklist)
            {
                yield return rule;
                continue;
            }

            if (rule.Layer == GuidanceLayer.Coach
                && CodeGuidanceExtractor.SkillForCoachSource(rule.Source) == skill)
            {
                yield return rule;
                continue;
            }

            if (rule.Layer == GuidanceLayer.Registry && skill is "CampaignBuild" or "EventModels" or "Verification")
            {
                yield return rule;
                continue;
            }

            if (rule.Source.Contains(GovernanceLoadSet.PhaseFileForSkill(skill, dataWarehouseEnabled: true),
                    StringComparison.OrdinalIgnoreCase)
                || GovernanceLoadSet.SupplementsForSkill(skill).Contains(rule.Source, StringComparer.OrdinalIgnoreCase))
                yield return rule;
        }
    }

    private static bool IsMatchedInTxt(NormalizedRule coachRule, IReadOnlyList<NormalizedRule> skillRules)
    {
        var coachTokens = RuleNormalizer.Tokenize(RuleNormalizer.Normalize(coachRule.Excerpt));
        return skillRules.Where(r => r.Layer is GuidanceLayer.Phase or GuidanceLayer.Supplement or GuidanceLayer.Core)
            .Any(txt => RuleNormalizer.Jaccard(coachTokens, RuleNormalizer.Tokenize(RuleNormalizer.Normalize(txt.Excerpt))) >= 0.85);
    }

    private static int CountDuplicateLines(IReadOnlyList<NormalizedRule> rules)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dup = 0;
        foreach (var rule in rules)
        {
            var norm = RuleNormalizer.Normalize(rule.Excerpt);
            if (!seen.Add(norm))
                dup++;
        }

        return dup;
    }

    private static string GradeBand(
        SkillCompetencyProfile profile,
        double referenceStable,
        double referenceRedundancy,
        double referenceSpecificity,
        IReadOnlyList<GovernanceFinding> findings)
    {
        if (findings.Any(f => f.Skill == profile.Skill
                              && f.Code is "CONTRADICTORY_GUIDANCE" or "MANIFEST_PHASE_CONFLICT"))
            return "F";

        if (profile.RedundancyIndex > referenceRedundancy + 0.15
            || profile.SpecificityScore < referenceSpecificity - 0.10)
            return "C";

        if (profile.StableChars < referenceStable * 0.5 && profile.CoachDependency > 0.40)
            return "D";

        var stableOk = profile.Skill == "Brief"
                       || Math.Abs(profile.StableChars - referenceStable) <= referenceStable * 0.20;

        if (profile.RedundancyIndex <= referenceRedundancy + 0.05
            && profile.SpecificityScore >= referenceSpecificity
            && stableOk)
            return "A";

        return "B";
    }

    private static double Average(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0 : list.Average();
    }
}
