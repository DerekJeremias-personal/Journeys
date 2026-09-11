using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>Maps completion <see cref="ChatMessage"/> instances to persisted <see cref="AgentMessage"/> rows.</summary>
internal static class CampaignAgentTranscriptPersistence
{
    public const int DefaultMaxPersistedAssistantNarrativeChars = 8192;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Maps one <see cref="ChatMessage"/> from the completion response into one or more <see cref="AgentMessage"/> rows.
    /// Embedded assistant <see cref="FunctionResultContent"/> is materialized as tool rows; envelopes store text + calls only.
    /// </summary>
    public static List<AgentMessage> BuildPersistenceRowsFromCompletionMessage(
        string tenantId,
        string userId,
        string conversationId,
        ref long sequence,
        string? linkedCampaignId,
        HashSet<string> satisfiedCallIds,
        ChatMessage msg,
        CampaignAgentTurnMetricsCollector? metrics = null,
        int maxPersistedToolResultChars = ToolResultHistoryStub.DefaultMaxPersistedChars,
        CampaignWorkflowPhase? workflowPhase = null,
        int maxPersistedAssistantNarrativeChars = DefaultMaxPersistedAssistantNarrativeChars)
    {
        var rows = new List<AgentMessage>();
        if (msg.Role == ChatRole.User)
            return rows;

        if (msg.Role == ChatRole.Tool)
        {
            foreach (var content in msg.Contents)
            {
                if (content is not FunctionResultContent fr || string.IsNullOrEmpty(fr.CallId))
                    continue;
                if (satisfiedCallIds.Contains(fr.CallId))
                    continue;

                rows.Add(CreateToolRow(
                    tenantId, userId, conversationId, ref sequence, linkedCampaignId,
                    fr.CallId, null, fr.Result, metrics, maxPersistedToolResultChars, workflowPhase));
                satisfiedCallIds.Add(fr.CallId);
            }

            return rows;
        }

        if (msg.Role == ChatRole.Assistant)
        {
            var callIdToName = BuildCallIdToToolName(msg);
            foreach (var content in msg.Contents)
            {
                if (content is not FunctionResultContent fr || string.IsNullOrEmpty(fr.CallId))
                    continue;
                if (satisfiedCallIds.Contains(fr.CallId))
                    continue;

                callIdToName.TryGetValue(fr.CallId, out var toolName);
                rows.Add(CreateToolRow(
                    tenantId, userId, conversationId, ref sequence, linkedCampaignId,
                    fr.CallId, toolName, fr.Result, metrics, maxPersistedToolResultChars, workflowPhase));
                satisfiedCallIds.Add(fr.CallId);
            }

            var slimContents = msg.Contents
                .Where(c => c is TextContent or FunctionCallContent)
                .Select(c => c is TextContent tc
                    ? (AIContent)new TextContent(TrimNarrativeIfNeeded(tc.Text, workflowPhase, maxPersistedAssistantNarrativeChars))
                    : c)
                .ToList();
            var slim = new ChatMessage(ChatRole.Assistant, slimContents)
            {
                MessageId = msg.MessageId,
                AuthorName = msg.AuthorName,
                CreatedAt = msg.CreatedAt,
                RawRepresentation = msg.RawRepresentation,
                AdditionalProperties = msg.AdditionalProperties
            };

            CampaignAgentTranscriptRules.RegisterAssistantResultsFromMessage(slim, satisfiedCallIds);
            var serialized = CampaignAgentMeaiTranscriptCodec.SerializeTranscriptMessage(slim);
            rows.Add(new AgentMessage(
                tenantId,
                userId,
                conversationId,
                sequence++,
                "assistant",
                serialized,
                linkedCampaignId,
                null, null, null, null));
        }

        return rows;
    }

    private static AgentMessage CreateToolRow(
        string tenantId,
        string userId,
        string conversationId,
        ref long sequence,
        string? linkedCampaignId,
        string callId,
        string? toolName,
        object? result,
        CampaignAgentTurnMetricsCollector? metrics,
        int maxPersistedToolResultChars,
        CampaignWorkflowPhase? workflowPhase)
    {
        var resultJson = JsonSerializer.Serialize(result, JsonOpts);
        resultJson = MaybeDigestBloatedToolResult(toolName, resultJson, workflowPhase);
        resultJson = ToolResultHistoryStub.MaybeStub(toolName, resultJson, maxPersistedToolResultChars);
        return new AgentMessage(
            tenantId,
            userId,
            conversationId,
            sequence++,
            "tool",
            string.Empty,
            linkedCampaignId,
            callId,
            toolName,
            null,
            resultJson,
            toolDurationMs: metrics?.GetToolDurationMs(callId));
    }

