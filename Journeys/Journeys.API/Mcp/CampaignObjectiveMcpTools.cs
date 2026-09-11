using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Journeys.API.Mcp;

/// <summary>
/// MCP tool used during the DataAnalysis phase when data-warehouse tools are disabled.
/// Captures the campaign objective the agent worked out with the user and echoes it back
/// as the CampaignDesignBrief source (no external data access).
/// </summary>
[McpServerToolType]
public class CampaignObjectiveMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "ProposeCampaignDesignBrief")]
    [Description("Propose a campaign design brief from the objective worked out with the user (used when data-warehouse analysis is unavailable). Call once the objective, audience, mechanic, and success criteria are clear.")]
    public string ProposeCampaignDesignBrief(
        [Description("What the campaign is trying to achieve (required).")] string objective,
        [Description("Target audience / segment / accounts.")] string? audience = null,
        [Description("How members qualify and the reward mechanic.")] string? mechanic = null,
        [Description("Campaign class: 'event-driven' or 'tag-first'.")] string? campaignClass = null,
        [Description("What the user wants to measure for success.")] string? successCriteria = null)
    {
        var resolvedClass = string.Equals(campaignClass?.Trim(), "tag-first", StringComparison.OrdinalIgnoreCase)
            ? "tag-first"
            : "event-driven";

        return JsonSerializer.Serialize(new
        {
            captured = true,
            objective = objective?.Trim(),
            audience = string.IsNullOrWhiteSpace(audience) ? null : audience.Trim(),
            mechanic = string.IsNullOrWhiteSpace(mechanic) ? null : mechanic.Trim(),
            campaignClass = resolvedClass,
            successCriteria = string.IsNullOrWhiteSpace(successCriteria) ? null : successCriteria.Trim()
        }, JsonOptions);
    }
}
