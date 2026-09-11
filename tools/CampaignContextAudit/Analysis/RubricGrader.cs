using System.Text;
using System.Text.RegularExpressions;
using CampaignContextAudit.Reporting;

namespace CampaignContextAudit.Analysis;

public static partial class RubricGrader
{
    private const double BaselineScore = 3.0;
    private const double MaxFindingPenaltyPerDimension = 2.0;
    private const double MaxBonusPerDimension = 0.5;

    private static readonly (int Id, string Name, string Lens, bool Competency)[] DimensionMeta =
    [
        (1, "Relevance", "Competency", true),
        (2, "Salience & recency", "Competency", true),
        (3, "Grounding fidelity", "Competency", true),
        (4, "Self-knowledge / working memory", "Competency", true),
        (5, "Instruction & persona efficacy", "Competency", true),
        (6, "Tool-result hygiene", "Efficiency", false),
        (7, "Redundancy control", "Efficiency", false),
        (8, "History / budget strategy", "Efficiency", false)
    ];

    public static RubricScorecard Grade(
        IReadOnlyList<Finding> findings,
        WorkflowSnapshot? snapshot,
        IReadOnlyList<TurnBudget> turns,
        string? deliveryGrade = null)
    {
        var budget = BudgetStressAnalyzer.Analyze(turns);
        if (budget.UserTurnCount == 0)
            return BuildInsufficientDepthScorecard();

        var scores = DimensionMeta.ToDictionary(m => m.Id, _ => BaselineScore);
        var findingPenaltyApplied = DimensionMeta.ToDictionary(m => m.Id, _ => 0.0);
        var bonusApplied = DimensionMeta.ToDictionary(m => m.Id, _ => 0.0);
        var contributions = DimensionMeta.ToDictionary(m => m.Id, _ => new List<string>());

        ApplyFindingPenalties(findings, scores, findingPenaltyApplied, contributions);
        ApplyBudgetPenalties(budget, findings, scores, contributions);
        ApplyBonuses(findings, snapshot, scores, bonusApplied, contributions, deliveryGrade);

        var dimensions = DimensionMeta.Select(meta =>
        {
            var score = LetterGradeConverter.ClampScore(scores[meta.Id]);
            var rationale = BuildRationale(meta.Id, contributions[meta.Id], budget, findings);
            return new RubricDimensionScore(
                meta.Id,
                meta.Name,
                meta.Lens,
                score,
                LetterGradeConverter.ToLetter(score),
                rationale);
        }).ToList();

        var competencyScores = dimensions.Where(d => d.DimensionId <= 5).Select(d => d.Score).ToList();
        var efficiencyScores = dimensions.Where(d => d.DimensionId >= 6).Select(d => d.Score).ToList();

        var competencyAvg = competencyScores.Count > 0 ? competencyScores.Average() : BaselineScore;
        var efficiencyAvg = efficiencyScores.Count > 0 ? efficiencyScores.Average() : BaselineScore;

        var uncappedCompetency = new OverallGrade(competencyAvg, LetterGradeConverter.ToLetter(competencyAvg));
        var cappedCompetency = ApplyDeliveryCap(uncappedCompetency, deliveryGrade);

        return new RubricScorecard(
            dimensions,
            cappedCompetency,
            new OverallGrade(efficiencyAvg, LetterGradeConverter.ToLetter(efficiencyAvg)),
            uncappedCompetency.Grade != cappedCompetency.Grade ? uncappedCompetency : null);
    }

    private static OverallGrade ApplyDeliveryCap(OverallGrade competency, string? deliveryGrade)
    {
        var cap = deliveryGrade switch
        {
            "F" => 1.0,
            "D" => 2.0,
            _ => competency.Score
        };

        if (competency.Score <= cap) return competency;
        return new OverallGrade(cap, LetterGradeConverter.ToLetter(cap));
    }

    private static RubricScorecard BuildInsufficientDepthScorecard()
    {
        const string rationale = "Insufficient transcript depth for grading.";
        var dimensions = DimensionMeta.Select(meta => new RubricDimensionScore(
            meta.Id,
            meta.Name,
            meta.Lens,
            BaselineScore,
            "B",
            rationale)).ToList();

        return new RubricScorecard(
            dimensions,
            new OverallGrade(BaselineScore, "B"),
            new OverallGrade(BaselineScore, "B"));
    }

    private static void ApplyFindingPenalties(
        IReadOnlyList<Finding> findings,
        Dictionary<int, double> scores,
        Dictionary<int, double> findingPenaltyApplied,
        Dictionary<int, List<string>> contributions)
    {
        foreach (var finding in findings)
        {
            var basePenalty = FindingDimensionMap.SeverityPenalty(finding.Severity);
            if (basePenalty <= 0)
                continue;

            foreach (var mapping in FindingDimensionMap.ForCode(finding.Code))
            {
                var penalty = mapping.Role == DimensionRole.Primary ? basePenalty : basePenalty / 2.0;
                var remaining = MaxFindingPenaltyPerDimension - findingPenaltyApplied[mapping.DimensionId];
                if (remaining <= 0)
                    continue;

                var applied = Math.Min(penalty, remaining);
                findingPenaltyApplied[mapping.DimensionId] += applied;
                scores[mapping.DimensionId] -= applied;
                contributions[mapping.DimensionId].Add(FormatFindingContribution(finding));
            }
        }
    }

