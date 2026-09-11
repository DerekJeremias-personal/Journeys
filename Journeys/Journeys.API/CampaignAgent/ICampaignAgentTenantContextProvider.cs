namespace Journeys.API.CampaignAgent;

public interface ICampaignAgentTenantContextProvider
{
    /// <summary>
    /// Loads tenant registry row for the route tenant key; returns null if missing or on failure.
    /// Uses memory cache when document version (LastUpdated/ETag) is unchanged.
    /// </summary>
    Task<CampaignAgentTenantLlmSlice?> GetSliceAsync(string routeTenantId, CancellationToken cancellationToken = default);
}
