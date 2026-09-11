using System.Text.RegularExpressions;

namespace CampaignContextAudit.Governance;

public static partial class CodeGuidanceExtractor
{
    private static readonly Dictionary<string, string> CoachSkillMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NavigationCoach"] = "Brief",
        ["EventModelsGateClosedCoach"] = "EventModels",
        ["EventModelResolutionCoach"] = "EventModels",
        ["EventModelWorkflowDriftCoach"] = "EventModels",
        ["CampaignBuildCoach"] = "CampaignBuild",
        ["CampaignValidationCoach"] = "CampaignBuild",
        ["JourneyCoach"] = "CampaignBuild",
        ["PatMutatorDeferralCoach"] = "CampaignBuild",
        ["VerificationCoach"] = "Verification"
    };

    public static IReadOnlyList<NormalizedRule> Extract(string sourceRoot)
    {
        var rules = new List<NormalizedRule>();
        if (!Directory.Exists(sourceRoot))
            return rules;

        ExtractFromFile(
            rules,
            Path.Combine(sourceRoot, "Journeys.Core", "Workflow", "WorkflowSkillRegistry.cs"),
            GuidanceLayer.Registry,
            "WorkflowSkillRegistry.cs");

        ExtractSalientFacts(
            rules,
            Path.Combine(sourceRoot, "Journeys.Core", "Utility", "SalientFactsPromptBuilder.cs"));

        var coachWorkflowDir = Path.Combine(sourceRoot, "Journeys.API", "CampaignAgent", "Workflow");
        if (Directory.Exists(coachWorkflowDir))
        {
            foreach (var path in Directory.EnumerateFiles(coachWorkflowDir, "*Coach.cs", SearchOption.TopDirectoryOnly))
                ExtractCoachFile(rules, path);
        }

        var coreUtilityDir = Path.Combine(sourceRoot, "Journeys.Core", "Utility");
        if (Directory.Exists(coreUtilityDir))
        {
            foreach (var path in Directory.EnumerateFiles(coreUtilityDir, "*Coach.cs", SearchOption.TopDirectoryOnly))
                ExtractCoachFile(rules, path);
        }

        var remediationDir = Path.Combine(sourceRoot, "Journeys.CampaignAgent.Remediation");
        if (Directory.Exists(remediationDir))
        {
            foreach (var path in Directory.EnumerateFiles(remediationDir, "*Catalog.cs"))
                ExtractRemediationFile(rules, path);
        }

        return rules;
    }

    private static void ExtractSalientFacts(List<NormalizedRule> rules, string path)
    {
        if (!File.Exists(path))
            return;

        var content = File.ReadAllText(path);
        foreach (Match match in SalientFactsLinePattern().Matches(content))
        {
            var line = Regex.Unescape(match.Groups[1].Value).Trim();
            if (!IsUserFacingGuidance(line, GuidanceLayer.SalientFacts))
                continue;

            rules.Add(new NormalizedRule(
                RuleNormalizer.Hash(RuleNormalizer.Normalize(line)),
                RuleNormalizer.Excerpt(line),
                GuidanceLayer.SalientFacts,
                "SalientFactsPromptBuilder.cs"));
        }
    }

    private static void ExtractCoachFile(List<NormalizedRule> rules, string path)
    {
        var fileName = Path.GetFileName(path);
        ExtractGuidanceLiterals(rules, File.ReadAllText(path), GuidanceLayer.Coach, fileName);
    }

    private static void ExtractFromFile(
        List<NormalizedRule> rules,
        string path,
        GuidanceLayer layer,
        string sourceLabel)
    {
        if (!File.Exists(path))
            return;

        ExtractGuidanceLiterals(rules, File.ReadAllText(path), layer, sourceLabel);
    }

    private static void ExtractRemediationFile(List<NormalizedRule> rules, string path)
    {
        if (!File.Exists(path))
            return;

        ExtractGuidanceLiterals(rules, File.ReadAllText(path), GuidanceLayer.Remediation, Path.GetFileName(path));
    }

    private static void ExtractGuidanceLiterals(
        List<NormalizedRule> rules,
        string content,
        GuidanceLayer layer,
        string sourceLabel)
    {
        foreach (Match match in StringLiteralPattern().Matches(content))
        {
            var literal = Regex.Unescape(match.Groups[1].Value);
            if (!IsUserFacingGuidance(literal, layer))
                continue;

            rules.Add(new NormalizedRule(
                RuleNormalizer.Hash(RuleNormalizer.Normalize(literal)),
                RuleNormalizer.Excerpt(literal),
                layer,
                sourceLabel));
        }
    }

    private static bool IsUserFacingGuidance(string text, GuidanceLayer layer)
    {
        if (text.Length < 25 || text.Length > 2000)
            return false;

        if (text.Contains('{') || text.Contains('}') || text.Contains(';'))
            return false;

        if (text.Contains("TryGetProperty", StringComparison.Ordinal)
            || text.Contains("ValueKind", StringComparison.Ordinal)
            || text.Contains("JsonSerializer", StringComparison.Ordinal)
            || text.Contains("StringComparison", StringComparison.Ordinal)
            || text.Contains("RegexOptions", StringComparison.Ordinal)
            || text.Contains("doc.RootElement", StringComparison.Ordinal)
            || text.Contains("catch (", StringComparison.Ordinal)
            || text.Contains("var ", StringComparison.Ordinal)
            || text.Contains("root.", StringComparison.Ordinal))
            return false;

        return layer switch
        {
            GuidanceLayer.Registry =>
                text.Contains("activeSkill", StringComparison.OrdinalIgnoreCase)
                || text.Contains("nextStep:", StringComparison.OrdinalIgnoreCase)
                || text.Contains("blockedUntil", StringComparison.OrdinalIgnoreCase)
                || text.Contains("SKILL MANIFEST", StringComparison.OrdinalIgnoreCase)
                || text.Contains("toolsThisTurn", StringComparison.OrdinalIgnoreCase),
            GuidanceLayer.SalientFacts =>
                text.Contains(':') && !text.StartsWith("http", StringComparison.OrdinalIgnoreCase),
            GuidanceLayer.Coach =>
                text.StartsWith("Coach:", StringComparison.OrdinalIgnoreCase)
                || text.Contains(" validate", StringComparison.OrdinalIgnoreCase)
                || text.Contains(" upsert", StringComparison.OrdinalIgnoreCase)
                || text.Contains("manifest", StringComparison.OrdinalIgnoreCase)
                || text.Contains("ruleSetCount", StringComparison.OrdinalIgnoreCase),
            GuidanceLayer.Remediation =>
                text.Contains("re-validate", StringComparison.OrdinalIgnoreCase)
                || text.Contains("upsert", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Add ", StringComparison.Ordinal)
                || text.StartsWith("Fix ", StringComparison.Ordinal)
                || text.StartsWith("Replace ", StringComparison.Ordinal)
                || text.Contains("navigation", StringComparison.OrdinalIgnoreCase)
                || text.Contains("affectedPointAccountTypeIds", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public static string? SkillForCoachSource(string source) =>
        CoachSkillMap.FirstOrDefault(kvp =>
            source.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)).Value;

    [GeneratedRegex(@"""((?:\\.|[^""\\]){20,})""")]
    private static partial Regex StringLiteralPattern();

    [GeneratedRegex(@"lines\.Add\(\$?""((?:\\.|[^""\\]){10,})""\)")]
    private static partial Regex SalientFactsLinePattern();
}
