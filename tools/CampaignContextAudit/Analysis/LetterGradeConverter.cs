namespace CampaignContextAudit.Analysis;

public static class LetterGradeConverter
{
    public static string ToLetter(double score)
    {
        if (score >= 3.67) return "A";
        if (score >= 3.34) return "A-";
        if (score >= 3.0) return "B+";
        if (score >= 2.67) return "B";
        if (score >= 2.34) return "B-";
        if (score >= 2.0) return "C+";
        if (score >= 1.67) return "C";
        if (score >= 1.34) return "C-";
        if (score >= 1.0) return "D+";
        if (score >= 0.67) return "D";
        if (score >= 0.34) return "D-";
        return "F";
    }

    public static double ClampScore(double score) => Math.Clamp(score, 0.0, 4.0);
}
