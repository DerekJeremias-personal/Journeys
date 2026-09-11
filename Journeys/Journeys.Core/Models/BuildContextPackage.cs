namespace Journeys.Core.Models;

/// <summary>
/// Situation-aware build context for supplemental governance, salient facts, and coaches.
/// </summary>
public sealed record BuildContextPackage(
    CampaignBuildSubStep SubStep,
    RuleEpisode Episode,
    string? LastViolationCode,
    string? PinnedPatternId,
    bool ToolSurfaceIncludesPatUpsert);
