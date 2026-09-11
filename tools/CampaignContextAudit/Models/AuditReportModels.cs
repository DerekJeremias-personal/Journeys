using System.Text.Json.Serialization;

namespace CampaignContextAudit.Models;

public sealed class AuditReportJson
{
    public int SchemaVersion { get; init; } = 3;
    public required string SourceFile { get; init; }
    public required string ConversationId { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required OutcomeSummary OutcomeSummary { get; init; }
    public required List<FindingJson> Findings { get; init; }
    public required List<BudgetTurnJson> BudgetByTurn { get; init; }
    public PerformanceSummaryJson? PerformanceSummary { get; init; }
    public DeliveryScorecardJson? Delivery { get; init; }
    public List<RubricDimensionJson> Rubric { get; init; } = [];
    public OverallGradeJson? OverallCompetency { get; init; }
    public OverallGradeJson? OverallEfficiency { get; init; }
}

public sealed class RubricDimensionJson
{
    public int DimensionId { get; init; }
    public required string Dimension { get; init; }
    public required string Lens { get; init; }
    public required string Grade { get; init; }
    public double Score { get; init; }
    public required string Rationale { get; init; }
}

public sealed class OverallGradeJson
{
    public required string Grade { get; init; }
    public double Score { get; init; }
    public string? UncappedGrade { get; init; }
    public double? UncappedScore { get; init; }
}

public sealed class DeliveryScorecardJson
{
    public required string Grade { get; init; }
    public double Score { get; init; }
    public required string Rationale { get; init; }
    public long EffectiveWallMs { get; init; }
    public string? HighestReached { get; init; }
    public string? ExpectedMinimum { get; init; }
    public List<MilestoneJson> Milestones { get; init; } = [];
}

public sealed class MilestoneJson
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public bool Reached { get; init; }
}

public sealed class OutcomeSummary
{
    public bool? CreationComplete { get; init; }
    public int? JourneyRuleSetCount { get; init; }
    public string? WorkflowPhase { get; init; }
    public string? LinkedCampaignId { get; init; }
}

public sealed class FindingJson
{
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required IReadOnlyList<string> CitedSequences { get; init; }
    public required string Summary { get; init; }
}

public sealed class BudgetTurnJson
{
    public long UserSequence { get; init; }
    public required string Phase { get; init; }
    public int StableChars { get; init; }
    public int HistoryChars { get; init; }
    public int SessionChars { get; init; }
    public int TotalChars { get; init; }
}

public sealed class PerformanceSummaryJson
{
    public required string DataSource { get; init; }
    public int TurnCount { get; init; }
    public long TotalWallMs { get; init; }
    public required PerformanceByCategoryJson ByCategory { get; init; }
    public List<SlowToolJson> SlowTools { get; init; } = [];
}

public sealed class PerformanceByCategoryJson
{
    public long Llm { get; init; }
    public long Tools { get; init; }
    public long Persist { get; init; }
    public long Other { get; init; }
}

public sealed class SlowToolJson
{
    public required string Tool { get; init; }
    public long MaxMs { get; init; }
    public int Count { get; init; }
    public required IReadOnlyList<string> Sequences { get; init; }
}
