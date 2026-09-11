using System.ComponentModel;
using System.Text.Json;
using Journeys.API.Examples;
using ModelContextProtocol.Server;

namespace Journeys.API.Mcp;

/// <summary>
/// MCP tools for listing and reading example campaigns (fictitious tenant mericantires).
/// </summary>
[McpServerToolType]
public class JourneysMcpExampleTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly JourneysExamplePack _pack;

    public JourneysMcpExampleTools(JourneysExamplePack pack)
    {
        _pack = pack ?? throw new ArgumentNullException(nameof(pack));
    }

    [McpServerTool, Description("Lists example campaign JSON packs shipped with the API (non-customer tenant mericantires). Use GetExampleCampaign to load one by id.")]
    public async Task<string> ListExampleCampaigns(
        [Description("Must be mericantires.")] string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(tenantId, JourneysExamplePack.ExampleTenantId, StringComparison.Ordinal))
            return JsonSerializer.Serialize(new { error = $"tenantId must be '{JourneysExamplePack.ExampleTenantId}' for example campaign tools." }, JsonOptions);

        return await _pack.ListSummariesJsonAsync(cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool, Description("Returns raw JSON for one example campaign id (from ListExampleCampaigns). Tenant must be mericantires.")]
    public async Task<string> GetExampleCampaign(
        [Description("Must be mericantires.")] string tenantId,
        [Description("Example id, e.g. tier-system-campaign.")] string exampleId,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(tenantId, JourneysExamplePack.ExampleTenantId, StringComparison.Ordinal))
            return JsonSerializer.Serialize(new { error = $"tenantId must be '{JourneysExamplePack.ExampleTenantId}' for example campaign tools." }, JsonOptions);

        return await _pack.GetExampleJsonAsync(exampleId, cancellationToken).ConfigureAwait(false);
    }
}