    private static void ApplyBudgetPenalties(
        BudgetStressSignals budget,
        IReadOnlyList<Finding> findings,
        Dictionary<int, double> scores,
        Dictionary<int, List<string>> contributions)
    {
        if (budget.AnyEviction)
        {
            ApplyBudgetPenalty(scores, contributions, 1, 0.50, $"History eviction: {budget.TotalDroppedChars:N0} chars across {budget.DroppedSegmentCount} segments.");
            ApplyBudgetPenalty(scores, contributions, 2, 0.50, $"History eviction: {budget.TotalDroppedChars:N0} chars across {budget.DroppedSegmentCount} segments.");
            ApplyBudgetPenalty(scores, contributions, 8, 0.50, $"History eviction: {budget.TotalDroppedChars:N0} chars across {budget.DroppedSegmentCount} segments.");
        }

        if (budget.HeavyEviction)
        {
            ApplyBudgetPenalty(scores, contributions, 1, 0.50, "Heavy eviction (>80k chars dropped in a segment).");
            ApplyBudgetPenalty(scores, contributions, 2, 0.50, "Heavy eviction (>80k chars dropped in a segment).");
            ApplyBudgetPenalty(scores, contributions, 8, 0.50, "Heavy eviction (>80k chars dropped in a segment).");
        }

        if (budget.NearBudget)
            ApplyBudgetPenalty(scores, contributions, 8, 0.35, $"Near 80k budget: max history {budget.MaxHistoryChars:N0} chars.");

        var bloatChars = SumBloatChars(findings);
        if (bloatChars >= 50_000)
            ApplyBudgetPenalty(scores, contributions, 6, 0.35, $"High bloat exposure: {bloatChars:N0} chars in top tool results.");
    }

    private static void ApplyBonuses(
        IReadOnlyList<Finding> findings,
        WorkflowSnapshot? snapshot,
        Dictionary<int, double> scores,
        Dictionary<int, double> bonusApplied,
        Dictionary<int, List<string>> contributions,
        string? deliveryGrade = null)
    {
        var hasBlocking = findings.Any(f =>
            string.Equals(f.Severity, "blocking", StringComparison.OrdinalIgnoreCase));
        var skipNoBlockingBonus = deliveryGrade is "F" or "D";

        if (!findings.Any(f => f.Code == "REDISCOVERY_AFTER_CREATION"))
            ApplyBonus(scores, bonusApplied, contributions, 4, 0.35, "No re-discovery after creation.");

        if (snapshot?.CreationComplete == true)
        {
            ApplyBonus(scores, bonusApplied, contributions, 1, 0.25, "creationComplete=true.");
            ApplyBonus(scores, bonusApplied, contributions, 5, 0.25, "creationComplete=true.");
        }

        if (!hasBlocking && !skipNoBlockingBonus)
        {
            ApplyBonus(scores, bonusApplied, contributions, 1, 0.25, "No blocking outcome findings.");
            ApplyBonus(scores, bonusApplied, contributions, 5, 0.25, "No blocking outcome findings.");
        }
    }

    private static void ApplyBudgetPenalty(
        Dictionary<int, double> scores,
        Dictionary<int, List<string>> contributions,
        int dimensionId,
        double penalty,
        string clause)
    {
        scores[dimensionId] -= penalty;
        contributions[dimensionId].Add(clause);
    }

    private static void ApplyBonus(
        Dictionary<int, double> scores,
        Dictionary<int, double> bonusApplied,
        Dictionary<int, List<string>> contributions,
        int dimensionId,
        double bonus,
        string clause)
    {
        var remaining = MaxBonusPerDimension - bonusApplied[dimensionId];
        if (remaining <= 0)
            return;

        var applied = Math.Min(bonus, remaining);
        bonusApplied[dimensionId] += applied;
        scores[dimensionId] += applied;
        contributions[dimensionId].Add(clause);
    }

    private static string FormatFindingContribution(Finding finding)
    {
        var seqs = finding.CitedSequences.Count > 0
            ? string.Join(", ", finding.CitedSequences)
            : "n/a";
        return $"{finding.Code} ({finding.Severity}, seq {seqs})";
    }

    private static string BuildRationale(
        int dimensionId,
        IReadOnlyList<string> contributions,
        BudgetStressSignals budget,
        IReadOnlyList<Finding> findings)
    {
        if (contributions.Count == 0)
            return $"No mapped findings; {budget.UserTurnCount} user turns, no history eviction.";

        var text = string.Join("; ", contributions.Distinct());
        if (text.Length <= 280)
            return text;

        return text[..277] + "…";
    }

    private static int SumBloatChars(IReadOnlyList<Finding> findings)
    {
        var total = 0;
        foreach (var finding in findings.Where(f => f.Code == "TOOL_RESULT_BLOAT"))
        {
            var match = BloatCharsRegex().Match(finding.Summary);
            if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ""), out var chars))
                total += chars;
        }

        return total;
    }

    [GeneratedRegex(@"is\s+([\d,]+)\s+chars", RegexOptions.IgnoreCase)]
    private static partial Regex BloatCharsRegex();
}
