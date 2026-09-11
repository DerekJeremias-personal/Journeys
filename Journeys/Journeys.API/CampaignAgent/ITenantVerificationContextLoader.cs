using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent;

public interface ITenantVerificationContextLoader
{
    Task<TenantVerificationContext> LoadAsync(string routeTenantId, CancellationToken cancellationToken = default);
    void ApplyToArtifacts(CampaignWorkflowState state, TenantVerificationContext context);
}