    private const int BloatedToolResultDigestThresholdChars = 900;
    private const int ProposeBriefSlimThresholdChars = 1200;

    private static string MaybeDigestBloatedToolResult(
        string? toolName,
        string resultJson,
        CampaignWorkflowPhase? workflowPhase)
    {
        if (IsGetModelTool(toolName))
        {
            if (IsVerifyPhase(workflowPhase))
            {
                var verifyDigest = ModelReadDigester.DigestForVerification(resultJson);
                if (verifyDigest.Transformed)
                    return verifyDigest.Json;
            }
            else if (resultJson.Length >= BloatedToolResultDigestThresholdChars
                     || IsCreationPhase(workflowPhase))
            {
                var creationDigest = ModelReadDigester.DigestForCreation(resultJson);
                if (creationDigest.Transformed)
                    return creationDigest.Json;
            }

            if (resultJson.Length >= BloatedToolResultDigestThresholdChars)
            {
                var digest = ModelReadDigester.Digest(resultJson);
                if (digest.Transformed)
                    return digest.Json;
            }

            return resultJson;
        }

        if (IsProposeBriefTool(toolName))
            return MaybeSlimProposeBriefResult(resultJson);

        return resultJson;
    }

    private static bool IsCreationPhase(CampaignWorkflowPhase? phase) =>
        phase is CampaignWorkflowPhase.EventModels
            or CampaignWorkflowPhase.DataAnalysis
            or CampaignWorkflowPhase.CampaignSetup
            or CampaignWorkflowPhase.PointAccountTypes
            or CampaignWorkflowPhase.CampaignBuild
            or CampaignWorkflowPhase.CampaignJourney;

    private static string MaybeSlimProposeBriefResult(string resultJson)
    {
        if (resultJson.Length < ProposeBriefSlimThresholdChars)
            return resultJson;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return resultJson;

            var slim = new Dictionary<string, object?>();
            if (TryReadStringProperty(root, "objective", out var objective))
                slim["objective"] = objective;
            if (TryReadStringProperty(root, "campaignClass", out var campaignClass))
                slim["campaignClass"] = campaignClass;
            if (root.TryGetProperty("captured", out var captured)
                && captured.ValueKind is JsonValueKind.True or JsonValueKind.False)
                slim["captured"] = captured.GetBoolean();

            if (slim.Count == 0)
                return resultJson;

            return JsonSerializer.Serialize(slim, JsonOpts);
        }
        catch (JsonException)
        {
            return resultJson;
        }
    }

    private static bool TryReadStringProperty(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;

        value = prop.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsProposeBriefTool(string? toolName) =>
        string.Equals(toolName, "ProposeCampaignDesignBrief", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "propose_campaign_design_brief", StringComparison.OrdinalIgnoreCase);

    private static bool IsVerifyPhase(CampaignWorkflowPhase? phase) =>
        phase is CampaignWorkflowPhase.Verification or CampaignWorkflowPhase.Done;

    private static bool IsGetModelTool(string? toolName) =>
        string.Equals(toolName, "get_model", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetModel", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldTrimNarrative(CampaignWorkflowPhase? phase) =>
        phase is CampaignWorkflowPhase.EventModels or CampaignWorkflowPhase.DataAnalysis;

    private static string? TrimNarrativeIfNeeded(string? text, CampaignWorkflowPhase? phase, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || !ShouldTrimNarrative(phase) || text.Length <= maxChars)
            return text;

        return text[..maxChars] + "\n\n[historyNarrativeTrimmed: event-models segment]";
    }

    private static Dictionary<string, string> BuildCallIdToToolName(ChatMessage msg)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (msg.Contents is null)
            return map;

        foreach (var c in msg.Contents)
        {
            if (c is not FunctionCallContent fc || string.IsNullOrEmpty(fc.CallId))
                continue;
            map[fc.CallId] = fc.Name ?? string.Empty;
        }

        return map;
    }
}
