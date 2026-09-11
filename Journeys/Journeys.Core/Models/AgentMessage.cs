using System.Text.Json.Serialization;

namespace Journeys.Core.Models;

/// <summary>
/// One chat message stored in Backend/Cosmos. Hierarchical partition (with tenant in API path):
/// PK = ownerUserId, PK2 = conversationId (see Backend model configuration).
/// </summary>
/// <remarks>
/// <para>Content vs tool payloads (Cosmos / debugging): <see cref="Content"/> is not the universal body field.
/// For <c>role = tool</c>, <see cref="Content"/> is intentionally empty and the tool output is in <see cref="ToolResultJson"/>
/// (see campaign agent orchestrator). For <c>role = assistant</c>, <see cref="Content"/> holds the MEAI transcript JSON string.
/// For <c>role = user</c>, <see cref="Content"/> is the user text. Empty <see cref="Content"/> on tool rows is expected, not data loss.</para>
/// </remarks>
public class AgentMessage : TenantedModelBase
{
    /// <summary>Reserved document id for per-conversation campaign workflow orchestration (same model/container as chat).</summary>
    public const string WorkflowOrchestrationDocumentId = "11111111-1111-4111-8111-111111111111";

    /// <summary>Role value for <see cref="WorkflowOrchestrationDocumentId"/> rows — excluded from LLM chat replay.</summary>
    public const string WorkflowOrchestrationRole = "workflow";

    [JsonConstructor]
    public AgentMessage(
        string tenantId,
        string ownerUserId,
        string conversationId,
        long sequence,
        string role,
        string? content,
        string? linkedCampaignId,
        string? toolCallId,
        string? toolName,
        string? toolArgumentsJson,
        string? toolResultJson,
        string? id = null,
        DateTimeOffset? createdate = null,
        DateTimeOffset? lastupdated = null,
        string? workflowPhase = null,
        string? workflowCampaignKind = null,
        bool? workflowUserSkippedEventModels = null,
        bool? workflowModelGatePassed = null,
        int? ttlSeconds = null,
        int? toolDurationMs = null,
        string? turnMetricsJson = null)
        : base(tenantId, id ?? Guid.NewGuid().ToString(), createdate, lastupdated)
    {
        OwnerUserId = ownerUserId;
        ConversationId = conversationId;
        Sequence = sequence;
        Role = role;
        Content = content ?? string.Empty;
        LinkedCampaignId = linkedCampaignId;
        ToolCallId = toolCallId;
        ToolName = toolName;
        ToolArgumentsJson = toolArgumentsJson;
        ToolResultJson = toolResultJson;
        WorkflowPhase = workflowPhase;
        WorkflowCampaignKind = workflowCampaignKind;
        WorkflowUserSkippedEventModels = workflowUserSkippedEventModels;
        WorkflowModelGatePassed = workflowModelGatePassed;
        TtlSeconds = ttlSeconds;
        ToolDurationMs = toolDurationMs;
        TurnMetricsJson = turnMetricsJson;
    }

    /// <summary>When set (or <see cref="Role"/> is <see cref="WorkflowOrchestrationRole"/>), this row holds campaign workflow state — not chat.</summary>
    public string? WorkflowPhase { get; set; }

    public string? WorkflowCampaignKind { get; set; }

    public bool? WorkflowUserSkippedEventModels { get; set; }

    public bool? WorkflowModelGatePassed { get; set; }

    public static bool IsWorkflowOrchestrationRow(AgentMessage m) =>
        m != null && (
            string.Equals(m.Role, WorkflowOrchestrationRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Id, WorkflowOrchestrationDocumentId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Partition key segment (JWT user id).</summary>
    public string OwnerUserId { get; set; }

    /// <summary>Partition key segment (thread id).</summary>
    public string ConversationId { get; set; }

    public long Sequence { get; set; }

    /// <summary>Chat role: <c>user</c>, <c>assistant</c>, <c>tool</c>, or workflow orchestration <c>workflow</c>.</summary>
    public string Role { get; set; }

    /// <summary>
    /// Primary text or serialized transcript for this row. For <c>tool</c> rows this is always empty; use <see cref="ToolResultJson"/>.
    /// For <c>assistant</c> rows this is MEAI JSON (serialized by the campaign agent transcript codec in the API layer).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>Optional campaign context for this message (denormalized).</summary>
    public string? LinkedCampaignId { get; set; }

    public string? ToolCallId { get; set; }

    public string? ToolName { get; set; }

    public string? ToolArgumentsJson { get; set; }

    /// <summary>Serialized tool/function result for <c>role = tool</c>; authoritative payload for replay. Null for non-tool rows.</summary>
    public string? ToolResultJson { get; set; }

    /// <summary>Cosmos DB document TTL in seconds. When set, the document is deleted after this interval from its last modification. Null omits TTL.</summary>
    [JsonPropertyName("ttl")]
    public int? TtlSeconds { get; set; }

    /// <summary>Wall-clock milliseconds for tool invocation (role=tool only).</summary>
    [JsonPropertyName("toolDurationMs")]
    public int? ToolDurationMs { get; set; }

    /// <summary>Per-turn metrics JSON summary (workflow row only).</summary>
    [JsonPropertyName("turnMetricsJson")]
    public string? TurnMetricsJson { get; set; }
}
