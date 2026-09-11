namespace Journeys.DTO.Models;

/// <summary>One ranked eventable model candidate for the EventModels selection step.</summary>
public sealed class EventModelCandidate
{
    public string EventModelId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? ModelType { get; set; }

    /// <summary>Higher = stronger order/earning match.</summary>
    public int Score { get; set; }

    /// <summary>Human-readable reasons, e.g. "name:order", "amount:grandTotal".</summary>
    public List<string> MatchReasons { get; set; } = new();

    public bool HasMonetaryAttribute { get; set; }

    /// <summary>True when this candidate clears the strong-match threshold.</summary>
    public bool IsStrongMatch { get; set; }
}

/// <summary>Ranked candidate set computed by the host on entering the EventModels phase.</summary>
public sealed class EventModelCandidateSet
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Ordered by descending score; capped to a small list for prompting.</summary>
    public List<EventModelCandidate> Candidates { get; set; } = new();

    /// <summary>The single recommended default id, or null when no candidate is a strong match.</summary>
    public string? RecommendedDefaultId { get; set; }

    /// <summary>True when the catalog fetch failed (degrade to prose behavior).</summary>
    public bool FetchFailed { get; set; }
}
