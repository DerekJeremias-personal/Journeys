using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Core.Models;

/// <summary>
/// In-memory campaign workflow snapshot for the HTTP campaign agent (mapped to/from a reserved <see cref="AgentMessage"/> row).
/// </summary>
public sealed class CampaignWorkflowState
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string TenantId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public CampaignWorkflowPhase Phase { get; set; }

    public CampaignWorkflowKind CampaignKind { get; set; }

    public bool UserSkippedEventModels { get; set; }

    public bool ModelGatePassed { get; set; }

    public CampaignWorkflowArtifactsDocument Artifacts { get; set; } = new();

    public static CampaignWorkflowState CreateDefault(string tenantId, string ownerUserId, string conversationId) =>
        new()
        {
            TenantId = tenantId,
            OwnerUserId = ownerUserId,
            ConversationId = conversationId,
            Phase = CampaignWorkflowPhase.DataAnalysis,
            CampaignKind = CampaignWorkflowKind.EventDriven,
            UserSkippedEventModels = false,
            ModelGatePassed = false,
            Artifacts = new CampaignWorkflowArtifactsDocument()
        };

    public static CampaignWorkflowState? FromWorkflowRow(AgentMessage? row)
    {
        if (row == null || !AgentMessage.IsWorkflowOrchestrationRow(row))
            return null;

        var phase = MapPhase(row.WorkflowPhase);
        var kind = Enum.TryParse<CampaignWorkflowKind>(row.WorkflowCampaignKind, ignoreCase: true, out var k)
            ? k
            : CampaignWorkflowKind.EventDriven;

        var artifacts = DeserializeArtifacts(row.Content);

        return new CampaignWorkflowState
        {
            TenantId = row.TenantId,
            OwnerUserId = row.OwnerUserId,
            ConversationId = row.ConversationId,
            Phase = phase,
            CampaignKind = kind,
            UserSkippedEventModels = row.WorkflowUserSkippedEventModels ?? false,
            ModelGatePassed = row.WorkflowModelGatePassed ?? false,
            Artifacts = artifacts
        };
    }

    public AgentMessage ToWorkflowRow()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentMessage(
            TenantId,
            OwnerUserId,
            ConversationId,
            sequence: 0,
            AgentMessage.WorkflowOrchestrationRole,
            SerializeArtifacts(Artifacts),
            linkedCampaignId: null,
            toolCallId: null,
            toolName: null,
            toolArgumentsJson: null,
            toolResultJson: null,
            id: AgentMessage.WorkflowOrchestrationDocumentId,
            createdate: now,
            lastupdated: now,
            workflowPhase: CampaignWorkflowPhaseNormalizer.Normalize(Phase).ToString(),
            workflowCampaignKind: CampaignKind.ToString(),
            workflowUserSkippedEventModels: UserSkippedEventModels,
            workflowModelGatePassed: ModelGatePassed);
    }

    public bool IsAwaitingApproval => Artifacts.AwaitingApproval != CampaignWorkflowApprovalKind.None;

    internal static CampaignWorkflowPhase MapPhase(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return CampaignWorkflowPhase.DataAnalysis;

        if (string.Equals(raw, "CampaignAndPat", StringComparison.OrdinalIgnoreCase))
            return CampaignWorkflowPhaseNormalizer.Normalize(CampaignWorkflowPhase.CampaignSetup);

        if (string.Equals(raw, "Verify", StringComparison.OrdinalIgnoreCase))
            return CampaignWorkflowPhase.Verification;

        if (!Enum.TryParse<CampaignWorkflowPhase>(raw, ignoreCase: true, out var p))
            return CampaignWorkflowPhase.DataAnalysis;

        return CampaignWorkflowPhaseNormalizer.Normalize(p);
    }

    private static CampaignWorkflowArtifactsDocument DeserializeArtifacts(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new CampaignWorkflowArtifactsDocument();

        try
        {
            return JsonSerializer.Deserialize<CampaignWorkflowArtifactsDocument>(content, JsonOpts)
                   ?? new CampaignWorkflowArtifactsDocument();
        }
        catch
        {
            return new CampaignWorkflowArtifactsDocument();
        }
    }

    private static string SerializeArtifacts(CampaignWorkflowArtifactsDocument artifacts) =>
        JsonSerializer.Serialize(artifacts, JsonOpts);
}
