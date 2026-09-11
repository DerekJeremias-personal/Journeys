namespace CampaignContextAudit.Governance;

public static class GovernanceCorpusLoader
{
    private const int LegacyStubMaxLines = 3;
    private const int DeprecatedBodyThreshold = 200;

    public static IReadOnlyList<GovernanceFileEntry> Load(string governanceDir, bool dataWarehouseEnabled)
    {
        if (!Directory.Exists(governanceDir))
            throw new DirectoryNotFoundException($"Governance dir not found: {governanceDir}");

        var entries = new List<GovernanceFileEntry>();
        foreach (var path in Directory.EnumerateFiles(governanceDir, "*.txt"))
        {
            var fileName = Path.GetFileName(path);
            var text = File.ReadAllText(path);
            var lines = text.Split('\n');
            var nonEmpty = lines.Count(l => !string.IsNullOrWhiteSpace(l));
            var classification = Classify(fileName, text, nonEmpty);
            entries.Add(new GovernanceFileEntry(
                fileName,
                classification,
                lines.Length,
                text.Trim().Length,
                GovernanceLoadSet.SkillsForFile(fileName, dataWarehouseEnabled)));
        }

        return entries.OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<GovernanceFinding> InventoryFindings(
        IReadOnlyList<GovernanceFileEntry> corpus,
        string governanceDir)
    {
        var findings = new List<GovernanceFinding>();

        foreach (var entry in corpus)
        {
            switch (entry.Classification)
            {
                case GovernanceFileClassification.Orphan:
                    findings.Add(new GovernanceFinding(
                        "ORPHAN_GOVERNANCE_FILE",
                        GovernanceFindingSeverity.Degrading,
                        [entry.FileName],
                        "Governance file is not loaded by CampaignAgentPromptComposer."));
                    break;
                case GovernanceFileClassification.LegacyStub:
                    findings.Add(new GovernanceFinding(
                        "LEGACY_STUB",
                        GovernanceFindingSeverity.Informational,
                        [entry.FileName],
                        "Legacy stub pointer file; safe to remove after confirming no doc references."));
                    break;
            }

            var path = Path.Combine(governanceDir, entry.FileName);
            if (!File.Exists(path))
                continue;

            var text = File.ReadAllText(path);
            if (IsDeprecatedHeader(text)
                && entry.Classification == GovernanceFileClassification.Orphan
                && BodyAfterHeader(text).Length > DeprecatedBodyThreshold)
            {
                findings.Add(new GovernanceFinding(
                    "DEPRECATED_FULL_CONTENT",
                    GovernanceFindingSeverity.Degrading,
                    [entry.FileName],
                    "Deprecated file still contains substantial governance prose (>200 chars).",
                    Skill: InferSkill(entry)));
            }
        }

        return findings;
    }

    public static IReadOnlyList<NormalizedRule> ExtractTxtRules(
        IReadOnlyList<GovernanceFileEntry> corpus,
        string governanceDir)
    {
        var rules = new List<NormalizedRule>();
        foreach (var entry in corpus)
        {
            if (entry.Classification is GovernanceFileClassification.Orphan or GovernanceFileClassification.LegacyStub)
                continue;

            var layer = LayerFor(entry);
            var path = Path.Combine(governanceDir, entry.FileName);
            if (!File.Exists(path))
                continue;

            rules.AddRange(RuleNormalizer.ExtractRules(File.ReadAllText(path), layer, entry.FileName));
        }

        return rules;
    }

    private static GovernanceFileClassification Classify(string fileName, string text, int nonEmptyLines)
    {
        if (GovernanceLoadSet.LoadedEveryTurn.Contains(fileName))
            return GovernanceFileClassification.LoadedEveryTurn;

        if (GovernanceLoadSet.IsLoadedOrSupplement(fileName))
        {
            if (IsDeprecatedHeader(text))
                return GovernanceFileClassification.Orphan;

            if (GovernanceLoadSet.SupplementsForSkill("CampaignBuild").Contains(fileName, StringComparer.OrdinalIgnoreCase)
                || GovernanceLoadSet.SupplementsForSkill("Verification").Contains(fileName, StringComparer.OrdinalIgnoreCase))
                return GovernanceFileClassification.Supplement;

            return GovernanceFileClassification.Phase;
        }

        return nonEmptyLines <= LegacyStubMaxLines
            ? GovernanceFileClassification.LegacyStub
            : GovernanceFileClassification.Orphan;
    }

    private static GuidanceLayer LayerFor(GovernanceFileEntry entry) =>
        entry.Classification switch
        {
            GovernanceFileClassification.LoadedEveryTurn when entry.FileName.Equals(
                "SystemPrompt.txt", StringComparison.OrdinalIgnoreCase) => GuidanceLayer.Persona,
            GovernanceFileClassification.LoadedEveryTurn when entry.FileName.Equals(
                "SharedAgentToolingGovernance.txt", StringComparison.OrdinalIgnoreCase) => GuidanceLayer.SharedTooling,
            GovernanceFileClassification.LoadedEveryTurn when entry.FileName.Equals(
                "CampaignGovernanceCore.txt", StringComparison.OrdinalIgnoreCase) => GuidanceLayer.Core,
            GovernanceFileClassification.LoadedEveryTurn => GuidanceLayer.CoachChecklist,
            GovernanceFileClassification.Supplement => GuidanceLayer.Supplement,
            _ => GuidanceLayer.Phase
        };

    private static bool IsDeprecatedHeader(string text)
    {
        var header = string.Join('\n', text.Split('\n').Take(3));
        return header.Contains("DEPRECATED —", StringComparison.OrdinalIgnoreCase)
               || header.TrimStart().StartsWith("DEPRECATED", StringComparison.OrdinalIgnoreCase);
    }

    private static string BodyAfterHeader(string text)
    {
        var lines = text.Split('\n');
        var start = 0;
        for (; start < lines.Length; start++)
        {
            if (string.IsNullOrWhiteSpace(lines[start]))
                break;
            if (!lines[start].Contains("DEPRECATED", StringComparison.OrdinalIgnoreCase)
                && !lines[start].StartsWith('#'))
                break;
        }

        return string.Join('\n', lines.Skip(start)).Trim();
    }

    private static string? InferSkill(GovernanceFileEntry entry) =>
        entry.LoadedForSkills.FirstOrDefault() ?? "CampaignBuild";
}
