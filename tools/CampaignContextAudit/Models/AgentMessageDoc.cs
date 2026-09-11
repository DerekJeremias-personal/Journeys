using System.Text.Json.Serialization;

namespace CampaignContextAudit.Models;

public sealed class AgentMessageDoc
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("sequence")] public long Sequence { get; set; }
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("toolCallId")] public string? ToolCallId { get; set; }
    [JsonPropertyName("toolName")] public string? ToolName { get; set; }
    [JsonPropertyName("toolArgumentsJson")] public string? ToolArgumentsJson { get; set; }
    [JsonPropertyName("toolResultJson")] public string? ToolResultJson { get; set; }
    [JsonPropertyName("workflowPhase")] public string? WorkflowPhase { get; set; }
    [JsonPropertyName("workflowCampaignKind")] public string? WorkflowCampaignKind { get; set; }
    [JsonPropertyName("workflowModelGatePassed")] public bool? WorkflowModelGatePassed { get; set; }
    [JsonPropertyName("workflowUserSkippedEventModels")] public bool? WorkflowUserSkippedEventModels { get; set; }
    [JsonPropertyName("_ts")] public long? CosmosTimestamp { get; set; }
    [JsonPropertyName("tenantId")] public string? TenantId { get; set; }
    [JsonPropertyName("conversationid")] public string? ConversationId { get; set; }
    [JsonPropertyName("toolDurationMs")] public int? ToolDurationMs { get; set; }
    [JsonPropertyName("turnMetricsJson")] public string? TurnMetricsJson { get; set; }

    public const string WorkflowOrchestrationDocumentId = "11111111-1111-4111-8111-111111111111";

    public bool IsWorkflowRow =>
        string.Equals(Role, "workflow", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Id, WorkflowOrchestrationDocumentId, StringComparison.OrdinalIgnoreCase);

    public int CharCount => (Content?.Length ?? 0) + (ToolResultJson?.Length ?? 0);
}
