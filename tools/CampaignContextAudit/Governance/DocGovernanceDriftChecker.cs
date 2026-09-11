using System.Text.RegularExpressions;

namespace CampaignContextAudit.Governance;

public static partial class DocGovernanceDriftChecker
{
    public static IReadOnlyList<GovernanceFinding> Check(string sourceRoot)
    {
        var docPath = Path.Combine(sourceRoot, "Journeys", "docs", "ELP-Campaign-Agent-HTTP-API.md");
        if (!File.Exists(docPath))
            return [];

        var text = File.ReadAllText(docPath);
        var findings = new List<GovernanceFinding>();

        foreach (Match match in GovernanceFilePattern().Matches(text))
        {
            var fileName = match.Groups[1].Value;
            if (GovernanceLoadSet.IsLoadedOrSupplement(fileName))
                continue;

            var lineStart = text.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
            var lineEnd = text.IndexOf('\n', match.Index);
            if (lineEnd < 0) lineEnd = text.Length;
            var line = text[lineStart..lineEnd];
            if (line.Contains("deprecated", StringComparison.OrdinalIgnoreCase)
                || line.Contains("stub", StringComparison.OrdinalIgnoreCase)
                || line.Contains("no longer loads", StringComparison.OrdinalIgnoreCase))
                continue;

            findings.Add(new GovernanceFinding(
                "DOC_GOVERNANCE_DRIFT",
                GovernanceFindingSeverity.Degrading,
                [Path.GetFileName(docPath), fileName],
                $"API doc references governance file not in composer load set: {fileName}."));
        }

        return findings;
    }

    [GeneratedRegex(@"([A-Za-z0-9]+Governance\.txt|WorkflowPhase[A-Za-z]+Governance\.txt)")]
    private static partial Regex GovernanceFilePattern();
}
