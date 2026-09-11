namespace CampaignContextAudit.Analysis;

public static class CreationMilestoneIds
{
    public const string DesignBrief = "DesignBrief";
    public const string EventModels = "EventModels";
    public const string CampaignSetup = "CampaignSetup";
    public const string PointAccountTypes = "PointAccountTypes";
    public const string CampaignJourney = "CampaignJourney";
    public const string Verification = "Verification";
    public const string Done = "Done";

    public static readonly string[] Ordered =
    [
        DesignBrief, EventModels, CampaignSetup, PointAccountTypes,
        CampaignJourney, Verification, Done
    ];
}

public sealed record MilestoneResult(string Id, string Label, bool Reached);

public sealed record CreationMilestoneEvaluation(
    IReadOnlyList<MilestoneResult> Milestones,
    string? HighestReached,
    string? ExpectedMinimum,
    bool HasMilestoneGap);

public sealed record DeliveryScorecard(
    string Grade,
    double Score,
    string Rationale,
    long EffectiveWallMs,
    string? HighestReached,
    string? ExpectedMinimum,
    IReadOnlyList<MilestoneResult> Milestones);
