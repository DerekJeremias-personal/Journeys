using System.Text;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

public static class DeliveryGrader
{
    public static DeliveryScorecard Grade(
        IReadOnlyList<Finding> findings,
        WorkflowSnapshot? snapshot,
        CreationMilestoneEvaluation milestones,
        long effectiveWallMs,
        IReadOnlyList<AgentMessageDoc> chatRows,
        int abortMs = 300_000)
    {
        var grade = ComputeGrade(findings, snapshot, milestones, effectiveWallMs, chatRows, abortMs);
        var score = GradeToScore(grade);
        var rationale = BuildRationale(findings, milestones, effectiveWallMs, grade);
        return new DeliveryScorecard(
            grade,
            score,
            rationale,
            effectiveWallMs,
            milestones.HighestReached,
            milestones.ExpectedMinimum,
            milestones.Milestones);
    }

    private static string ComputeGrade(
        IReadOnlyList<Finding> findings,
        WorkflowSnapshot? snapshot,
        CreationMilestoneEvaluation milestones,
        long effectiveWallMs,
        IReadOnlyList<AgentMessageDoc> chatRows,
        int abortMs)
    {
        var hasBlockingLoop = findings.Any(f =>
            f.Code == "VALIDATION_UPSERT_LOOP" && string.Equals(f.Severity, "blocking", StringComparison.OrdinalIgnoreCase));
        var hasJourney = OutcomeDetectors.HasJourneyDeliveryForGrading(snapshot, chatRows);

        if (findings.Any(f => f.Code == "CREATION_ABORTED")) return "F";
        if (effectiveWallMs >= abortMs && !hasJourney) return "F";
        if (findings.Any(f => f.Code == "SESSION_STALLED_NO_DELIVERY") && hasBlockingLoop) return "F";
        if (hasBlockingLoop
            && string.Equals(milestones.ExpectedMinimum, CreationMilestoneIds.CampaignJourney, StringComparison.Ordinal)
            && snapshot?.JourneyRuleSetCount is not > 0)
            return "F";

        if (snapshot?.CreationComplete == true
            && snapshot.JourneyRuleSetCount is > 0
            && !milestones.Milestones.First(m => m.Id == CreationMilestoneIds.Verification).Reached)
            return "B";

        if (snapshot?.CreationComplete == true
            && (snapshot.JourneyRuleSetCount is not > 0
                || milestones.Milestones.First(m => m.Id == CreationMilestoneIds.Verification).Reached))
            return "A";
        if (snapshot?.JourneyRuleSetCount is > 0) return "B";

        if (findings.Any(f => f.Code == "SESSION_STALLED_NO_DELIVERY")
            || (milestones.HasMilestoneGap && MilestoneGapTiers(milestones) >= 2)
            || (milestones.HasMilestoneGap && hasBlockingLoop))
            return "D";

        if (milestones.HighestReached is CreationMilestoneIds.PointAccountTypes
            or CreationMilestoneIds.CampaignSetup)
            return "C";

        if (milestones.HasMilestoneGap) return "D";

        return "C";
    }

    private static int MilestoneGapTiers(CreationMilestoneEvaluation milestones)
    {
        var highest = Ordinal(milestones.HighestReached);
        var expected = Ordinal(milestones.ExpectedMinimum);
        if (highest < 0 || expected < 0) return 0;
        return expected - highest;
    }

    private static int Ordinal(string? milestoneId)
    {
        if (milestoneId is null) return -1;
        var idx = Array.IndexOf(CreationMilestoneIds.Ordered, milestoneId);
        return idx < 0 ? -1 : idx;
    }

    private static double GradeToScore(string grade) => grade switch
    {
        "A" => 4.0,
        "A-" => 3.67,
        "B+" => 3.0,
        "B" => 3.0,
        "B-" => 2.67,
        "C+" => 2.0,
        "C" => 2.0,
        "C-" => 1.67,
        "D+" => 1.0,
        "D" => 1.0,
        "D-" => 0.67,
        _ => 0.25
    };

    private static string BuildRationale(
        IReadOnlyList<Finding> findings,
        CreationMilestoneEvaluation milestones,
        long effectiveWallMs,
        string grade)
    {
        var parts = new List<string>();
        foreach (var code in new[] { "CREATION_ABORTED", "SESSION_STALLED_NO_DELIVERY", "VALIDATION_UPSERT_LOOP" })
        {
            var match = findings.FirstOrDefault(f => f.Code == code);
            if (match is not null)
                parts.Add($"{match.Code} ({match.Severity})");
        }

        if (milestones.HasMilestoneGap)
            parts.Add($"gap: {milestones.HighestReached ?? "none"}<{milestones.ExpectedMinimum ?? "none"}");

        parts.Add($"wall ~{effectiveWallMs:N0} ms");
        parts.Add($"grade {grade}");

        var text = string.Join("; ", parts);
        return text.Length <= 280 ? text : text[..277] + "…";
    }
}
