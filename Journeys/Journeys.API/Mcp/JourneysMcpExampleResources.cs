using System.ComponentModel;
using Journeys.API.Examples;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Journeys.API.Mcp;

/// <summary>
/// MCP resources for repo-backed example campaigns (tenant mericantires).
/// </summary>
[McpServerResourceType]
public class JourneysMcpExampleResources
{
    private readonly JourneysExamplePack _pack;

    public JourneysMcpExampleResources(JourneysExamplePack pack)
    {
        _pack = pack ?? throw new ArgumentNullException(nameof(pack));
    }

    [McpServerResource(UriTemplate = "journeys://examples/campaigns", Name = "ListExampleCampaignsResource", MimeType = "application/json")]
    [Description("Lists example campaign JSON packs (id, title, summary, tenant mericantires).")]
    public async Task<ResourceContents> ListExamplesResource(CancellationToken cancellationToken = default)
    {
        var json = await _pack.ListSummariesJsonAsync(cancellationToken).ConfigureAwait(false);
        return new TextResourceContents
        {
            Uri = "journeys://examples/campaigns",
            MimeType = "application/json",
            Text = json
        };
    }

    [McpServerResource(UriTemplate = "journeys://examples/campaign/{exampleId}", Name = "GetExampleCampaignResource", MimeType = "application/json")]
    [Description("Returns raw JSON for one example campaign by id.")]
    public async Task<ResourceContents> GetExampleCampaignResource(
        [Description("Example id from the index (e.g. tier-system-campaign).")] string exampleId,
        CancellationToken cancellationToken = default)
    {
        var json = await _pack.GetExampleJsonAsync(exampleId, cancellationToken).ConfigureAwait(false);
        return new TextResourceContents
        {
            Uri = $"journeys://examples/campaign/{exampleId}",
            MimeType = "application/json",
            Text = json
        };
    }
}
