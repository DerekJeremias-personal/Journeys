namespace Journeys.Core.Configuration;

/// <summary>
/// Options for campaign upsert validation (e.g. taxonomy path checks against model definitions).
/// </summary>
public class CampaignUpsertValidationOptions
{
    public const string SectionName = "CampaignUpsertValidation";

    /// <summary>
    /// Ordered list of model types to try when loading each id in <see cref="Models.Campaign.Events"/> (Backend GET requires modelType).
    /// </summary>
    public string[] EventModelTypeCandidates { get; set; } = new[] { "loyalty" };
}
