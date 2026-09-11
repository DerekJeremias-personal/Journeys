namespace CampaignContextAudit.Analysis;

public sealed record RubricDimensionScore(
    int DimensionId,
    string Dimension,
    string Lens,
    double Score,
    string Grade,
    string Rationale);

public sealed record OverallGrade(double Score, string Grade);

public sealed record RubricScorecard(
    IReadOnlyList<RubricDimensionScore> Dimensions,
    OverallGrade Competency,
    OverallGrade Efficiency,
    OverallGrade? UncappedCompetency = null);
