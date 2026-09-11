namespace Journeys.API.CampaignAgent;

/// <summary>Options for <see cref="CampaignAgentTokenBudget.Apply"/>.</summary>
public sealed record HistoryBudgetOptions
{
    public bool PinFirstUserSegment { get; init; } = true;
    public bool PinMostRecentCompletedSegment { get; init; } = true;
    public bool ShrinkToolResults { get; init; } = true;
    public int ShrinkThresholdChars { get; init; } = 2048;
    public int MaxChars { get; init; } = CampaignAgentTokenBudget.DefaultMaxChars;
    public int MaxUserTurns { get; init; } = CampaignAgentTokenBudget.DefaultMaxUserTurns;

    /// <summary>When true, contract summary rows in protected segments are exempt from shrink.</summary>
    public bool JourneyContractSummaryPinActive { get; init; }

    public static HistoryBudgetOptions Default { get; } = new();
}
